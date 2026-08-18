using Accounting.Application.Common;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace Accounting.Api.Middleware;

/// <summary>
/// Beklenmeyen hataları ve AppException türevlerini standart ProblemDetails
/// yanıtına çevirir. İç hata detayları dışarıya sızdırılmaz.
/// </summary>
public sealed partial class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var (status, title, detail) = exception switch
        {
            AppException appException => (appException.StatusCode, appException.Title, appException.Message),
            _ => (StatusCodes.Status500InternalServerError, "Sunucu hatası",
                "Beklenmeyen bir hata oluştu. Sorun sürerse lütfen destek ile iletişime geçin."),
        };

        if (status >= StatusCodes.Status500InternalServerError)
        {
            LogUnhandled(logger, exception, httpContext.Request.Path.ToString());
        }
        else
        {
            LogRejected(logger, status, httpContext.Request.Path.ToString(), exception.Message);
        }

        httpContext.Response.StatusCode = status;
        await httpContext.Response.WriteAsJsonAsync(new ProblemDetails
        {
            Type = $"https://httpstatuses.io/{status}",
            Title = title,
            Detail = detail,
            Status = status,
            Instance = httpContext.Request.Path,
        }, cancellationToken);

        return true;
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "İşlenmeyen hata: {Path}")]
    private static partial void LogUnhandled(ILogger logger, Exception exception, string path);

    [LoggerMessage(Level = LogLevel.Warning, Message = "İstek reddedildi ({Status}) {Path}: {Reason}")]
    private static partial void LogRejected(ILogger logger, int status, string path, string reason);
}
