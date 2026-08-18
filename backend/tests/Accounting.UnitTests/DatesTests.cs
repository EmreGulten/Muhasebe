using Accounting.Application.Common;

namespace Accounting.UnitTests;

/// <summary>Tarih normalizasyonu: Npgsql timestamptz Kind=Utc ister (Common/Dates XML doc'u).</summary>
public class DatesTests
{
    [Fact]
    public void ToUtcDate_DuzTarihi_UtcGeceYarisinaCevirir()
    {
        var unspecified = new DateTime(2026, 8, 19, 21, 45, 0, DateTimeKind.Unspecified);

        var normalized = Dates.ToUtcDate(unspecified);

        Assert.Equal(new DateTime(2026, 8, 19, 0, 0, 0, DateTimeKind.Utc), normalized);
        Assert.Equal(DateTimeKind.Utc, normalized.Kind);
    }

    [Fact]
    public void ToUtcDate_UtcTarihi_SadeceGeceYarisinaIndirger()
    {
        var utc = new DateTime(2026, 8, 19, 5, 0, 0, DateTimeKind.Utc);

        var normalized = Dates.ToUtcDate(utc);

        Assert.Equal(new DateTime(2026, 8, 19, 0, 0, 0, DateTimeKind.Utc), normalized);
    }
}
