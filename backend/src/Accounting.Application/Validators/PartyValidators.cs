using Accounting.Contracts.Parties;
using Accounting.Domain.Enums;
using FluentValidation;

namespace Accounting.Application.Validators;

/// <summary>Para alanları numeric(18,2) olduğundan en fazla 2 basamak ondalık kabul edilir.</summary>
internal static class MoneyRules
{
    public static bool HasValidScale(decimal value) => value == decimal.Round(value, 2);

    public const string ScaleMessage = "Tutar en fazla 2 basamak ondalık içerebilir (örn. 1250,50).";
}

/// <summary>Cari kartı oluşturma doğrulaması.</summary>
public sealed class CreatePartyValidator : AbstractValidator<CreatePartyRequest>
{
    public CreatePartyValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Cari adı gereklidir.")
            .MinimumLength(2).WithMessage("Cari adı en az 2 karakter olmalı.")
            .MaximumLength(200).WithMessage("Cari adı en fazla 200 karakter olabilir.");

        RuleFor(x => x.Type)
            .Must(BeValidType).WithMessage("Cari türü geçersiz. Geçerli değerler: Customer, Supplier, Both.");

        RuleFor(x => x.TaxNumber)
            .MaximumLength(20).WithMessage("Vergi/TCKN numarası en fazla 20 karakter olabilir.");

        RuleFor(x => x.TaxOffice)
            .MaximumLength(60).WithMessage("Vergi dairesi en fazla 60 karakter olabilir.");

        RuleFor(x => x.Phone)
            .MaximumLength(30).WithMessage("Telefon en fazla 30 karakter olabilir.");

        RuleFor(x => x.Email)
            .EmailAddress().WithMessage("Geçerli bir e-posta adresi girin.")
            .MaximumLength(150).WithMessage("E-posta en fazla 150 karakter olabilir.");

        RuleFor(x => x.Address)
            .MaximumLength(300).WithMessage("Adres en fazla 300 karakter olabilir.");

        RuleFor(x => x.City).MaximumLength(60);
        RuleFor(x => x.District).MaximumLength(60);
        RuleFor(x => x.ContactName).MaximumLength(120);

        RuleFor(x => x.OpeningBalance)
            .Must(MoneyRules.HasValidScale).WithMessage(MoneyRules.ScaleMessage);

        RuleFor(x => x.CreditLimit)
            .GreaterThanOrEqualTo(0).WithMessage("Kredi limiti negatif olamaz.")
            .Must(MoneyRules.HasValidScale).WithMessage(MoneyRules.ScaleMessage);

        RuleFor(x => x.Notes).MaximumLength(1000);
    }

    internal static bool BeValidType(string? value) =>
        Enum.TryParse<PartyType>(value, ignoreCase: false, out _);
}

/// <summary>Cari kartı güncelleme doğrulaması (açılış bakiyesi hariç).</summary>
public sealed class UpdatePartyValidator : AbstractValidator<UpdatePartyRequest>
{
    public UpdatePartyValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Cari adı gereklidir.")
            .MinimumLength(2).WithMessage("Cari adı en az 2 karakter olmalı.")
            .MaximumLength(200).WithMessage("Cari adı en fazla 200 karakter olabilir.");

        RuleFor(x => x.Type)
            .Must(CreatePartyValidator.BeValidType).WithMessage("Cari türü geçersiz. Geçerli değerler: Customer, Supplier, Both.");

        RuleFor(x => x.TaxNumber).MaximumLength(20);
        RuleFor(x => x.TaxOffice).MaximumLength(60);
        RuleFor(x => x.Phone).MaximumLength(30);

        RuleFor(x => x.Email)
            .EmailAddress().WithMessage("Geçerli bir e-posta adresi girin.")
            .MaximumLength(150);

        RuleFor(x => x.Address).MaximumLength(300);
        RuleFor(x => x.City).MaximumLength(60);
        RuleFor(x => x.District).MaximumLength(60);
        RuleFor(x => x.ContactName).MaximumLength(120);

        RuleFor(x => x.CreditLimit)
            .GreaterThanOrEqualTo(0).WithMessage("Kredi limiti negatif olamaz.")
            .Must(MoneyRules.HasValidScale).WithMessage(MoneyRules.ScaleMessage);

        RuleFor(x => x.Notes).MaximumLength(1000);
    }
}

/// <summary>Manuel cari hareketi doğrulaması.</summary>
public sealed class CreatePartyTransactionValidator : AbstractValidator<CreatePartyTransactionRequest>
{
    public CreatePartyTransactionValidator()
    {
        RuleFor(x => x.Type)
            .Must(BeValidManualType)
            .WithMessage("Hareket türü geçersiz. Manuel girilebilen türler: OpeningBalance, Debit, Credit, Adjustment.");

        RuleFor(x => x.Date)
            .NotEmpty().WithMessage("Hareket tarihi gereklidir.");

        RuleFor(x => x.Amount)
            .NotEqual(0).WithMessage("Hareket tutarı sıfır olamaz.")
            .Must(MoneyRules.HasValidScale).WithMessage(MoneyRules.ScaleMessage);

        RuleFor(x => x.Description).MaximumLength(300);
    }

    private static bool BeValidManualType(string? value) =>
        Enum.TryParse<PartyTransactionType>(value, ignoreCase: false, out var type)
        && type is PartyTransactionType.OpeningBalance
            or PartyTransactionType.Debit
            or PartyTransactionType.Credit
            or PartyTransactionType.Adjustment;
}
