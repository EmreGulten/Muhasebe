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

// ---- Satış (PHASE 4)

public sealed class SaleConfiguration : IEntityTypeConfiguration<Sale>
{
    public void Configure(EntityTypeBuilder<Sale> builder)
    {
        builder.ToTable("Sales");

        builder.HasKey(s => s.Id);

        builder.Property(s => s.Number).HasMaxLength(20);
        builder.Property(s => s.Description).HasMaxLength(500);
        builder.Property(s => s.CancelReason).HasMaxLength(300);
        builder.Property(s => s.Status).HasConversion<int>();

        builder.HasQueryFilter(s => !s.IsDeleted);

        // Numara tenant içinde benzersiz (eşzamanlı seri atamasını DB düzeyinde korur).
        builder.HasIndex(s => new { s.TenantId, s.Number }).IsUnique();

        // Liste sorguları: durum + tarih, müşteri filtresi.
        builder.HasIndex(s => new { s.TenantId, s.Status, s.Date });
        builder.HasIndex(s => new { s.TenantId, s.PartyId });

        builder.HasOne(s => s.Party)
            .WithMany()
            .HasForeignKey(s => s.PartyId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(s => s.Warehouse)
            .WithMany()
            .HasForeignKey(s => s.WarehouseId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

/// <summary>Satış kalemi — belge ile birlikte yaşar, defter kaydı değildir.</summary>
public sealed class SaleItemConfiguration : IEntityTypeConfiguration<SaleItem>
{
    public void Configure(EntityTypeBuilder<SaleItem> builder)
    {
        builder.ToTable("SaleItems");

        builder.HasKey(i => i.Id);

        builder.Property(i => i.ProductName).HasMaxLength(200);
        builder.Property(i => i.Quantity).HasColumnType("numeric(18,4)");
        // Fiyat/tutar alanları AppDbContext varsayılanından numeric(18,2) alır.

        builder.HasIndex(i => new { i.TenantId, i.ProductId });

        builder.HasOne(i => i.Sale)
            .WithMany(s => s.Items)
            .HasForeignKey(i => i.SaleId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(i => i.Product)
            .WithMany()
            .HasForeignKey(i => i.ProductId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

/// <summary>Tahsilat — defter kaydı, silinmez.</summary>
public sealed class SalePaymentConfiguration : IEntityTypeConfiguration<SalePayment>
{
    public void Configure(EntityTypeBuilder<SalePayment> builder)
    {
        builder.ToTable("SalePayments");

        builder.HasKey(p => p.Id);

        builder.Property(p => p.Description).HasMaxLength(300);

        builder.HasIndex(p => new { p.TenantId, p.SaleId });
        builder.HasIndex(p => new { p.TenantId, p.AccountId, p.Date });

        builder.HasOne(p => p.Sale)
            .WithMany(s => s.Payments)
            .HasForeignKey(p => p.SaleId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(p => p.Account)
            .WithMany()
            .HasForeignKey(p => p.AccountId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

/// <summary>Kasa/banka hesabı — PHASE 4 minimal (default "Kasa"), yönetimi PHASE 6.</summary>
public sealed class AccountConfiguration : IEntityTypeConfiguration<Account>
{
    public void Configure(EntityTypeBuilder<Account> builder)
    {
        builder.ToTable("Accounts");

        builder.HasKey(a => a.Id);

        builder.Property(a => a.Name).HasMaxLength(100);
        builder.Property(a => a.Type).HasConversion<int>();
        builder.Property(a => a.Currency).HasMaxLength(3);
        builder.Property(a => a.OpeningBalance).HasPrecision(18, 2);

        builder.HasQueryFilter(a => !a.IsDeleted);

        builder.HasIndex(a => new { a.TenantId, a.IsDefault });
    }
}

/// <summary>Hesap hareketi — defter kaydı, işaretli tutarla numeric(18,2).</summary>
public sealed class AccountTransactionConfiguration : IEntityTypeConfiguration<AccountTransaction>
{
    public void Configure(EntityTypeBuilder<AccountTransaction> builder)
    {
        builder.ToTable("AccountTransactions");

        builder.HasKey(t => t.Id);

        builder.Property(t => t.Type).HasConversion<int>();
        builder.Property(t => t.Description).HasMaxLength(300);
        builder.Property(t => t.ReferenceType).HasMaxLength(50);

        // Hesap ekstresi: tenant + hesap + tarih.
        builder.HasIndex(t => new { t.TenantId, t.AccountId, t.Date });

        // Üretici kayıt üzerinden ters bulma (satış iptali vb.).
        builder.HasIndex(t => new { t.TenantId, t.ReferenceType, t.ReferenceId });

        builder.HasOne(t => t.Account)
            .WithMany(a => a.Transactions)
            .HasForeignKey(t => t.AccountId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

// ---- Alış (PHASE 5)

/// <summary>Alış belgesi — satış konfigürasyonunun aynası.</summary>
public sealed class PurchaseConfiguration : IEntityTypeConfiguration<Purchase>
{
    public void Configure(EntityTypeBuilder<Purchase> builder)
    {
        builder.ToTable("Purchases");

        builder.HasKey(p => p.Id);

        builder.Property(p => p.Number).HasMaxLength(20);
        builder.Property(p => p.Description).HasMaxLength(500);
        builder.Property(p => p.CancelReason).HasMaxLength(300);
        builder.Property(p => p.Status).HasConversion<int>();

        builder.HasQueryFilter(p => !p.IsDeleted);

        // Numara tenant içinde benzersiz (eşzamanlı seri atamasını DB düzeyinde korur).
        builder.HasIndex(p => new { p.TenantId, p.Number }).IsUnique();

        // Liste sorguları: durum + tarih, tedarikçi filtresi.
        builder.HasIndex(p => new { p.TenantId, p.Status, p.Date });
        builder.HasIndex(p => new { p.TenantId, p.PartyId });

        builder.HasOne(p => p.Party)
            .WithMany()
            .HasForeignKey(p => p.PartyId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(p => p.Warehouse)
            .WithMany()
            .HasForeignKey(p => p.WarehouseId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

/// <summary>Alış kalemi — belge ile birlikte yaşar, defter kaydı değildir.</summary>
public sealed class PurchaseItemConfiguration : IEntityTypeConfiguration<PurchaseItem>
{
    public void Configure(EntityTypeBuilder<PurchaseItem> builder)
    {
        builder.ToTable("PurchaseItems");

        builder.HasKey(i => i.Id);

        builder.Property(i => i.ProductName).HasMaxLength(200);
        builder.Property(i => i.Quantity).HasColumnType("numeric(18,4)");

        builder.HasIndex(i => new { i.TenantId, i.ProductId });

        builder.HasOne(i => i.Purchase)
            .WithMany(p => p.Items)
            .HasForeignKey(i => i.PurchaseId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(i => i.Product)
            .WithMany()
            .HasForeignKey(i => i.ProductId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

/// <summary>Alış ödemesi — defter kaydı, silinmez.</summary>
public sealed class PurchasePaymentConfiguration : IEntityTypeConfiguration<PurchasePayment>
{
    public void Configure(EntityTypeBuilder<PurchasePayment> builder)
    {
        builder.ToTable("PurchasePayments");

        builder.HasKey(p => p.Id);

        builder.Property(p => p.Description).HasMaxLength(300);

        builder.HasIndex(p => new { p.TenantId, p.PurchaseId });
        builder.HasIndex(p => new { p.TenantId, p.AccountId, p.Date });

        builder.HasOne(p => p.Purchase)
            .WithMany(p => p.Payments)
            .HasForeignKey(p => p.PurchaseId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(p => p.Account)
            .WithMany()
            .HasForeignKey(p => p.AccountId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

// ---- Gelir / gider (PHASE 7)

/// <summary>Gelir/gider kategorisi — ad tenant ve tür içinde benzersizdir.</summary>
public sealed class IncomeExpenseCategoryConfiguration : IEntityTypeConfiguration<IncomeExpenseCategory>
{
    public void Configure(EntityTypeBuilder<IncomeExpenseCategory> builder)
    {
        builder.ToTable("IncomeExpenseCategories");

        builder.HasQueryFilter(c => !c.IsDeleted);

        builder.HasKey(c => c.Id);

        builder.Property(c => c.Name)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(c => c.Type).HasConversion<int>();

        // Benzersizlik handler'da denetlenir (soft-delete satırları DB
        // benzersizliğini bloklardığı için burada sorgu dizini kalır).
        builder.HasIndex(c => new { c.TenantId, c.Type, c.Name });
    }
}

/// <summary>Gelir/gider kaydı — değiştirilemez defter satırı, numeric(18,2).</summary>
public sealed class IncomeExpenseRecordConfiguration : IEntityTypeConfiguration<IncomeExpenseRecord>
{
    public void Configure(EntityTypeBuilder<IncomeExpenseRecord> builder)
    {
        builder.ToTable("IncomeExpenseRecords");

        builder.HasKey(r => r.Id);

        builder.Property(r => r.Type).HasConversion<int>();
        builder.Property(r => r.Status).HasConversion<int>();
        builder.Property(r => r.Amount).HasPrecision(18, 2);
        builder.Property(r => r.Description).HasMaxLength(300);
        builder.Property(r => r.DocumentNumber).HasMaxLength(50);
        builder.Property(r => r.AttachmentUrl).HasMaxLength(500);

        // Liste filtreleri: dönem + tür; kategori bazlı döküm.
        builder.HasIndex(r => new { r.TenantId, r.Date });
        builder.HasIndex(r => new { r.TenantId, r.CategoryId });

        builder.HasOne(r => r.Category)
            .WithMany(c => c.Records)
            .HasForeignKey(r => r.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(r => r.PaymentAccount)
            .WithMany()
            .HasForeignKey(r => r.PaymentAccountId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

/// <summary>AI asistan sohbet mesajları (bölüm 11, PHASE 9).</summary>
public sealed class AiMessageConfiguration : IEntityTypeConfiguration<AiMessage>
{
    public void Configure(EntityTypeBuilder<AiMessage> builder)
    {
        builder.ToTable("AiMessages");

        builder.HasKey(m => m.Id);

        builder.Property(m => m.Role).HasConversion<int>();
        builder.Property(m => m.Content).HasMaxLength(4000).IsRequired();

        // Sohbet geçmişi ve aylık kullanım limiti sayımı.
        builder.HasIndex(m => new { m.TenantId, m.UserId, m.CreatedAtUtc });
        builder.HasIndex(m => new { m.TenantId, m.Role, m.CreatedAtUtc });
    }
}
