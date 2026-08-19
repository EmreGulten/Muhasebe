using Accounting.Contracts.Accounts;
using Accounting.Domain.Enums;
using FluentValidation;

namespace Accounting.Application.Validators;

/// <summary>Hesap oluşturma doğrulaması.</summary>
public sealed class CreateAccountValidator : AbstractValidator<CreateAccountRequest>
{
    public CreateAccountValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Hesap adı gereklidir.")
            .MaximumLength(100).WithMessage("Hesap adı en fazla 100 karakter olabilir.");

        RuleFor(x => x.Type)
            .Must(BeValidType).WithMessage("Hesap türü geçersiz. Geçerli değerler: Cash, Bank, CreditCard, VirtualPOS.");

        RuleFor(x => x.Currency)
            .Must(BeValidCurrency).WithMessage("Para birimi 3 harfli ISO kodu olmalı (örn. TRY).");

        RuleFor(x => x.OpeningBalance)
            .GreaterThanOrEqualTo(0).WithMessage("Açılış bakiyesi negatif olamaz.")
            .Must(MoneyRules.HasValidScale).WithMessage(MoneyRules.ScaleMessage);
    }

    internal static bool BeValidType(string? value) =>
        Enum.TryParse<AccountType>(value, ignoreCase: false, out _);

    // Boş → TRY varsayılır; doluysa tam 3 harf olmalı.
    internal static bool BeValidCurrency(string? value) =>
        string.IsNullOrWhiteSpace(value)
        || (value.Trim().Length == 3 && value.Trim().All(char.IsLetter));
}

/// <summary>Hesap güncelleme doğrulaması (ad + aktiflik).</summary>
public sealed class UpdateAccountValidator : AbstractValidator<UpdateAccountRequest>
{
    public UpdateAccountValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Hesap adı gereklidir.")
            .MaximumLength(100).WithMessage("Hesap adı en fazla 100 karakter olabilir.");
    }
}

/// <summary>Manuel hesap hareketi doğrulaması.</summary>
public sealed class CreateAccountTransactionValidator : AbstractValidator<CreateAccountTransactionRequest>
{
    public CreateAccountTransactionValidator()
    {
        RuleFor(x => x.Direction)
            .Must(d => d is "In" or "Out")
            .WithMessage("Hareket yönü geçersiz. Geçerli değerler: In, Out.");

        RuleFor(x => x.Date)
            .NotEmpty().WithMessage("Hareket tarihi gereklidir.");

        RuleFor(x => x.Amount)
            .GreaterThan(0).WithMessage("Hareket tutarı 0'dan büyük olmalı.")
            .Must(MoneyRules.HasValidScale).WithMessage(MoneyRules.ScaleMessage);

        RuleFor(x => x.Description).MaximumLength(300);
    }
}

/// <summary>Hesaplar arası transfer doğrulaması.</summary>
public sealed class TransferValidator : AbstractValidator<TransferRequest>
{
    public TransferValidator()
    {
        RuleFor(x => x.FromAccountId)
            .NotEmpty().WithMessage("Kaynak hesap gereklidir.");

        RuleFor(x => x.ToAccountId)
            .NotEmpty().WithMessage("Hedef hesap gereklidir.");

        RuleFor(x => x.Date)
            .NotEmpty().WithMessage("Transfer tarihi gereklidir.");

        RuleFor(x => x.Amount)
            .GreaterThan(0).WithMessage("Transfer tutarı 0'dan büyük olmalı.")
            .Must(MoneyRules.HasValidScale).WithMessage(MoneyRules.ScaleMessage);

        RuleFor(x => x.Description).MaximumLength(300);
    }
}
