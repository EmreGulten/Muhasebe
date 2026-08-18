using Accounting.Contracts.Products;
using Accounting.Domain.Enums;
using FluentValidation;

namespace Accounting.Application.Validators;

/// <summary>Miktar alanları numeric(18,4) olduğundan en fazla 4 basamak ondalık kabul edilir.</summary>
internal static class QuantityRules
{
    public static bool HasValidScale(decimal value) => value == decimal.Round(value, 4);

    public const string ScaleMessage = "Miktar en fazla 4 basamak ondalık içerebilir (örn. 2,5 kg).";
}

/// <summary>Ürün/hizmet kartı oluşturma doğrulaması.</summary>
public sealed class CreateProductValidator : AbstractValidator<CreateProductRequest>
{
    public CreateProductValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Ürün adı gereklidir.")
            .MinimumLength(2).WithMessage("Ürün adı en az 2 karakter olmalı.")
            .MaximumLength(200).WithMessage("Ürün adı en fazla 200 karakter olabilir.");

        RuleFor(x => x.Sku).MaximumLength(50).WithMessage("Stok kodu en fazla 50 karakter olabilir.");
        RuleFor(x => x.Barcode).MaximumLength(50).WithMessage("Barkod en fazla 50 karakter olabilir.");
        RuleFor(x => x.Description).MaximumLength(500);

        RuleFor(x => x.PurchasePrice)
            .GreaterThanOrEqualTo(0).WithMessage("Alış fiyatı negatif olamaz.")
            .Must(MoneyRules.HasValidScale).WithMessage(MoneyRules.ScaleMessage);

        RuleFor(x => x.SalePrice)
            .GreaterThanOrEqualTo(0).WithMessage("Satış fiyatı negatif olamaz.")
            .Must(MoneyRules.HasValidScale).WithMessage(MoneyRules.ScaleMessage);

        RuleFor(x => x.VatRate)
            .InclusiveBetween(0, 100).WithMessage("KDV oranı 0 ile 100 arasında olmalı.")
            .Must(MoneyRules.HasValidScale).WithMessage(MoneyRules.ScaleMessage);

        RuleFor(x => x.MinimumStock)
            .GreaterThanOrEqualTo(0).WithMessage("Kritik stok eşiği negatif olamaz.")
            .Must(QuantityRules.HasValidScale).WithMessage(QuantityRules.ScaleMessage);
    }
}

/// <summary>Ürün güncelleme doğrulaması (aynı kurallar + aktiflik).</summary>
public sealed class UpdateProductValidator : AbstractValidator<UpdateProductRequest>
{
    public UpdateProductValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Ürün adı gereklidir.")
            .MinimumLength(2).WithMessage("Ürün adı en az 2 karakter olmalı.")
            .MaximumLength(200).WithMessage("Ürün adı en fazla 200 karakter olabilir.");

        RuleFor(x => x.Sku).MaximumLength(50);
        RuleFor(x => x.Barcode).MaximumLength(50);
        RuleFor(x => x.Description).MaximumLength(500);

        RuleFor(x => x.PurchasePrice)
            .GreaterThanOrEqualTo(0).WithMessage("Alış fiyatı negatif olamaz.")
            .Must(MoneyRules.HasValidScale).WithMessage(MoneyRules.ScaleMessage);

        RuleFor(x => x.SalePrice)
            .GreaterThanOrEqualTo(0).WithMessage("Satış fiyatı negatif olamaz.")
            .Must(MoneyRules.HasValidScale).WithMessage(MoneyRules.ScaleMessage);

        RuleFor(x => x.VatRate)
            .InclusiveBetween(0, 100).WithMessage("KDV oranı 0 ile 100 arasında olmalı.")
            .Must(MoneyRules.HasValidScale).WithMessage(MoneyRules.ScaleMessage);

        RuleFor(x => x.MinimumStock)
            .GreaterThanOrEqualTo(0).WithMessage("Kritik stok eşiği negatif olamaz.")
            .Must(QuantityRules.HasValidScale).WithMessage(QuantityRules.ScaleMessage);
    }
}

/// <summary>Manuel stok hareketi doğrulaması.</summary>
public sealed class CreateInventoryTransactionValidator : AbstractValidator<CreateInventoryTransactionRequest>
{
    public CreateInventoryTransactionValidator()
    {
        RuleFor(x => x.ProductId).NotEmpty().WithMessage("Ürün seçmelisiniz.");

        RuleFor(x => x.WarehouseId).NotEmpty().WithMessage("Depo seçmelisiniz.");

        RuleFor(x => x.Type)
            .Must(BeValidManualType)
            .WithMessage("Hareket türü geçersiz. Manuel girilebilen türler: Count, ManualIn, ManualOut, Return.");

        RuleFor(x => x.Date).NotEmpty().WithMessage("Hareket tarihi gereklidir.");

        RuleFor(x => x.Quantity)
            .NotEqual(0).WithMessage("Miktar sıfır olamaz.")
            .Must(QuantityRules.HasValidScale).WithMessage(QuantityRules.ScaleMessage);

        RuleFor(x => x.Description).MaximumLength(300);
    }

    private static bool BeValidManualType(string? value) =>
        Enum.TryParse<InventoryTransactionType>(value, ignoreCase: false, out var type)
        && type is InventoryTransactionType.Count
            or InventoryTransactionType.ManualIn
            or InventoryTransactionType.ManualOut
            or InventoryTransactionType.Return;
}

/// <summary>Depolar arası transfer doğrulaması.</summary>
public sealed class CreateInventoryTransferValidator : AbstractValidator<CreateInventoryTransferRequest>
{
    public CreateInventoryTransferValidator()
    {
        RuleFor(x => x.ProductId).NotEmpty().WithMessage("Ürün seçmelisiniz.");

        RuleFor(x => x.SourceWarehouseId).NotEmpty().WithMessage("Kaynak depo seçmelisiniz.");
        RuleFor(x => x.TargetWarehouseId).NotEmpty().WithMessage("Hedef depo seçmelisiniz.");

        RuleFor(x => x.Date).NotEmpty().WithMessage("Transfer tarihi gereklidir.");

        RuleFor(x => x.Quantity)
            .NotEqual(0).WithMessage("Miktar sıfır olamaz.")
            .GreaterThan(0).WithMessage("Transfer miktarı pozitif olmalı.")
            .Must(QuantityRules.HasValidScale).WithMessage(QuantityRules.ScaleMessage);

        RuleFor(x => x.Description).MaximumLength(300);
    }
}

// ---- Tanımlar (kategori / birim / depo)

public sealed class CreateCategoryValidator : AbstractValidator<CreateCategoryRequest>
{
    public CreateCategoryValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Kategori adı gereklidir.")
            .MinimumLength(2).WithMessage("Kategori adı en az 2 karakter olmalı.")
            .MaximumLength(100).WithMessage("Kategori adı en fazla 100 karakter olabilir.");
    }
}

public sealed class UpdateCategoryValidator : AbstractValidator<UpdateCategoryRequest>
{
    public UpdateCategoryValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Kategori adı gereklidir.")
            .MinimumLength(2).WithMessage("Kategori adı en az 2 karakter olmalı.")
            .MaximumLength(100).WithMessage("Kategori adı en fazla 100 karakter olabilir.");
    }
}

public sealed class CreateUnitValidator : AbstractValidator<CreateUnitRequest>
{
    public CreateUnitValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Birim adı gereklidir.")
            .MinimumLength(1).WithMessage("Birim adı en az 1 karakter olmalı.")
            .MaximumLength(50).WithMessage("Birim adı en fazla 50 karakter olabilir.");

        RuleFor(x => x.Code).MaximumLength(10).WithMessage("Birim kısa kodu en fazla 10 karakter olabilir.");
    }
}

public sealed class UpdateUnitValidator : AbstractValidator<UpdateUnitRequest>
{
    public UpdateUnitValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Birim adı gereklidir.")
            .MinimumLength(1).WithMessage("Birim adı en az 1 karakter olmalı.")
            .MaximumLength(50).WithMessage("Birim adı en fazla 50 karakter olabilir.");

        RuleFor(x => x.Code).MaximumLength(10).WithMessage("Birim kısa kodu en fazla 10 karakter olabilir.");
    }
}

public sealed class CreateWarehouseValidator : AbstractValidator<CreateWarehouseRequest>
{
    public CreateWarehouseValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Depo adı gereklidir.")
            .MinimumLength(2).WithMessage("Depo adı en az 2 karakter olmalı.")
            .MaximumLength(100).WithMessage("Depo adı en fazla 100 karakter olabilir.");

        RuleFor(x => x.Address).MaximumLength(300);
    }
}

public sealed class UpdateWarehouseValidator : AbstractValidator<UpdateWarehouseRequest>
{
    public UpdateWarehouseValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Depo adı gereklidir.")
            .MinimumLength(2).WithMessage("Depo adı en az 2 karakter olmalı.")
            .MaximumLength(100).WithMessage("Depo adı en fazla 100 karakter olabilir.");

        RuleFor(x => x.Address).MaximumLength(300);
    }
}
