using Accounting.Application.Abstractions;
using Accounting.Application.Common;
using Accounting.Contracts.Parties;
using Accounting.Domain.Entities;
using Accounting.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Accounting.Application.Features.Parties;

/// <summary>
/// Tenant bağlamından işletme kimliğini çözer; RequireTenant işaretli
/// endpoint'lerde middleware bunu garanti eder.
/// </summary>
internal static class PartyQueries
{
    public static Guid RequireTenantId(ICurrentTenant currentTenant) =>
        currentTenant.TenantId
        ?? throw new ConflictException("Aktif işletme bağlamı bulunamadı. X-Tenant-Id başlığını gönderin.");

    /// <summary>Belirtilen işletmeye ait, silinmemiş cari kartını getirir.</summary>
    public static Task<Party?> FindPartyAsync(
        IApplicationDbContext db, Guid tenantId, Guid partyId, CancellationToken cancellationToken) =>
        db.Parties.FirstOrDefaultAsync(p => p.TenantId == tenantId && p.Id == partyId, cancellationToken);

    /// <summary>Bir carinin hesap özeti: toplam borç, toplam alacak, son hareket tarihi.</summary>
    public static async Task<(decimal Balance, decimal TotalDebit, decimal TotalCredit, DateTime? Last)> SummarizeAsync(
        IApplicationDbContext db, Guid tenantId, Guid partyId, CancellationToken cancellationToken)
    {
        var summary = await db.PartyTransactions
            .Where(t => t.TenantId == tenantId && t.PartyId == partyId)
            .GroupBy(t => t.PartyId)
            .Select(g => new
            {
                Debit = g.Sum(t => t.Debit),
                Credit = g.Sum(t => t.Credit),
                Last = g.Max(t => (DateTime?)t.Date),
            })
            .FirstOrDefaultAsync(cancellationToken);

        var totalDebit = summary?.Debit ?? 0;
        var totalCredit = summary?.Credit ?? 0;

        return (totalDebit - totalCredit, totalDebit, totalCredit, summary?.Last);
    }

    /// <summary>Metni küçük harfe çevirerek Contains arar — SQLite ve PostgreSQL'de tutarlı davranır.</summary>
    public static string NormalizeSearch(string? search) =>
        search?.Trim().ToLowerInvariant() ?? string.Empty;
}

/// <summary>
/// Yeni cari kartı. OpeningBalance sıfırdan farklıysa tek seferlik açılış
/// hareketi de oluşturulur; party + hareket tek SaveChanges'ta atomik yazılır.
/// </summary>
public sealed class CreatePartyHandler(IApplicationDbContext db, ICurrentTenant currentTenant, TimeProvider timeProvider)
{
    public async Task<PartyResponse> HandleAsync(CreatePartyRequest request, CancellationToken cancellationToken)
    {
        var tenantId = PartyQueries.RequireTenantId(currentTenant);
        var now = timeProvider.GetUtcNow().UtcDateTime;

        var party = new Party
        {
            TenantId = tenantId,
            Type = PartyParsers.ParseType(request.Type),
            Name = request.Name.Trim(),
            TaxNumber = request.TaxNumber.NullIfEmpty(),
            TaxOffice = request.TaxOffice.NullIfEmpty(),
            Phone = request.Phone.NullIfEmpty(),
            Email = request.Email.NullIfEmpty(),
            Address = request.Address.NullIfEmpty(),
            City = request.City.NullIfEmpty(),
            District = request.District.NullIfEmpty(),
            ContactName = request.ContactName.NullIfEmpty(),
            OpeningBalance = request.OpeningBalance,
            CreditLimit = request.CreditLimit,
            Notes = request.Notes.NullIfEmpty(),
            IsActive = true,
            CreatedAtUtc = now,
        };
        db.Parties.Add(party);

        if (request.OpeningBalance != 0)
        {
            db.PartyTransactions.Add(new PartyTransaction
            {
                TenantId = tenantId,
                PartyId = party.Id,
                Type = PartyTransactionType.OpeningBalance,
                Debit = request.OpeningBalance > 0 ? request.OpeningBalance : 0,
                Credit = request.OpeningBalance < 0 ? -request.OpeningBalance : 0,
                Date = now.Date,
                Description = "Açılış bakiyesi",
                CreatedAtUtc = now,
            });
        }

        await db.SaveChangesAsync(cancellationToken);

        return new PartyResponse(
            party.Id, party.Type.ToString(), party.Name, party.TaxNumber, party.TaxOffice,
            party.Phone, party.Email, party.Address, party.City, party.District, party.ContactName,
            party.OpeningBalance, party.CreditLimit, party.Notes, party.IsActive,
            party.CreatedAtUtc, party.UpdatedAtUtc,
            Balance: request.OpeningBalance,
            TotalDebit: request.OpeningBalance > 0 ? request.OpeningBalance : 0,
            TotalCredit: request.OpeningBalance < 0 ? -request.OpeningBalance : 0,
            LastTransactionDateUtc: request.OpeningBalance != 0 ? now.Date : null);
    }
}

/// <summary>Cari kartını günceller. Açılış bakiyesi ve hareketler dokunulmaz.</summary>
public sealed class UpdatePartyHandler(IApplicationDbContext db, ICurrentTenant currentTenant, TimeProvider timeProvider)
{
    public async Task<PartyResponse> HandleAsync(Guid partyId, UpdatePartyRequest request, CancellationToken cancellationToken)
    {
        var tenantId = PartyQueries.RequireTenantId(currentTenant);

        var party = await PartyQueries.FindPartyAsync(db, tenantId, partyId, cancellationToken)
            ?? throw new NotFoundException("Cari kartı bulunamadı.");

        party.Type = PartyParsers.ParseType(request.Type);
        party.Name = request.Name.Trim();
        party.TaxNumber = request.TaxNumber.NullIfEmpty();
        party.TaxOffice = request.TaxOffice.NullIfEmpty();
        party.Phone = request.Phone.NullIfEmpty();
        party.Email = request.Email.NullIfEmpty();
        party.Address = request.Address.NullIfEmpty();
        party.City = request.City.NullIfEmpty();
        party.District = request.District.NullIfEmpty();
        party.ContactName = request.ContactName.NullIfEmpty();
        party.CreditLimit = request.CreditLimit;
        party.Notes = request.Notes.NullIfEmpty();
        party.IsActive = request.IsActive;
        party.UpdatedAtUtc = timeProvider.GetUtcNow().UtcDateTime;

        await db.SaveChangesAsync(cancellationToken);

        var (balance, totalDebit, totalCredit, last) = await PartyQueries.SummarizeAsync(db, tenantId, partyId, cancellationToken);
        return party.ToResponse(balance, totalDebit, totalCredit, last);
    }
}

/// <summary>
/// Cari kartını soft-delete ile kaldırır. Hareketi olan cari silinemez —
/// finansal kayıt zinciri korunur (muhasebe.md bölüm 23); pasifleştirme önerilir.
/// </summary>
public sealed class DeletePartyHandler(IApplicationDbContext db, ICurrentTenant currentTenant)
{
    public async Task HandleAsync(Guid partyId, CancellationToken cancellationToken)
    {
        var tenantId = PartyQueries.RequireTenantId(currentTenant);

        var party = await PartyQueries.FindPartyAsync(db, tenantId, partyId, cancellationToken)
            ?? throw new NotFoundException("Cari kartı bulunamadı.");

        var hasTransactions = await db.PartyTransactions
            .AnyAsync(t => t.TenantId == tenantId && t.PartyId == partyId, cancellationToken);

        if (hasTransactions)
        {
            throw new ConflictException(
                "Bu carinin hareket geçmişi var; finansal kayıtlar silinemez. " +
                "Cariyi pasifleştirmek için güncelleme'de IsActive alanını kapatın.");
        }

        // Interceptor fiziksel DELETE'i soft-delete'e çevirir.
        db.Parties.Remove(party);
        await db.SaveChangesAsync(cancellationToken);
    }
}

/// <summary>Tek carinin detayı ve hesap özeti.</summary>
public sealed class GetPartyHandler(IApplicationDbContext db, ICurrentTenant currentTenant)
{
    public async Task<PartyResponse> HandleAsync(Guid partyId, CancellationToken cancellationToken)
    {
        var tenantId = PartyQueries.RequireTenantId(currentTenant);

        var party = await PartyQueries.FindPartyAsync(db, tenantId, partyId, cancellationToken)
            ?? throw new NotFoundException("Cari kartı bulunamadı.");

        var (balance, totalDebit, totalCredit, last) = await PartyQueries.SummarizeAsync(db, tenantId, partyId, cancellationToken);
        return party.ToResponse(balance, totalDebit, totalCredit, last);
    }
}

/// <summary>
/// Cari listesi. type=Customer → Customer+Both, type=Supplier → Supplier+Both
/// ("bu rolde kullanılabilen" anlamında); arama ad/telefon/e-posta/vergi no üzerinden.
/// </summary>
public sealed class ListPartiesHandler(IApplicationDbContext db, ICurrentTenant currentTenant)
{
    private const int MaxPageSize = 100;

    public async Task<PagedResponse<PartySummaryDto>> HandleAsync(
        string? search, string? type, bool includeInactive, int page, int pageSize, CancellationToken cancellationToken)
    {
        var tenantId = PartyQueries.RequireTenantId(currentTenant);
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize <= 0 ? 20 : pageSize, 1, MaxPageSize);

        var query = db.Parties.AsNoTracking().Where(p => p.TenantId == tenantId);

        if (!includeInactive)
        {
            query = query.Where(p => p.IsActive);
        }

        if (PartyParsers.TryParseTypeFilter(type, out var typeFilter))
        {
            query = ApplyTypeFilter(query, typeFilter);
        }

        var term = PartyQueries.NormalizeSearch(search);
        if (term.Length > 0)
        {
            // string.ToLower() burada bilinçli: EF Core bunu SQL LOWER()'a çevirir ve
            // kültür parametresi alan overload çevrilemediği için kullanılamaz
            // (SQLite testlerinde de aynı şekilde davranır).
#pragma warning disable CA1304, CA1311, CA1862
            query = query.Where(p =>
                p.Name.ToLower().Contains(term)
                || (p.Phone != null && p.Phone.ToLower().Contains(term))
                || (p.Email != null && p.Email.ToLower().Contains(term))
                || (p.TaxNumber != null && p.TaxNumber.ToLower().Contains(term)));
#pragma warning restore CA1304, CA1311, CA1862
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderBy(p => p.Name)
            .ThenBy(p => p.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(p => new
            {
                p.Id, p.Type, p.Name, p.Phone, p.Email, p.City, p.IsActive,
            })
            .ToListAsync(cancellationToken);

        // Sayfadaki carilerin bakiye özetleri tek grup sorgusunda.
        var ids = items.Select(p => p.Id).ToList();
        var aggregates = await db.PartyTransactions
            .AsNoTracking()
            .Where(t => t.TenantId == tenantId && ids.Contains(t.PartyId))
            .GroupBy(t => t.PartyId)
            .Select(g => new
            {
                PartyId = g.Key,
                Balance = g.Sum(t => t.Debit) - g.Sum(t => t.Credit),
                Last = g.Max(t => (DateTime?)t.Date),
            })
            .ToDictionaryAsync(a => a.PartyId, cancellationToken);

        var summaries = items.Select(p => new PartySummaryDto(
                p.Id, p.Type.ToString(), p.Name, p.Phone, p.Email, p.City,
                aggregates.TryGetValue(p.Id, out var agg) ? agg.Balance : 0,
                p.IsActive,
                aggregates.TryGetValue(p.Id, out agg) ? agg.Last : null))
            .ToList();

        return new PagedResponse<PartySummaryDto>(summaries, page, pageSize, totalCount);
    }

    private static IQueryable<Party> ApplyTypeFilter(IQueryable<Party> query, PartyType typeFilter) =>
        typeFilter switch
        {
            PartyType.Customer => query.Where(p => p.Type == PartyType.Customer || p.Type == PartyType.Both),
            PartyType.Supplier => query.Where(p => p.Type == PartyType.Supplier || p.Type == PartyType.Both),
            _ => query.Where(p => p.Type == PartyType.Both),
        };
}

/// <summary>DTO ↔ enum dönüşümleri.</summary>
internal static class PartyParsers
{
    public static PartyType ParseType(string value) =>
        TryParseTypeFilter(value, out var type)
            ? type
            : throw new AppException("Cari türü geçersiz. Geçerli değerler: Customer, Supplier, Both.");

    /// <summary>"Customer"/"Supplier" filtre anlamında, "Both" yalnızca tam eşleşme olarak çözümlenir.</summary>
    public static bool TryParseTypeFilter(string? value, out PartyType type)
    {
        switch (value?.Trim())
        {
            case "Customer":
                type = PartyType.Customer;
                return true;
            case "Supplier":
                type = PartyType.Supplier;
                return true;
            case "Both":
                type = PartyType.Both;
                return true;
            default:
                type = default;
                return false;
        }
    }
}

internal static class PartyMappingExtensions
{
    public static string? NullIfEmpty(this string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    public static PartyResponse ToResponse(
        this Party party, decimal balance, decimal totalDebit, decimal totalCredit, DateTime? last) =>
        new(
            party.Id, party.Type.ToString(), party.Name, party.TaxNumber, party.TaxOffice,
            party.Phone, party.Email, party.Address, party.City, party.District, party.ContactName,
            party.OpeningBalance, party.CreditLimit, party.Notes, party.IsActive,
            party.CreatedAtUtc, party.UpdatedAtUtc,
            balance, totalDebit, totalCredit, last);
}
