namespace Accounting.Domain.Enums;

/// <summary>Gelir/gider kategorisi ve kaydının yönü.</summary>
public enum IncomeExpenseType
{
    Income = 1,
    Expense = 2,
}

/// <summary>
/// Gelir/gider kaydının durumu. Kayıtlar değiştirilemez/silinemez; hatalı kayıt
/// iptal edilir — iptal kasa hareketinin tersini yazar.
/// </summary>
public enum IncomeExpenseStatus
{
    Active = 1,
    Cancelled = 2,
}
