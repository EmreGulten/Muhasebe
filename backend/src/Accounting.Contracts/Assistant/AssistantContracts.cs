namespace Accounting.Contracts.Assistant;

/// <summary>AI asistana soru.</summary>
public sealed record AskAssistantRequest(string Question);

/// <summary>
/// Asistan yanıtı. Provider alanı yanıtın hangi sağlayıcıdan geldiğini belirtir
/// ("openai" ya da "offline") — API anahtarı yoksa offline mod devrededir.
/// </summary>
public sealed record AskAssistantResponse(string Answer, string Provider);

/// <summary>Sohbet geçmişi satırı — kullanıcı soruları ve asistan yanıtları.</summary>
public sealed record AiMessageDto(Guid Id, string Role, string Content, DateTime CreatedAtUtc);
