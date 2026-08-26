using Accounting.Application.Abstractions;
using Accounting.Application.Common;
using Accounting.Contracts.IncomeExpenses;
using Accounting.Domain.Entities;
using Accounting.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Accounting.Application.Features.IncomeExpenses;

/// <summary>Gelir/gider sorgu yardımcıları — diğer modüllerdeki desenle aynı.</summary>
internal static class IncomeExpenseQueries
{
    public static Guid RequireTenantId(ICurrentTenant currentTenant) =>
        currentTenant.TenantId ?? throw new AppException("Eksik işletme bilgisi.");

    /// <summary>Kontrattaki tür dizgesini enum'a çevirir; geçersizse uygulamayı durdurur.</summary>
    public static IncomeExpenseType ParseType(string? type) =>
        type switch
        {
            "Income" => IncomeExpenseType.Income,
            "Expense" => IncomeExpenseType.Expense,
            _ => throw new AppException("Kayıt türü geçersiz. Geçerli değerler: Income, Expense."),
        };

    public static string? NullIfEmpty(this string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

/// <summary>
/// Kategori listesi. Varsayılan 13 gider ve 4 gelir kategorisi eksikse her çağrıda
/// tamamlanır — kullanıcının sildiği
/// varsayılan geri gelmez, elle eklenen kategori tohumlamayı engellemez.
/// "Diğer" her iki tarafta bulunabildiği için benzersizlik tenant + tür içindedir.
/// </summary>
public sealed class ListIncomeExpenseCategoriesHandler(
    IApplicationDbContext db, ICurrentTenant currentTenant, TimeProvider timeProvider)
{
    /// <summary>Yeni işletmeler için varsayılan gelir ve gider kategorileri.</summary>
    public static readonly (string Name, IncomeExpenseType Type)[] DefaultCategories =
    [
        ("Kira", IncomeExpenseType.Expense),
        ("Elektrik", IncomeExpenseType.Expense),
        ("Su", IncomeExpenseType.Expense),
        ("Doğalgaz", IncomeExpenseType.Expense),
        ("Personel", IncomeExpenseType.Expense),
        ("Yakıt", IncomeExpenseType.Expense),
        ("Kargo", IncomeExpenseType.Expense),
        ("Reklam", IncomeExpenseType.Expense),
        ("Yemek", IncomeExpenseType.Expense),
        ("Vergi", IncomeExpenseType.Expense),
        ("Muhasebeci", IncomeExpenseType.Expense),
        ("Bakım", IncomeExpenseType.Expense),
        ("Diğer", IncomeExpenseType.Expense),
        ("Hizmet", IncomeExpenseType.Income),
        ("Kira Geliri", IncomeExpenseType.Income),
        ("Faiz", IncomeExpenseType.Income),
        ("Diğer", IncomeExpenseType.Income),
    ];

    public async Task<IReadOnlyList<IncomeExpenseCategoryDto>> HandleAsync(
        string? type, CancellationToken cancellationToken)
    {
        var tenantId = IncomeExpenseQueries.RequireTenantId(currentTenant);

        // Varsayılanları eksik olanlara ekle (idempotent). Varlık kontrolü
        // soft-deleted satırları da sayar: kullanıcının sildiği varsayılan
        // dirilmez; elle eklenen kategori tohumlamayı engellemez.
        var existing = await db.IncomeExpenseCategories
            .IgnoreQueryFilters()
            .Where(c => c.TenantId == tenantId)
            .Select(c => new { c.Type, c.Name })
            .ToListAsync(cancellationToken);
        var existingKeys = existing.Select(c => (c.Type, c.Name)).ToHashSet();

        var missing = DefaultCategories
            .Where(entry => !existingKeys.Contains((entry.Type, entry.Name)))
            .ToList();
        if (missing.Count > 0)
        {
            var now = timeProvider.GetUtcNow().UtcDateTime;
            db.IncomeExpenseCategories.AddRange(missing.Select(entry => new IncomeExpenseCategory
            {
                TenantId = tenantId,
                Name = entry.Name,
                Type = entry.Type,
                IsActive = true,
                CreatedAtUtc = now,
            }));
            await db.SaveChangesAsync(cancellationToken);
        }

        var parsedType = type is null ? null : (IncomeExpenseType?)IncomeExpenseQueries.ParseType(type);

        var counts = await db.IncomeExpenseRecords.AsNoTracking()
            .Where(r => r.TenantId == tenantId)
            .GroupBy(r => r.CategoryId)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(g => g.Key, g => g.Count, cancellationToken);

        var categories = await db.IncomeExpenseCategories.AsNoTracking()
            .Where(c => c.TenantId == tenantId && (parsedType == null || c.Type == parsedType))
            .OrderBy(c => c.Type)
            .ThenBy(c => c.Name)
            .ToListAsync(cancellationToken);

        return categories
            .Select(c => new IncomeExpenseCategoryDto(
                c.Id, c.Name, c.Type.ToString(), c.IsActive,
                counts.GetValueOrDefault(c.Id), c.CreatedAtUtc, c.UpdatedAtUtc))
            .ToList();
    }
}

/// <summary>Yeni kategori — ad tenant ve tür içinde benzersizdir.</summary>
public sealed class CreateIncomeExpenseCategoryHandler(
    IApplicationDbContext db, ICurrentTenant currentTenant, TimeProvider timeProvider)
{
    public async Task<IncomeExpenseCategoryDto> HandleAsync(
        CreateIncomeExpenseCategoryRequest request, CancellationToken cancellationToken)
    {
        var tenantId = IncomeExpenseQueries.RequireTenantId(currentTenant);
        var name = request.Name.Trim();
        var type = IncomeExpenseQueries.ParseType(request.Type);

        if (await NameInUseAsync(db, tenantId, name, type, null, cancellationToken))
        {
            throw new ConflictException($"'{name}' adında bir kategori bu tarafta zaten var.");
        }

        var category = new IncomeExpenseCategory
        {
            TenantId = tenantId,
            Name = name,
            Type = type,
            IsActive = true,
            CreatedAtUtc = timeProvider.GetUtcNow().UtcDateTime,
        };
        db.IncomeExpenseCategories.Add(category);
        await db.SaveChangesAsync(cancellationToken);

        return new IncomeExpenseCategoryDto(category.Id, category.Name, category.Type.ToString(), true, 0,
            category.CreatedAtUtc, null);
    }

    internal static Task<bool> NameInUseAsync(
        IApplicationDbContext db, Guid tenantId, string name, IncomeExpenseType type, Guid? excludeId,
        CancellationToken cancellationToken) =>
        db.IncomeExpenseCategories.AnyAsync(
            c => c.TenantId == tenantId && c.Type == type && c.Name == name && c.Id != excludeId,
            cancellationToken);
}

/// <summary>Kategori düzenleme — yalnızca ad ve aktiflik; tür sabittir.</summary>
public sealed class UpdateIncomeExpenseCategoryHandler(
    IApplicationDbContext db, ICurrentTenant currentTenant, TimeProvider timeProvider)
{
    public async Task<IncomeExpenseCategoryDto> HandleAsync(
        Guid id, UpdateIncomeExpenseCategoryRequest request, CancellationToken cancellationToken)
    {
        var tenantId = IncomeExpenseQueries.RequireTenantId(currentTenant);
        var name = request.Name.Trim();

        var category = await db.IncomeExpenseCategories
            .FirstOrDefaultAsync(c => c.TenantId == tenantId && c.Id == id, cancellationToken)
            ?? throw new NotFoundException("Kategori bulunamadı.");

        if (await CreateIncomeExpenseCategoryHandler.NameInUseAsync(
                db, tenantId, name, category.Type, category.Id, cancellationToken))
        {
            throw new ConflictException($"'{name}' adında bir kategori bu tarafta zaten var.");
        }

        category.Name = name;
        category.IsActive = request.IsActive;
        category.UpdatedAtUtc = timeProvider.GetUtcNow().UtcDateTime;
        await db.SaveChangesAsync(cancellationToken);

        var count = await db.IncomeExpenseRecords.AsNoTracking()
            .CountAsync(r => r.TenantId == tenantId && r.CategoryId == category.Id, cancellationToken);

        return new IncomeExpenseCategoryDto(category.Id, category.Name, category.Type.ToString(),
            category.IsActive, count, category.CreatedAtUtc, category.UpdatedAtUtc);
    }
}

/// <summary>
/// Kategori silme (soft delete). Kaydı olan kategori silinemez — kayıt zinciri
/// korunur; pasifleştirme önerilir (cari/ürün modülleriyle aynı kural).
/// </summary>
public sealed class DeleteIncomeExpenseCategoryHandler(
    IApplicationDbContext db, ICurrentTenant currentTenant, TimeProvider timeProvider)
{
    public async Task HandleAsync(Guid id, CancellationToken cancellationToken)
    {
        var tenantId = IncomeExpenseQueries.RequireTenantId(currentTenant);

        var category = await db.IncomeExpenseCategories
            .FirstOrDefaultAsync(c => c.TenantId == tenantId && c.Id == id, cancellationToken)
            ?? throw new NotFoundException("Kategori bulunamadı.");

        if (await db.IncomeExpenseRecords.AnyAsync(
                r => r.TenantId == tenantId && r.CategoryId == id, cancellationToken))
        {
            throw new ConflictException("Kaydı olan kategori silinemez; kategoriyi pasifleştirin.");
        }

        category.IsDeleted = true;
        category.DeletedAtUtc = timeProvider.GetUtcNow().UtcDateTime;
        await db.SaveChangesAsync(cancellationToken);
    }
}
