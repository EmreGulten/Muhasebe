using System.Globalization;
using System.Text.Json;
using Accounting.Application.Abstractions;

namespace Accounting.Application.Features.Assistant;

/// <summary>
/// AI'ın çağırabileceği onaylı iş aracı. Araçlar salt
/// okunurdur, daima TenantId filtresiyle çalışır ve yapılandırılmış JSON +
/// Türkçe bir "summary" döndürür. AI yalnızca bu kayıt defterindeki araçlara
/// ulaşabilir — SQL'e hiçbir biçimde dokunmaz.
/// </summary>
public interface IAiTool
{
    /// <summary>Araç adı — sağlayıcıya tanıtılan çağrı anahtarı.</summary>
    string Name { get; }

    /// <summary>Modelin aracı seçmesini sağlayan açıklama (Türkçe).</summary>
    string Description { get; }

    /// <summary>OpenAI işlev şeması (JSON Schema).</summary>
    string ParametersJsonSchema { get; }

    Task<JsonElement> ExecuteAsync(Guid tenantId, JsonElement arguments, CancellationToken cancellationToken);
}

internal static class AiToolHelpers
{
    public static readonly CultureInfo Turkish = new("tr-TR");

    /// <summary>"1.234,56 TL" biçimi — sağlayıcıdan bağımsız sabit biçim.</summary>
    public static string Money(decimal value) => $"{value.ToString("N2", Turkish)} TL";

    /// <summary>"Ağustos 2026" gibi ay etiketi.</summary>
    public static string MonthLabel(int year, int month) =>
        new DateTime(year, month, 1).ToString("MMMM yyyy", Turkish);

    /// <summary>YYYY-MM metnini çözümler; geçersizse null.</summary>
    public static (int Year, int Month)? ParseMonth(string? value)
    {
        if (value is null
            || value.Length != 7
            || value[4] != '-'
            || !int.TryParse(value.AsSpan(0, 4), out var year)
            || !int.TryParse(value.AsSpan(5, 2), out var month)
            || month is < 1 or > 12)
        {
            return null;
        }

        return (year, month);
    }

    /// <summary>Önceki değere göre yüzdesel değişim cümlesi.</summary>
    public static string Change(decimal previous, decimal current)
    {
        if (previous == 0m)
        {
            return current == 0m ? "değişmedi" : "geçen ay sıfırdı";
        }

        var percent = Math.Round((current - previous) / previous * 100m, 0);
        return percent == 0m ? "değişmedi" : $"%{Math.Abs(percent).ToString("0", Turkish)} {(percent > 0 ? "arttı" : "azaldı")}";
    }

    public static bool TryGetString(this JsonElement arguments, string name, out string value)
    {
        if (arguments.ValueKind == JsonValueKind.Object
            && arguments.TryGetProperty(name, out var element)
            && element.ValueKind == JsonValueKind.String
            && !string.IsNullOrWhiteSpace(element.GetString()))
        {
            value = element.GetString()!;
            return true;
        }

        value = string.Empty;
        return false;
    }

    public static bool TryGetInt32(this JsonElement arguments, string name, out int value)
    {
        if (arguments.ValueKind == JsonValueKind.Object
            && arguments.TryGetProperty(name, out var element)
            && element.ValueKind == JsonValueKind.Number
            && element.TryGetInt32(out value))
        {
            return true;
        }

        value = 0;
        return false;
    }

    /// <summary>Nesneyi araç sonucu JSON'una çevirir.</summary>
    public static JsonElement ToJson<T>(T value) =>
        JsonSerializer.SerializeToElement(value);
}
