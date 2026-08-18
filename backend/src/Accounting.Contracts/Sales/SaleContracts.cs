using Accounting.Contracts;

namespace Accounting.Contracts.Sales;

// ---- İstekler

/// <summary>Satış kalemi. Fiyat KDV hariç; iskonto ve KDV oranı yüzde (0–100).</summary>
public sealed record SaleItemRequest(Guid ProductId, decimal Quantity, decimal UnitPrice, decimal DiscountRate, decimal VatRate);

/// <summary>Yeni satış belgesi (Draft olarak oluşur). WarehouseId verilmezse varsayılan depo kullanılır.</summary>
public sealed record CreateSaleRequest(
    Guid? PartyId,
    Guid? WarehouseId,
    DateTime Date,
    DateTime? DueDate,
    string? Description,
    IReadOnlyList<SaleItemRequest> Items);

public sealed record UpdateSaleRequest(
    Guid? PartyId,
    Guid? WarehouseId,
    DateTime Date,
    DateTime? DueDate,
    string? Description,
    IReadOnlyList<SaleItemRequest> Items);

/// <summary>Onay anında girilebilecek tahsilat (opsiyonel).</summary>
public sealed record ConfirmPaymentRequest(DateTime Date, decimal Amount, string? Description);

public sealed record ConfirmSaleRequest(ConfirmPaymentRequest? Payment);

public sealed record CancelSaleRequest(string Reason);

public sealed record AddSalePaymentRequest(DateTime Date, decimal Amount, string? Description);

// ---- Yanıtlar

public sealed record SaleItemDto(
    Guid Id,
    Guid ProductId,
    string ProductName,
    decimal Quantity,
    decimal UnitPrice,
    decimal DiscountRate,
    decimal NetAmount,
    decimal VatRate,
    decimal VatAmount,
    decimal LineTotal);

public sealed record SalePaymentDto(
    Guid Id,
    Guid AccountId,
    string AccountName,
    DateTime Date,
    decimal Amount,
    string? Description,
    bool PaidOnConfirm,
    DateTime CreatedAtUtc);

/// <summary>Özet satırı — satış listesi.</summary>
public sealed record SaleSummaryDto(
    Guid Id,
    string Number,
    DateTime Date,
    Guid? PartyId,
    string? PartyName,
    int ItemCount,
    decimal Total,
    decimal PaidAmount,
    string Status);

/// <summary>Satış belgesi detayı. DueAmount = Total − PaidAmount (kalan borç).</summary>
public sealed record SaleResponse(
    Guid Id,
    string Number,
    Guid? PartyId,
    string? PartyName,
    Guid WarehouseId,
    string WarehouseName,
    DateTime Date,
    DateTime? DueDate,
    string? Description,
    decimal SubTotal,
    decimal DiscountTotal,
    decimal VatTotal,
    decimal Total,
    decimal PaidAmount,
    decimal DueAmount,
    string Status,
    DateTime? ConfirmedAtUtc,
    DateTime? CancelledAtUtc,
    string? CancelReason,
    DateTime CreatedAtUtc,
    IReadOnlyList<SaleItemDto> Items,
    IReadOnlyList<SalePaymentDto> Payments);
