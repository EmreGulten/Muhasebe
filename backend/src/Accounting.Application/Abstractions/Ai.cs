namespace Accounting.Application.Abstractions;

// ---- AI asistan sağlayıcı soyutlaması
//
// AI hiçbir zaman SQL üretip çalıştırmaz: sağlayıcı yalnızca buradaki araç
// çağrıları üzerinden backend'in onayladığı sorgulara ulaşır. Araçların
// kendileri Application katmanındaki IAiTool kayıt defterinde tanımlıdır.

/// <summary>Sohbet iletisi — "system", "user" ya da "assistant".</summary>
public sealed record AiChatMessage(string Role, string Content);

/// <summary>Sağlayıcıya tanıtılan onaylı iş aracı.</summary>
public sealed record AiToolDefinition(string Name, string Description, string ParametersJsonSchema);

/// <summary>Sağlayıcıdan gelen araç çağrısı.</summary>
public sealed record AiToolCall(string Id, string Name, string ArgumentsJson);

/// <summary>Sağlayıcıya giden sohbet isteği: sistem yönergesi, geçmiş ve araçlar.</summary>
public sealed record AiChatRequest(
    string SystemPrompt,
    IReadOnlyList<AiChatMessage> Messages,
    IReadOnlyList<AiToolDefinition> Tools);

/// <summary>
/// AI sağlayıcı soyutlaması. Implementasyonlar (OpenAI uyumlu HTTP, offline)
/// değiştirilebilir olmalıdır. Sağlayıcı, araç çağırma
/// gerektiğinde executeTool temsilcisiyle onaylı aracı çalıştırır ve son yanıt
/// metnini döndürür.
/// </summary>
public interface IAiProvider
{
    string Name { get; }

    Task<string> CompleteAsync(
        AiChatRequest request,
        Func<AiToolCall, CancellationToken, Task<string>> executeTool,
        CancellationToken cancellationToken);
}
