using Accounting.Contracts.Purchases;
using FluentValidation;

namespace Accounting.Application.Validators;

/// <summary>Alış belgesi oluşturma doğrulaması.</summary>
public sealed class CreatePurchaseValidator : AbstractValidator<CreatePurchaseRequest>
{
    public CreatePurchaseValidator()
    {
        RuleFor(x => x.Date).NotEmpty().WithMessage("Alış tarihi gereklidir.");
        RuleFor(x => x.Description).MaximumLength(500);

        RuleFor(x => x.Items)
            .NotEmpty().WithMessage("Alış en az bir kalem içermelidir.");

        RuleForEach(x => x.Items).ChildRules(item =>
        {
            item.RuleFor(i => i.ProductId).NotEmpty().WithMessage("Kalem ürünü gereklidir.");
            item.RuleFor(i => i.Quantity)
                .GreaterThan(0).WithMessage("Alış miktarı pozitif olmalıdır.")
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
            .WithMessage("Vade tarihi alış tarihinden önce olamaz.");
    }
}

/// <summary>Taslak düzenleme doğrulaması — create ile aynı şema.</summary>
public sealed class UpdatePurchaseValidator : AbstractValidator<UpdatePurchaseRequest>
{
    public UpdatePurchaseValidator()
    {
        RuleFor(x => x.Date).NotEmpty().WithMessage("Alış tarihi gereklidir.");
        RuleFor(x => x.Description).MaximumLength(500);

        RuleFor(x => x.Items)
            .NotEmpty().WithMessage("Alış en az bir kalem içermelidir.");

        RuleForEach(x => x.Items).ChildRules(item =>
        {
            item.RuleFor(i => i.ProductId).NotEmpty().WithMessage("Kalem ürünü gereklidir.");
            item.RuleFor(i => i.Quantity)
                .GreaterThan(0).WithMessage("Alış miktarı pozitif olmalıdır.")
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
            .WithMessage("Vade tarihi alış tarihinden önce olamaz.");
    }
}

/// <summary>Alış onayı — istenirse anlık ödeme.</summary>
public sealed class ConfirmPurchaseValidator : AbstractValidator<ConfirmPurchaseRequest>
{
    public ConfirmPurchaseValidator()
    {
        RuleFor(x => x.Payment!.Date).NotEmpty().WithMessage("Ödeme tarihi gereklidir.")
            .When(x => x.Payment is not null);
        RuleFor(x => x.Payment!.Amount)
            .GreaterThan(0).WithMessage("Ödeme tutarı pozitif olmalıdır.")
            .Must(MoneyRules.HasValidScale).WithMessage(MoneyRules.ScaleMessage)
            .When(x => x.Payment is not null);
        RuleFor(x => x.Payment!.Description).MaximumLength(300)
            .When(x => x.Payment is not null);
    }
}

/// <summary>İptal — gerekçe zorunlu.</summary>
public sealed class CancelPurchaseValidator : AbstractValidator<CancelPurchaseRequest>
{
    public CancelPurchaseValidator()
    {
        RuleFor(x => x.Reason)
            .NotEmpty().WithMessage("İptal gerekçesi gereklidir.")
            .MaximumLength(300).WithMessage("İptal gerekçesi en fazla 300 karakter olabilir.");
    }
}

/// <summary>Sonradan ödeme.</summary>
public sealed class AddPurchasePaymentValidator : AbstractValidator<AddPurchasePaymentRequest>
{
    public AddPurchasePaymentValidator()
    {
        RuleFor(x => x.Date).NotEmpty().WithMessage("Ödeme tarihi gereklidir.");
        RuleFor(x => x.Amount)
            .GreaterThan(0).WithMessage("Ödeme tutarı pozitif olmalıdır.")
            .Must(MoneyRules.HasValidScale).WithMessage(MoneyRules.ScaleMessage);
        RuleFor(x => x.Description).MaximumLength(300);
    }
}
