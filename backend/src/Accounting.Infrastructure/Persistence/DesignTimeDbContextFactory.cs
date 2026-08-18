using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Accounting.Infrastructure.Persistence;

/// <summary>
/// dotnet ef için design-time factory. Bağlantı gerçekten açılmaz; yalnızca
/// model/migration üretiminde kullanılır. Program.cs'in fail-fast kontrollerini
/// (ör. JWT secret) devre dışı bırakmadan migration üretmeyi sağlar.
/// </summary>
public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql("Host=localhost;Port=5432;Database=accounting;Username=accounting;Password=design-time")
            .Options;

        return new AppDbContext(options);
    }
}
