using System.Text.Json;
using Accounting.Application.Abstractions;
using Accounting.Application.Common;
using Accounting.Contracts;
using Accounting.Contracts.Assistant;
using Accounting.Domain.Entities;
using Accounting.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Accounting.Application.Features.Assistant;

internal static class AssistantQueries
{
    public static Guid RequireTenantId(ICurrentTenant currentTenant) =>
        currentTenant.TenantId
            ?? throw new ConflictException("Aktif işletme bağlamı bulunamadı. X-Tenant-Id başlığını gönderin.");
}

/// <summary>Sistem yönergesi — model araç dışından rakam üretmesin.</summary>
internal static class AssistantPrompts
{
    public const string System =
        """
        Sen mikro işletme sahiplerine yardımcı olan Türkçe konuşan bir ön muhasebe asistanısın.
        Finansal verileri yalnızca sağlanan araçlardan al; araç çağırmadan rakam belirtme, asla rakam uydurma.
        Tutarları "1.234,56 TL" biçiminde yaz. Yanıtların kısa ve anlaşılır olsun; veri yoksa bunu açıkça söyle
        ve kullanıcıya örnek sorular öner. SQL ya da teknik sorgular hakkında asla yardım etme.
        """;
}

/// <summary>
/// AI asistan sorusu: sohbet geçmişini bağlam
/// olarak verir, sağlayıcının onaylı araç çağrılarını yürütür ve soru + yanıt
/// çiftini geçmişe yazar. AI hiçbir zaman SQL üretip çalıştırmaz — araç kayıt
/// defterindeki isimler dışındaki çağrılar reddedilir.
/// </summary>
public sealed class AskAssistantHandler(
    IApplicationDbContext db,
    ICurrentTenant currentTenant,
    ICurrentUser currentUser,
    IEnumerable<IAiTool> tools,
    IAiProvider provider,
    IOptions<AiOptions> aiOptions,
    TimeProvider timeProvider,
    IFeatureGuard featureGuard)
{
    /// <summary>Sağlayıcıya bağlam olarak verilen en fazla geçmiş mesaj sayısı.</summary>
    private const int HistoryContextMessages = 10;

    public async Task<AskAssistantResponse> HandleAsync(AskAssistantRequest request, CancellationToken cancellationToken)
    {
        var tenantId = AssistantQueries.RequireTenantId(currentTenant);
        var userId = currentUser.UserId
            ?? throw new AppException("Soruyu soran kullanıcı çözülemedi.", 401, "Oturum gerekli");
        var question = request.Question.Trim();

        // Plan kısıtı: AI özelliği plana bağlı.
        await featureGuard.EnsureFeatureAsync(tenantId, PlanFeatures.AiAssistant, cancellationToken);

        // Kullanım limiti: plan limiti öncelikli, yoksa genel varsayılan.
        var monthlyLimit = await featureGuard.AiMonthlyLimitAsync(
            tenantId, aiOptions.Value.MonthlyQuestionLimit, cancellationToken);
        var today = Dates.ToUtcDate(timeProvider.GetUtcNow().UtcDateTime);
        var monthStart = new DateTime(today.Year, today.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var monthQuestions = await db.AiMessages.AsNoTracking()
            .CountAsync(m => m.TenantId == tenantId
                && m.Role == AiMessageRole.User
                && m.CreatedAtUtc >= monthStart, cancellationToken);
        if (monthQuestions >= monthlyLimit)
        {
            throw new AppException(
                $"Bu ayın AI soru limiti doldu ({monthlyLimit} soru).", 429, "Limit aşıldı");
        }

        // Bağlam: kullanıcının bu işletmedeki son mesajları (kronolojik).
        var recent = await db.AiMessages.AsNoTracking()
            .Where(m => m.TenantId == tenantId && m.UserId == userId)
            .OrderByDescending(m => m.CreatedAtUtc)
            .ThenByDescending(m => m.Id)
            .Take(HistoryContextMessages)
            .ToListAsync(cancellationToken);
        recent.Reverse();

        var messages = recent
            .Select(m => new AiChatMessage(
                m.Role == AiMessageRole.User ? "user" : "assistant",
                m.Content))
            .ToList();
        messages.Add(new AiChatMessage("user", question));

        var definitions = tools
            .Select(t => new AiToolDefinition(t.Name, t.Description, t.ParametersJsonSchema))
            .ToList();

        // Sağlayıcının araç çağrılarını burada yürütülür: yalnızca kayıt defterindeki
        // araçlar çalışabilir, hepsi TenantId filtresiyle.
        async Task<string> ExecuteToolAsync(AiToolCall call, CancellationToken ct)
        {
            var tool = tools.FirstOrDefault(t => string.Equals(t.Name, call.Name, StringComparison.Ordinal));
            if (tool is null)
            {
                return $$"""{"error":"Bilinmeyen araç: {{call.Name}}"}""";
            }

            JsonElement arguments;
            try
            {
                arguments = string.IsNullOrWhiteSpace(call.ArgumentsJson)
                    ? default
                    : JsonDocument.Parse(call.ArgumentsJson).RootElement.Clone();
            }
            catch (JsonException)
            {
                return """{"error":"Araç argümanları geçersiz JSON."}""";
            }

            try
            {
                var result = await tool.ExecuteAsync(tenantId, arguments, ct);
                return JsonSerializer.Serialize(result);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Araç hatası modele hatayla döner; soru akışı düşmez.
                return $$"""{"error":"{{ex.Message.Replace("\"", "'")}}"}""";
            }
        }

        string answer;
        try
        {
            answer = await provider.CompleteAsync(
                new AiChatRequest(AssistantPrompts.System, messages, definitions),
                ExecuteToolAsync,
                cancellationToken);
        }
        catch (Exception ex) when (ex is not AppException and not OperationCanceledException)
        {
            throw new AppException("Asistana şu anda ulaşılamadı; lütfen tekrar deneyin.", 502, "Asistan hatası");
        }

        if (string.IsNullOrWhiteSpace(answer))
        {
            answer = "Üzgünüm, bu soruya yanıt üretemedim. Farklı bir ifadeyle tekrar sorar mısınız?";
        }

        var now = timeProvider.GetUtcNow().UtcDateTime;
        db.AiMessages.AddRange(
            new AiMessage
            {
                TenantId = tenantId,
                UserId = userId,
                Role = AiMessageRole.User,
                Content = question,
                CreatedAtUtc = now,
            },
            new AiMessage
            {
                TenantId = tenantId,
                UserId = userId,
                Role = AiMessageRole.Assistant,
                Content = answer,
                // 1 ms: PostgreSQL timestamptz mikrosaniye hassasiyetinde tick'i yutar;
                // yanıt sorudan sonraysa geçmiş sıralaması (en yeni önce) tutarlı kalır.
                CreatedAtUtc = now.AddMilliseconds(1),
            });
        await db.SaveChangesAsync(cancellationToken);

        return new AskAssistantResponse(answer, provider.Name);
    }
}

/// <summary>Sohbet geçmişi — kullanıcının bu işletmedeki mesajları, en yeni önce.</summary>
public sealed class ListAssistantHistoryHandler(
    IApplicationDbContext db,
    ICurrentTenant currentTenant,
    ICurrentUser currentUser)
{
    private const int MaxPageSize = 100;

    public async Task<PagedResponse<AiMessageDto>> HandleAsync(
        int page, int pageSize, CancellationToken cancellationToken)
    {
        var tenantId = AssistantQueries.RequireTenantId(currentTenant);
        var userId = currentUser.UserId
            ?? throw new AppException("Oturum kullanıcısı çözülemedi.", 401, "Oturum gerekli");
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize <= 0 ? 20 : pageSize, 1, MaxPageSize);

        var query = db.AiMessages.AsNoTracking()
            .Where(m => m.TenantId == tenantId && m.UserId == userId);

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(m => m.CreatedAtUtc)
            .ThenByDescending(m => m.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(m => new AiMessageDto(m.Id, m.Role.ToString(), m.Content, m.CreatedAtUtc))
            .ToListAsync(cancellationToken);

        return new PagedResponse<AiMessageDto>(items, page, pageSize, totalCount);
    }
}
