using FluentValidation;
using Microsoft.AspNetCore.Http;

namespace Accounting.Api.Middleware;

/// <summary>
/// FluentValidation validator'ları minimal API body parametrelerine otomatik
/// uygular. Uyumsuzluk varsa 400 + alan bazlı hata listesi döner.
/// </summary>
public sealed class ValidationEndpointFilter : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        foreach (var argument in context.Arguments)
        {
            if (argument is null)
            {
                continue;
            }

            var validatorType = typeof(IValidator<>).MakeGenericType(argument.GetType());
            if (context.HttpContext.RequestServices.GetService(validatorType) is not IValidator validator)
            {
                continue;
            }

            var validationResult = await validator.ValidateAsync(
                new ValidationContext<object>(argument),
                context.HttpContext.RequestAborted);

            if (!validationResult.IsValid)
            {
                var errors = validationResult.Errors
                    .GroupBy(f => f.PropertyName, StringComparer.Ordinal)
                    .ToDictionary(g => g.Key, g => g.Select(f => f.ErrorMessage).ToArray());

                return Results.ValidationProblem(errors, detail: "Gönderdiğiniz bilgilerde düzeltilmesi gereken alanlar var.");
            }
        }

        return await next(context);
    }
}
