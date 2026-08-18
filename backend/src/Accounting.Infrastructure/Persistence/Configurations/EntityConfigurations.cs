using Accounting.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Accounting.Infrastructure.Persistence.Configurations;

public sealed class TenantConfiguration : IEntityTypeConfiguration<Tenant>
{
    public void Configure(EntityTypeBuilder<Tenant> builder)
    {
        builder.ToTable("Tenants");

        // Silinmiş işletmeler sorgularda otomatik süzülür.
        builder.HasQueryFilter(t => !t.IsDeleted);

        builder.HasKey(t => t.Id);

        builder.Property(t => t.Name)
            .IsRequired()
            .HasMaxLength(120);

        builder.HasIndex(t => t.IsDeleted);

        builder.HasMany(t => t.Members)
            .WithOne(m => m.Tenant)
            .HasForeignKey(m => m.TenantId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class UserTenantConfiguration : IEntityTypeConfiguration<UserTenant>
{
    public void Configure(EntityTypeBuilder<UserTenant> builder)
    {
        builder.ToTable("UserTenants");

        builder.HasKey(m => m.Id);

        // Bir kullanıcı bir işletmede tek üyeliğe sahip olabilir.
        builder.HasIndex(m => new { m.UserId, m.TenantId }).IsUnique();

        builder.HasOne(m => m.User)
            .WithMany(u => u.Tenants)
            .HasForeignKey(m => m.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(m => m.Tenant)
            .WithMany(t => t.Members)
            .HasForeignKey(m => m.TenantId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.ToTable("RefreshTokens");

        builder.HasKey(t => t.Id);

        builder.Property(t => t.TokenHash)
            .IsRequired()
            .HasMaxLength(64);

        builder.HasIndex(t => t.TokenHash).IsUnique();

        builder.HasIndex(t => t.ExpiresAtUtc);

        builder.HasOne(t => t.User)
            .WithMany(u => u.RefreshTokens)
            .HasForeignKey(t => t.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.ToTable("AuditLogs");

        builder.HasKey(a => a.Id);

        builder.Property(a => a.EntityType)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(a => a.EntityId)
            .IsRequired()
            .HasMaxLength(64);

        builder.Property(a => a.Action)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(a => a.IpAddress).HasMaxLength(64);

        builder.HasIndex(a => new { a.TenantId, a.CreatedAtUtc });

        builder.HasIndex(a => a.EntityType);
    }
}

public sealed class PartyConfiguration : IEntityTypeConfiguration<Party>
{
    public void Configure(EntityTypeBuilder<Party> builder)
    {
        builder.ToTable("Parties");

        // Silinmiş cariler sorgularda otomatik süzülür.
        builder.HasQueryFilter(p => !p.IsDeleted);

        builder.HasKey(p => p.Id);

        builder.Property(p => p.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(p => p.Type).HasConversion<int>();

        builder.Property(p => p.TaxNumber).HasMaxLength(20);
        builder.Property(p => p.TaxOffice).HasMaxLength(60);
        builder.Property(p => p.Phone).HasMaxLength(30);
        builder.Property(p => p.Email).HasMaxLength(150);
        builder.Property(p => p.Address).HasMaxLength(300);
        builder.Property(p => p.City).HasMaxLength(60);
        builder.Property(p => p.District).HasMaxLength(60);
        builder.Property(p => p.ContactName).HasMaxLength(120);
        builder.Property(p => p.Notes).HasMaxLength(1000);

        // Liste ve tenant izolasyonu için sorgu desenleri.
        builder.HasIndex(p => new { p.TenantId, p.Name });
        builder.HasIndex(p => new { p.TenantId, p.IsActive });

        builder.HasMany(p => p.Transactions)
            .WithOne(t => t.Party)
            .HasForeignKey(t => t.PartyId)
            .OnDelete(DeleteBehavior.Restrict); // hareket zinciri cascade'siz kalmalı
    }
}

public sealed class PartyTransactionConfiguration : IEntityTypeConfiguration<PartyTransaction>
{
    public void Configure(EntityTypeBuilder<PartyTransaction> builder)
    {
        builder.ToTable("PartyTransactions");

        builder.HasKey(t => t.Id);

        builder.Property(t => t.Type).HasConversion<int>();

        builder.Property(t => t.Description).HasMaxLength(300);
        builder.Property(t => t.ReferenceType).HasMaxLength(50);

        // Ekstre sorgusu: tenant + party + tarih sırası.
        builder.HasIndex(t => new { t.TenantId, t.PartyId, t.Date });

        // Üretilecek referans kayıtlar (satış/alış) üzerinden ters bulma.
        builder.HasIndex(t => new { t.TenantId, t.ReferenceType, t.ReferenceId });
    }
}

public sealed class CategoryConfiguration : IEntityTypeConfiguration<Category>
{
    public void Configure(EntityTypeBuilder<Category> builder)
    {
        builder.ToTable("Categories");

        builder.HasQueryFilter(c => !c.IsDeleted);

        builder.HasKey(c => c.Id);

        builder.Property(c => c.Name)
            .IsRequired()
            .HasMaxLength(100);

        // Benzersizlik handler'da denetlenir: soft-delete satırları DB benzersiz
        // indexini bloklardığı için burada yalnız sorgu dizini bırakılır.
        builder.HasIndex(c => new { c.TenantId, c.Name });
    }
}

public sealed class UnitConfiguration : IEntityTypeConfiguration<Unit>
{
    public void Configure(EntityTypeBuilder<Unit> builder)
    {
        builder.ToTable("Units");

        builder.HasQueryFilter(u => !u.IsDeleted);

        builder.HasKey(u => u.Id);

        builder.Property(u => u.Name)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(u => u.Code).HasMaxLength(10);

        builder.HasIndex(u => new { u.TenantId, u.Name });
    }
}

public sealed class WarehouseConfiguration : IEntityTypeConfiguration<Warehouse>
{
    public void Configure(EntityTypeBuilder<Warehouse> builder)
    {
        builder.ToTable("Warehouses");

        builder.HasQueryFilter(w => !w.IsDeleted);

        builder.HasKey(w => w.Id);

        builder.Property(w => w.Name)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(w => w.Address).HasMaxLength(300);

        // Varsayılan depo çözümleme deseni.
        builder.HasIndex(w => new { w.TenantId, w.IsDefault });
    }
}

public sealed class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.ToTable("Products");

        builder.HasQueryFilter(p => !p.IsDeleted);

        builder.HasKey(p => p.Id);

        builder.Property(p => p.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(p => p.Sku).HasMaxLength(50);
        builder.Property(p => p.Barcode).HasMaxLength(50);
        builder.Property(p => p.Description).HasMaxLength(500);

        // KDV oranı yüzde: numeric(5,2); kritik eşik miktar alanıdır: numeric(18,4).
        builder.Property(p => p.VatRate).HasColumnType("numeric(5,2)");
        builder.Property(p => p.MinimumStock).HasColumnType("numeric(18,4)");

        // SKU benzersizliği handler'da denetlenir (soft-delete ile index çakışması).
        builder.HasIndex(p => new { p.TenantId, p.Name });
        builder.HasIndex(p => new { p.TenantId, p.Sku });
        builder.HasIndex(p => new { p.TenantId, p.IsActive });

        builder.HasOne(p => p.Category)
            .WithMany(c => c.Products)
            .HasForeignKey(p => p.CategoryId)
            .OnDelete(DeleteBehavior.Restrict); // kullanan ürün varken kategori düşmez

        builder.HasOne(p => p.Unit)
            .WithMany(u => u.Products)
            .HasForeignKey(p => p.UnitId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(p => p.InventoryTransactions)
            .WithOne(t => t.Product)
            .HasForeignKey(t => t.ProductId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class InventoryTransactionConfiguration : IEntityTypeConfiguration<InventoryTransaction>
{
    public void Configure(EntityTypeBuilder<InventoryTransaction> builder)
    {
        builder.ToTable("InventoryTransactions");

        builder.HasKey(t => t.Id);

        builder.Property(t => t.Type).HasConversion<int>();

        // İşaretli miktar: giriş pozitif, çıkış negatif — numeric(18,4).
        builder.Property(t => t.Quantity).HasColumnType("numeric(18,4)");

        builder.Property(t => t.Description).HasMaxLength(300);
        builder.Property(t => t.ReferenceType).HasMaxLength(50);

        // Hareket geçmişi sorgusu: tenant + ürün + tarih.
        builder.HasIndex(t => new { t.TenantId, t.ProductId, t.Date });

        // Depo bazlı stok toplamı.
        builder.HasIndex(t => new { t.TenantId, t.WarehouseId });

        // Üretici kayıt (satış/alış/transfer) üzerinden ters bulma.
        builder.HasIndex(t => new { t.TenantId, t.ReferenceType, t.ReferenceId });

        builder.HasOne(t => t.Warehouse)
            .WithMany(w => w.Transactions)
            .HasForeignKey(t => t.WarehouseId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
