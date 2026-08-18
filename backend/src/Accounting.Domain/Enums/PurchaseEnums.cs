namespace Accounting.Domain.Enums;

/// <summary>Alış belgesi durum makinesi — satışla aynı akış (muhasebe.md bölüm 23).</summary>
public enum PurchaseStatus
{
    Draft = 1,
    Confirmed = 2,
    PartiallyPaid = 3,
    Paid = 4,
    Cancelled = 5,
}
