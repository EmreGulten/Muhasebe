using Accounting.Contracts.Assistant;
using FluentValidation;

namespace Accounting.Application.Validators;

public sealed class AskAssistantValidator : AbstractValidator<AskAssistantRequest>
{
    public AskAssistantValidator()
    {
        RuleFor(x => x.Question)
            .NotEmpty().WithMessage("Soru boş olamaz.")
            .Length(1, 500).WithMessage("Soru en fazla 500 karakter olabilir.");
    }
}
