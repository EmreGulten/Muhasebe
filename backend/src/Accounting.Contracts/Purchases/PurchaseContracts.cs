using Accounting.Contracts;

namespace Accounting.Contracts.Purchases;

/// <summary>Alış kalemi — satış kalemiyle aynı hesaplama zinciri.</summary>
public sealed record PurchaseItemRequest(
    Guid ProductId,
    decimal Quantity,
    decimal UnitPrice,
    decimal DiscountRate,
    decimal VatRate);

/// <summary>Yeni alış belgesi. Tedarikçi verilmezse nakit alış olur (cari hareketi yazılmaz).</summary>
public sealed record CreatePurchaseRequest(
    Guid? PartyId,
    Guid? WarehouseId,
    DateTime Date,
    DateTime? DueDate,
    string? Description,
    IReadOnlyList<PurchaseItemRequest> Items);

/// <summary>Taslak düzenleme — kalemler yeniden yazılır (satışla aynı kural).</summary>
public sealed record UpdatePurchaseRequest(
    Guid? PartyId,
    Guid? WarehouseId,
    DateTime Date,
    DateTime? DueDate,
    string? Description,
    IReadOnlyList<PurchaseItemRequest> Items);

/// <summary>Onay anı ödemesi (opsiyonel) — kasa çıkışı + tedarikçi borcu düşümü.</summary>
public sealed record PurchaseConfirmPaymentRequest(
    DateTime Date,
    decimal Amount,
    string? Description);

/// <summary>Alışı onayla: stok girişi + tedarikçi borcu (+ istenirse anlık ödeme).</summary>
public sealed record ConfirmPurchaseRequest(PurchaseConfirmPaymentRequest? Payment);

/// <summary>Alışı iptal et — ters hareketler, terminal durum.</summary>
public sealed record CancelPurchaseRequest(string Reason);

/// <summary>Onaylı alışa sonradan ödeme ekle.</summary>
public sealed record AddPurchasePaymentRequest(
    DateTime Date,
    decimal Amount,
    string? Description);

public sealed record PurchaseItemDto(
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

public sealed record PurchasePaymentDto(
    Guid Id,
    Guid AccountId,
    string AccountName,
    DateTime Date,
    decimal Amount,
    string? Description,
    bool PaidOnConfirm,
    DateTime CreatedAtUtc);

/// <summary>Liste satırı.</summary>
public sealed record PurchaseSummaryDto(
    Guid Id,
    string Number,
    DateTime Date,
    Guid? PartyId,
    string? PartyName,
    int ItemCount,
    decimal Total,
    decimal PaidAmount,
    string Status);

/// <summary>Belge detayı — kalemler ve ödemeler dahil.</summary>
public sealed record PurchaseResponse(
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
    IReadOnlyList<PurchaseItemDto> Items,
    IReadOnlyList<PurchasePaymentDto> Payments);
