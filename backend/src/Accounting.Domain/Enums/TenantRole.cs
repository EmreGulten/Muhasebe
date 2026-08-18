namespace Accounting.Domain.Enums;

/// <summary>
/// Bir kullanıcının işletme (tenant) içindeki rolü.
/// Rol, UserTenant üyeliği üzerinde tutulur; aynı kullanıcı farklı işletmelerde
/// farklı rollere sahip olabilir.
/// </summary>
public enum TenantRole
{
    Owner = 1,
    Admin = 2,
    Accountant = 3,
    Employee = 4,
    Viewer = 5,
}
