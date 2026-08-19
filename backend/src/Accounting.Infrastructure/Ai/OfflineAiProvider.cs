using System.Text.Json;
using Accounting.Application.Abstractions;

namespace Accounting.Infrastructure.Ai;

/// <summary>
/// Offline asistan (muhasebe.md bölüm 11): API anahtarı yoksa devreye girer.
/// Anahtar kelime eşleştirmesiyle plan bölüm 11.1'deki onaylı araçlardan
/// birini seçip çağırır; yanıt aracın "summary" alanından üretilir. Dış ağa
/// hiç çıkmaz, SQL'e hiç dokunmaz — diğer sağlayıcılarla aynı araç sözleşmesi
/// üzerinden çalışır.
/// </summary>
public sealed class OfflineAiProvider : IAiProvider
{
    public string Name => "offline";

    private const string Guide =
        """
        Bu soruyu offline modda eşleştiremedim. Şunları sorabilirsiniz:
        • "Bu ay ne kadar kazandım?"
        • "Bana borcu olan müşterileri göster."
        • "En çok satan ürünlerim hangileri?"
        • "Hangi ürünlerin stoğu bitmek üzere?"
        • "Bu ay en yüksek gider kategorim nedir?"
        • "Önümüzdeki 7 günde ne kadar ödeme yapmam gerekir?"
        • "Geçen aya göre giderim nasıl değişti?"
        """;

    public async Task<string> CompleteAsync(
        AiChatRequest request,
        Func<AiToolCall, CancellationToken, Task<string>> executeTool,
        CancellationToken cancellationToken)
    {
        var question = request.Messages.LastOrDefault(m => m.Role == "user")?.Content ?? string.Empty;
        var normalized = question.ToLowerInvariant();

        var tool = Route(normalized);
        if (tool is null)
        {
            return Guide;
        }

        var result = await executeTool(new AiToolCall("offline", tool, "{}"), cancellationToken);
        var summary = TryGetSummary(result);
        return summary is null
            ? Guide
            : $"{summary}\n\n(Offline asistan — veriler yalnızca onaylı iş araçlarından okundu.)";
    }

    private static string? TryGetSummary(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            return document.RootElement.TryGetProperty("summary", out var summary)
                && summary.ValueKind == JsonValueKind.String
                ? summary.GetString()
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>Türkçe anahtar kelimelerden aracı seçer; sıra önemlidir (özel → genel).</summary>
    internal static string? Route(string normalized)
    {
        if (ContainsAny(["en çok satan", "çok satan", "satılan ürün", "satış yapan ürün"]))
        {
            return "get_top_products";
        }

        if (ContainsAny(["stok", "kritik"]))
        {
            return "get_low_stock_products";
        }

        if (ContainsAny(["geçen ay", "karşılaştır", "kıyas", "önceki ay"]))
        {
            return "compare_months";
        }

        if (ContainsAny(["gider kategori", "hangi kategori", "kategorim", "giderlerim", "en yüksek gider"]))
        {
            return "get_expense_breakdown";
        }

        if (ContainsAny(["gecikmiş", "geciken", "gecikme", "borcu olan", "borçlu"]))
        {
            return "get_overdue_receivables";
        }

        if (ContainsAny(["ödeme"]) && ContainsAny(["önümüzdeki", "yaklaşan", "vade", "ne kadar", "kaç gün"]))
        {
            return "get_upcoming_payments";
        }

        if (ContainsAny(["bakiye", "müşterinin borcu", "borcu var", "alacağım", "müşterilerim"]))
        {
            return "get_customer_balance";
        }

        if (ContainsAny(["kazan", " kâr", "kârım", "net kazanç", "ciro", "giderim nedir", "gider"]))
        {
            return "get_monthly_profit";
        }

        return null;

        bool ContainsAny(IEnumerable<string> keywords) =>
            keywords.Any(keyword => normalized.Contains(keyword, StringComparison.Ordinal));
    }
}
