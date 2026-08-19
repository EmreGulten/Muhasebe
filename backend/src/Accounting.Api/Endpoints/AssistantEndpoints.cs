using Accounting.Api.Authorization;
using Accounting.Api.Middleware;
using Accounting.Application.Features.Assistant;
using Accounting.Contracts.Assistant;
using Accounting.Domain.Authorization;

namespace Accounting.Api.Endpoints;

/// <summary>AI asistan uç noktaları (muhasebe.md bölüm 11, PHASE 9).</summary>
public static class AssistantEndpoints
{
    public static void MapAssistantEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/assistant").WithTags("AI Asistan");

        group.MapPost("/ask", async (
                AskAssistantRequest request,
                AskAssistantHandler handler,
                CancellationToken cancellationToken) =>
            Results.Ok(await handler.HandleAsync(request, cancellationToken)))
            .WithName("AskAssistant")
            .WithSummary("İşletme verileri üzerinden doğal dilde soru — yalnızca onaylı araçlar çağrılır")
            .RequireTenant()
            .RequirePermission(Permissions.AiAssistantUse);

        group.MapGet("/history", async (
                int? page,
                int? pageSize,
                ListAssistantHistoryHandler handler,
                CancellationToken cancellationToken) =>
            Results.Ok(await handler.HandleAsync(page ?? 1, pageSize ?? 20, cancellationToken)))
            .WithName("ListAssistantHistory")
            .WithSummary("Sohbet geçmişi — kullanıcının soruları ve asistan yanıtları, en yeni önce")
            .RequireTenant()
            .RequirePermission(Permissions.AiAssistantUse);
    }
}
