using Accounting.Contracts.Tenants;
using FluentValidation;

namespace Accounting.Application.Validators;

public sealed class CreateTenantValidator : AbstractValidator<CreateTenantRequest>
{
    public CreateTenantValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("İşletme adı gereklidir.")
            .MinimumLength(2).WithMessage("İşletme adı en az 2 karakter olmalı.")
            .MaximumLength(120).WithMessage("İşletme adı en fazla 120 karakter olabilir.");
    }
}
