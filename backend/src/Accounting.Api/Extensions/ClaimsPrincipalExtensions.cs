using System.Security.Claims;
using Accounting.Application.Common;

namespace Accounting.Api.Extensions;

public static class ClaimsPrincipalExtensions
{
    /// <summary>JWT "sub" claim'inden kullanıcı kimliğini çözer.</summary>
    public static Guid GetUserId(this ClaimsPrincipal principal)
    {
        var value = principal.FindFirstValue("sub")
                    ?? principal.FindFirstValue(ClaimTypes.NameIdentifier);

        return Guid.TryParse(value, out var id) && id != Guid.Empty
            ? id
            : throw new UnauthorizedException("Oturum bilgisi geçersiz.");
    }
}
