using Accounting.Application.Abstractions;
using Microsoft.Extensions.Logging;

namespace Accounting.Infrastructure.Services;

/// <summary>
/// Geliştirme e-posta göndericisi: içeriği log'a yazar. Kalıcı sağlayıcı
/// (Resend/SES) IEmailSender arkasına bağlanabilir.
/// </summary>
public sealed partial class DevEmailSender(ILogger<DevEmailSender> logger) : IEmailSender
{
    public Task SendAsync(string recipient, string subject, string htmlBody, CancellationToken cancellationToken = default)
    {
        LogDevEmail(logger, recipient, subject, htmlBody);
        return Task.CompletedTask;
    }

    [LoggerMessage(Level = LogLevel.Information,
        Message = "==== DEV E-POSTA ==== Kime: {Recipient} | Konu: {Subject}\n{Body}\n=====================")]
    private static partial void LogDevEmail(ILogger logger, string recipient, string subject, string body);
}
