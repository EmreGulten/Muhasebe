using Accounting.Contracts.Auth;
using FluentValidation;

namespace Accounting.Application.Validators;

/// <summary>
/// Parola kuralı Identity policy ile aynı tutulur; anlaşılır Türkçe mesaj verir.
/// </summary>
public static class PasswordRules
{
    public static IRuleBuilderOptions<T, string> StrongPassword<T>(this IRuleBuilder<T, string> ruleBuilder) =>
        ruleBuilder
            .NotEmpty().WithMessage("Parola gereklidir.")
            .MinimumLength(8).WithMessage("Parola en az 8 karakter olmalıdır.")
            .MaximumLength(128)
            .Must(p => p.Any(char.IsDigit)).WithMessage("Parola en az bir rakam içermelidir.")
            .Must(p => p.Any(char.IsUpper)).WithMessage("Parola en az bir büyük harf içermelidir.")
            .Must(p => p.Any(char.IsLower)).WithMessage("Parola en az bir küçük harf içermelidir.");
}

public sealed class RegisterValidator : AbstractValidator<RegisterRequest>
{
    public RegisterValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("E-posta gereklidir.")
            .EmailAddress().WithMessage("Geçerli bir e-posta adresi girin.")
            .MaximumLength(256);

        RuleFor(x => x.Password).StrongPassword();

        RuleFor(x => x.FullName)
            .NotEmpty().WithMessage("Ad soyad gereklidir.")
            .MaximumLength(150);

        RuleFor(x => x.BusinessName)
            .MinimumLength(2).WithMessage("İşletme adı en az 2 karakter olmalı.")
            .MaximumLength(120).WithMessage("İşletme adı en fazla 120 karakter olabilir.");
    }
}

public sealed class LoginValidator : AbstractValidator<LoginRequest>
{
    public LoginValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("E-posta gereklidir.")
            .EmailAddress().WithMessage("Geçerli bir e-posta adresi girin.");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Parola gereklidir.");
    }
}

public sealed class ForgotPasswordValidator : AbstractValidator<ForgotPasswordRequest>
{
    public ForgotPasswordValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("E-posta gereklidir.")
            .EmailAddress().WithMessage("Geçerli bir e-posta adresi girin.");
    }
}

public sealed class ResetPasswordValidator : AbstractValidator<ResetPasswordRequest>
{
    public ResetPasswordValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("E-posta gereklidir.")
            .EmailAddress().WithMessage("Geçerli bir e-posta adresi girin.");

        RuleFor(x => x.ResetToken)
            .NotEmpty().WithMessage("Parola sıfırlama kodu gereklidir.");

        RuleFor(x => x.NewPassword).StrongPassword();
    }
}
