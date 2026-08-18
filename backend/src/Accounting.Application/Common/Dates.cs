namespace Accounting.Application.Common;

/// <summary>İstemciden gelen takvim tarihi alanlarının normalize edilmesi.</summary>
public static class Dates
{
    /// <summary>
    /// Tarihi UTC gece yarısına sabitler. Npgsql yalnızca Kind=Utc değerleri
    /// timestamptz kolonlara yazar; JSON'daki düz tarih ("2026-08-19") Kind=Unspecified
    /// ayrıştırılır ve kaydı 500 ile düşürür. Gün-bazlı muhasebe alanlarında saat
    /// bilgisi bilinçli olarak atılır.
    /// </summary>
    public static DateTime ToUtcDate(DateTime value) => DateTime.SpecifyKind(value.Date, DateTimeKind.Utc);
}
