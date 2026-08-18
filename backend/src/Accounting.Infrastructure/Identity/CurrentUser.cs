using System.Security.Claims;
using Accounting.Application.Abstractions;
using Microsoft.AspNetCore.Http;

namespace Accounting.Infrastructure.Identity;

/// <summary>HttpContext üzerinden oturum açan kullanıcı bilgisini okur.</summary>
public sealed class CurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    private HttpContext? Context => accessor.HttpContext;

    public Guid? UserId
    {
        get
        {
            var value = Context?.User.FindFirstValue(ClaimTypes.NameIdentifier)
                        ?? Context?.User.FindFirstValue("sub");
            return Guid.TryParse(value, out var id) ? id : null;
        }
    }

    public string? IpAddress => Context?.Connection.RemoteIpAddress?.ToString();

    public string? UserAgent => Context?.Request.Headers.UserAgent.ToString();
}
