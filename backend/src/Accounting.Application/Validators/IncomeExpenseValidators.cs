using Accounting.Contracts.IncomeExpenses;
using FluentValidation;

namespace Accounting.Application.Validators;

/// <summary>Gelir/gider kategorisi oluşturma doğrulaması.</summary>
public sealed class CreateIncomeExpenseCategoryValidator : AbstractValidator<CreateIncomeExpenseCategoryRequest>
{
    public CreateIncomeExpenseCategoryValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Kategori adı gereklidir.")
            .MaximumLength(100).WithMessage("Kategori adı en fazla 100 karakter olabilir.");

        RuleFor(x => x.Type)
            .Must(t => t is "Income" or "Expense")
            .WithMessage("Kategori türü geçersiz. Geçerli değerler: Income, Expense.");
    }
}

/// <summary>Kategori düzenleme doğrulaması — tür oluşturulduktan sonra değişmez.</summary>
public sealed class UpdateIncomeExpenseCategoryValidator : AbstractValidator<UpdateIncomeExpenseCategoryRequest>
{
    public UpdateIncomeExpenseCategoryValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Kategori adı gereklidir.")
            .MaximumLength(100).WithMessage("Kategori adı en fazla 100 karakter olabilir.");
    }
}

/// <summary>Gelir/gider kaydı doğrulaması (muhasebe.md bölüm 8).</summary>
public sealed class CreateIncomeExpenseRecordValidator : AbstractValidator<CreateIncomeExpenseRecordRequest>
{
    public CreateIncomeExpenseRecordValidator()
    {
        RuleFor(x => x.Type)
            .Must(t => t is "Income" or "Expense")
            .WithMessage("Kayıt türü geçersiz. Geçerli değerler: Income, Expense.");

        RuleFor(x => x.CategoryId)
            .NotEmpty().WithMessage("Kategori seçmelisiniz.");

        RuleFor(x => x.Amount)
            .GreaterThan(0).WithMessage("Tutar 0'dan büyük olmalı.")
            .Must(MoneyRules.HasValidScale).WithMessage(MoneyRules.ScaleMessage);

        RuleFor(x => x.Date)
            .NotEmpty().WithMessage("Kayıt tarihi gereklidir.");

        RuleFor(x => x.Description).MaximumLength(300);
        RuleFor(x => x.DocumentNumber).MaximumLength(50);
    }
}
