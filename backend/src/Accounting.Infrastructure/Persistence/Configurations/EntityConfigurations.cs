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
