using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Accounting.Application.Abstractions;

namespace Accounting.Infrastructure.Ai;

/// <summary>
/// OpenAI uyumlu chat completions sağlayıcısı. Sağlayıcı implementasyonu
/// yapılandırma üzerinden değiştirilebilir. Tool calling döngüsü burada döner:
/// model araç isterse onaylı araç temsilcisi çağrılır, sonuç "tool" mesajı
/// olarak geri verilir ve model son yanıtı üretir. Model hiçbir koşulda
/// SQL'e dokunmaz; yalnızca istekte tanıtılan araçları çağırabilir.
/// </summary>
public sealed class OpenAiProvider(HttpClient httpClient, AiOptions options) : IAiProvider
{
    public string Name => "openai";

    /// <summary>Guard: aracı istismar eden model sonsuz döngüye giremez.</summary>
    private const int MaxToolRounds = 4;

    public async Task<string> CompleteAsync(
        AiChatRequest request,
        Func<AiToolCall, CancellationToken, Task<string>> executeTool,
        CancellationToken cancellationToken)
    {
        var messages = new JsonArray
        {
            new JsonObject { ["role"] = "system", ["content"] = request.SystemPrompt },
        };
        foreach (var message in request.Messages)
        {
            messages.Add(new JsonObject { ["role"] = message.Role, ["content"] = message.Content });
        }

        var tools = new JsonArray();
        foreach (var tool in request.Tools)
        {
            tools.Add(new JsonObject
            {
                ["type"] = "function",
                ["function"] = new JsonObject
                {
                    ["name"] = tool.Name,
                    ["description"] = tool.Description,
                    ["parameters"] = JsonNode.Parse(tool.ParametersJsonSchema),
                },
            });
        }

        for (var round = 0; round < MaxToolRounds; round++)
        {
            var payload = new JsonObject
            {
                ["model"] = options.Model,
                ["messages"] = messages.DeepClone(),
                ["tools"] = tools.DeepClone(),
                ["tool_choice"] = "auto",
            };

            using var response = await httpClient.PostAsJsonAsync("chat/completions", payload, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                throw new InvalidOperationException(
                    $"AI sağlayıcısı {(int)response.StatusCode} döndürdü: {await response.Content.ReadAsStringAsync(cancellationToken)}");
            }

            using var document = await response.Content.ReadFromJsonAsync<JsonDocument>(cancellationToken: cancellationToken);
            var message = document!.RootElement
                .GetProperty("choices")[0]
                .GetProperty("message");

            var hasToolCalls = message.TryGetProperty("tool_calls", out var toolCalls)
                && toolCalls.ValueKind == JsonValueKind.Array;
            if (!hasToolCalls || toolCalls.GetArrayLength() == 0)
            {
                return message.TryGetProperty("content", out var content) && content.ValueKind == JsonValueKind.String
                    ? content.GetString() ?? string.Empty
                    : string.Empty;
            }

            // Asistanın araç istek mesajı olduğu gibi geçmişe eklenir (tool_call_id
            // bağını korumak için), ardından her çağrının sonucu "tool" mesajıdır.
            messages.Add(JsonNode.Parse(message.GetRawText()));

            foreach (var call in toolCalls.EnumerateArray())
            {
                var id = call.GetProperty("id").GetString() ?? string.Empty;
                var function = call.GetProperty("function");
                var name = function.GetProperty("name").GetString() ?? string.Empty;
                var arguments = function.TryGetProperty("arguments", out var args) && args.ValueKind == JsonValueKind.String
                    ? args.GetString() ?? "{}"
                    : "{}";

                var result = await executeTool(new AiToolCall(id, name, arguments), cancellationToken);
                messages.Add(new JsonObject
                {
                    ["role"] = "tool",
                    ["tool_call_id"] = id,
                    ["content"] = result,
                });
            }
        }

        throw new InvalidOperationException("AI sağlayıcısı araç çağrılarını sonuçlandıramadı.");
    }
}
