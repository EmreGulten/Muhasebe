using Accounting.Contracts.Sales;
using FluentValidation;

namespace Accounting.Application.Validators;

/// <summary>Satış belgesi oluşturma doğrulaması.</summary>
public sealed class CreateSaleValidator : AbstractValidator<CreateSaleRequest>
{
    public CreateSaleValidator()
    {
        RuleFor(x => x.Date).NotEmpty().WithMessage("Satış tarihi gereklidir.");
        RuleFor(x => x.Description).MaximumLength(500);

        RuleFor(x => x.Items)
            .NotEmpty().WithMessage("Satış en az bir kalem içermelidir.");

        RuleForEach(x => x.Items).ChildRules(item =>
        {
            item.RuleFor(i => i.ProductId).NotEmpty().WithMessage("Kalem ürünü gereklidir.");
            item.RuleFor(i => i.Quantity)
                .GreaterThan(0).WithMessage("Satış miktarı pozitif olmalıdır.")
                .Must(QuantityRules.HasValidScale).WithMessage(QuantityRules.ScaleMessage);
            item.RuleFor(i => i.UnitPrice)
                .GreaterThanOrEqualTo(0).WithMessage("Birim fiyat negatif olamaz.")
                .Must(MoneyRules.HasValidScale).WithMessage(MoneyRules.ScaleMessage);
            item.RuleFor(i => i.DiscountRate)
                .InclusiveBetween(0, 100).WithMessage("İskonto oranı 0 ile 100 arasında olmalıdır.")
                .Must(MoneyRules.HasValidScale).WithMessage(MoneyRules.ScaleMessage);
            item.RuleFor(i => i.VatRate)
                .InclusiveBetween(0, 100).WithMessage("KDV oranı 0 ile 100 arasında olmalıdır.")
                .Must(MoneyRules.HasValidScale).WithMessage(MoneyRules.ScaleMessage);
        });

        RuleFor(x => x)
            .Must(x => x.DueDate is null || x.DueDate.Value.Date >= x.Date.Date)
            .WithMessage("Vade tarihi satış tarihinden önce olamaz.");
    }
}

/// <summary>Taslak düzenleme doğrulaması — create ile aynı şema.</summary>
public sealed class UpdateSaleValidator : AbstractValidator<UpdateSaleRequest>
{
    public UpdateSaleValidator()
    {
        RuleFor(x => x.Date).NotEmpty().WithMessage("Satış tarihi gereklidir.");
        RuleFor(x => x.Description).MaximumLength(500);

        RuleFor(x => x.Items)
            .NotEmpty().WithMessage("Satış en az bir kalem içermelidir.");

        RuleForEach(x => x.Items).ChildRules(item =>
        {
            item.RuleFor(i => i.ProductId).NotEmpty().WithMessage("Kalem ürünü gereklidir.");
            item.RuleFor(i => i.Quantity)
                .GreaterThan(0).WithMessage("Satış miktarı pozitif olmalıdır.")
                .Must(QuantityRules.HasValidScale).WithMessage(QuantityRules.ScaleMessage);
            item.RuleFor(i => i.UnitPrice)
                .GreaterThanOrEqualTo(0).WithMessage("Birim fiyat negatif olamaz.")
                .Must(MoneyRules.HasValidScale).WithMessage(MoneyRules.ScaleMessage);
            item.RuleFor(i => i.DiscountRate)
                .InclusiveBetween(0, 100).WithMessage("İskonto oranı 0 ile 100 arasında olmalıdır.")
                .Must(MoneyRules.HasValidScale).WithMessage(MoneyRules.ScaleMessage);
            item.RuleFor(i => i.VatRate)
                .InclusiveBetween(0, 100).WithMessage("KDV oranı 0 ile 100 arasında olmalıdır.")
                .Must(MoneyRules.HasValidScale).WithMessage(MoneyRules.ScaleMessage);
        });

        RuleFor(x => x)
            .Must(x => x.DueDate is null || x.DueDate.Value.Date >= x.Date.Date)
            .WithMessage("Vade tarihi satış tarihinden önce olamaz.");
    }
}

/// <summary>Satış onayı — istenirse anlık tahsilat.</summary>
public sealed class ConfirmSaleValidator : AbstractValidator<ConfirmSaleRequest>
{
    public ConfirmSaleValidator()
    {
        RuleFor(x => x.Payment!.Date).NotEmpty().WithMessage("Tahsilat tarihi gereklidir.")
            .When(x => x.Payment is not null);
        RuleFor(x => x.Payment!.Amount)
            .GreaterThan(0).WithMessage("Tahsilat tutarı pozitif olmalıdır.")
            .Must(MoneyRules.HasValidScale).WithMessage(MoneyRules.ScaleMessage)
            .When(x => x.Payment is not null);
        RuleFor(x => x.Payment!.Description).MaximumLength(300)
            .When(x => x.Payment is not null);
    }
}

/// <summary>İptal — gerekçe zorunlu.</summary>
public sealed class CancelSaleValidator : AbstractValidator<CancelSaleRequest>
{
    public CancelSaleValidator()
    {
        RuleFor(x => x.Reason)
            .NotEmpty().WithMessage("İptal gerekçesi gereklidir.")
            .MaximumLength(300).WithMessage("İptal gerekçesi en fazla 300 karakter olabilir.");
    }
}

/// <summary>Sonradan tahsilat.</summary>
public sealed class AddSalePaymentValidator : AbstractValidator<AddSalePaymentRequest>
{
    public AddSalePaymentValidator()
    {
        RuleFor(x => x.Date).NotEmpty().WithMessage("Tahsilat tarihi gereklidir.");
        RuleFor(x => x.Amount)
            .GreaterThan(0).WithMessage("Tahsilat tutarı pozitif olmalıdır.")
            .Must(MoneyRules.HasValidScale).WithMessage(MoneyRules.ScaleMessage);
        RuleFor(x => x.Description).MaximumLength(300);
    }
}
