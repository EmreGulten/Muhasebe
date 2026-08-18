using Accounting.Domain.Common;
using Microsoft.AspNetCore.Identity;

namespace Accounting.Domain.Entities;

/// <summary>
/// Uygulama kullanıcısı. ASP.NET Core Identity üzerinde genişletilmiştir.
/// </summary>
public class ApplicationUser : IdentityUser<Guid>, IHasTimestamps
{
    public string FullName { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; }

    public DateTime? UpdatedAtUtc { get; set; }

    public ICollection<UserTenant> Tenants { get; set; } = [];

    public ICollection<RefreshToken> RefreshTokens { get; set; } = [];
}
