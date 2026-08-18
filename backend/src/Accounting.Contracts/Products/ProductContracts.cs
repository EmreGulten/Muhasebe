namespace Accounting.Contracts.Products;

// ---- Kategori

public sealed record CreateCategoryRequest(string Name);

public sealed record UpdateCategoryRequest(string Name);

/// <summary>Liste satırı — ürün sayısı dahil.</summary>
public sealed record CategoryDto(Guid Id, string Name, int ProductCount);

// ---- Birim

public sealed record CreateUnitRequest(string Name, string? Code);

public sealed record UpdateUnitRequest(string Name, string? Code);

public sealed record UnitDto(Guid Id, string Name, string? Code, int ProductCount);

// ---- Depo

public sealed record CreateWarehouseRequest(string Name, string? Address, bool IsDefault);

public sealed record UpdateWarehouseRequest(string Name, string? Address, bool IsActive, bool IsDefault);

public sealed record WarehouseDto(Guid Id, string Name, string? Address, bool IsDefault, bool IsActive);

/// <summary>Yeni ürün/hizmet kartı. Hizmetler IsService=true ile stok takibi yapmaz.</summary>
public sealed record CreateProductRequest(
    string Name,
    string? Sku,
    string? Barcode,
    string? Description,
    Guid? CategoryId,
    Guid? UnitId,
    decimal PurchasePrice,
    decimal SalePrice,
    decimal VatRate,
    decimal MinimumStock,
    bool IsService);

/// <summary>Ürün güncelleme. Stok hareketleri ve kritik eşik dahil alanlar güncellenebilir.</summary>
public sealed record UpdateProductRequest(
    string Name,
    string? Sku,
    string? Barcode,
    string? Description,
    Guid? CategoryId,
    Guid? UnitId,
    decimal PurchasePrice,
    decimal SalePrice,
    decimal VatRate,
    decimal MinimumStock,
    bool IsService,
    bool IsActive);

/// <summary>Ürün listesi satırı — güncel stok ve kritik durumu dahil.</summary>
public sealed record ProductSummaryDto(
    Guid Id,
    string Name,
    string? Sku,
    string? Barcode,
    string? CategoryName,
    string? UnitName,
    decimal SalePrice,
    decimal VatRate,
    decimal CurrentStock,
    bool IsCritical,
    bool IsService,
    bool IsActive);

/// <summary>Ürün detayı + stok özeti.</summary>
public sealed record ProductResponse(
    Guid Id,
    string Name,
    string? Sku,
    string? Barcode,
    string? Description,
    Guid? CategoryId,
    string? CategoryName,
    Guid? UnitId,
    string? UnitName,
    decimal PurchasePrice,
    decimal SalePrice,
    decimal VatRate,
    decimal MinimumStock,
    bool IsService,
    bool IsActive,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc,
    decimal CurrentStock,
    bool IsCritical);

/// <summary>Depo bazında stok dökümü.</summary>
public sealed record WarehouseStockDto(Guid WarehouseId, string WarehouseName, decimal Stock);

public sealed record ProductStockResponse(Guid ProductId, string ProductName, decimal TotalStock, IReadOnlyList<WarehouseStockDto> Warehouses);

// ---- Stok hareketleri

/// <summary>
/// Manuel stok hareketi. Type: "Count" | "ManualIn" | "ManualOut" | "Return".
/// Quantity işaretlidir (pozitif giriş, negatif çıkış); Count türünde girilen
/// değer sayım sonucudur, sunucu fark hareketini (sayılan − güncel stok) yazar.
/// </summary>
public sealed record CreateInventoryTransactionRequest(
    Guid ProductId,
    Guid WarehouseId,
    string Type,
    DateTime Date,
    decimal Quantity,
    string? Description);

/// <summary>Depolar arası transfer — kaynak çıkış + hedef giriş çifti tek işlemde.</summary>
public sealed record CreateInventoryTransferRequest(
    Guid ProductId,
    Guid SourceWarehouseId,
    Guid TargetWarehouseId,
    DateTime Date,
    decimal Quantity,
    string? Description);

public sealed record InventoryTransactionDto(
    Guid Id,
    Guid ProductId,
    string ProductName,
    Guid WarehouseId,
    string WarehouseName,
    string Type,
    DateTime Date,
    decimal Quantity,
    string? Description,
    string? ReferenceType,
    Guid? ReferenceId,
    DateTime CreatedAtUtc);

/// <summary>Kritik stok satırı: güncel stok eşik değerinin altında.</summary>
public sealed record CriticalStockItemDto(
    Guid ProductId,
    string Name,
    string? Sku,
    decimal CurrentStock,
    decimal MinimumStock,
    string? UnitName);
