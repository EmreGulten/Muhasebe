using Accounting.Application.Abstractions;
using Accounting.Application.Common;
using Accounting.Contracts.Parties;
using Accounting.Domain.Entities;
using Accounting.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Accounting.Application.Features.Parties;

/// <summary>
/// Manuel cari hareketi ekler.
/// Yalnızca açılış/borçlandırma/alacaklandırma/düzeltme kabul edilir; Satış,
/// Tahsilat, Alış, Ödeme türleri ilgili modüller tarafından üretilir.
/// </summary>
public sealed class CreatePartyTransactionHandler(
    IApplicationDbContext db,
    ICurrentTenant currentTenant,
    TimeProvider timeProvider)
{
    private static readonly HashSet<PartyTransactionType> ManualTypes =
    [
        PartyTransactionType.OpeningBalance,
        PartyTransactionType.Debit,
        PartyTransactionType.Credit,
        PartyTransactionType.Adjustment,
    ];

    public async Task<PartyTransactionDto> HandleAsync(
        Guid partyId, CreatePartyTransactionRequest request, CancellationToken cancellationToken)
    {
        var tenantId = PartyQueries.RequireTenantId(currentTenant);

        var party = await PartyQueries.FindPartyAsync(db, tenantId, partyId, cancellationToken)
            ?? throw new NotFoundException("Cari kartı bulunamadı.");

        if (!party.IsActive)
        {
            throw new ConflictException("Pasif cariye hareket girilemez. Önce cariyi aktifleştirin.");
        }

        var type = ParseTransactionType(request.Type);

        if (!ManualTypes.Contains(type))
        {
            throw new AppException(
                "Bu hareket türü manuel girilemez; satış/tahsilat/alış/ödeme hareketleri ilgili modüller üzerinden oluşur. " +
                "Manuel türler: OpeningBalance, Debit, Credit, Adjustment.");
        }

        if (type == PartyTransactionType.OpeningBalance)
        {
            var hasOpening = await db.PartyTransactions.AnyAsync(
                t => t.TenantId == tenantId && t.PartyId == partyId && t.Type == PartyTransactionType.OpeningBalance,
                cancellationToken);

            if (hasOpening)
            {
                throw new ConflictException("Bu carinin zaten bir açılış bakiyesi hareketi var.");
            }
        }

        var amount = request.Amount;
        if (amount == 0)
        {
            throw new AppException("Hareket tutarı sıfır olamaz.");
        }

        // Tür-tutar tutarlılığı: işaretli tutar modelinde borç pozitif, alacak negatiftir.
        if (type == PartyTransactionType.Debit && amount < 0)
        {
            throw new AppException("Borçlandırma hareketinin tutarı pozitif olmalı.");
        }

        if (type == PartyTransactionType.Credit && amount > 0)
        {
            throw new AppException("Alacaklandırma hareketinin tutarı negatif olmalı.");
        }

        if (request.DueDate is { } dueDate && dueDate.Date < request.Date.Date)
        {
            throw new AppException("Vade tarihi hareket tarihinden önce olamaz.");
        }

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var transaction = new PartyTransaction
        {
            TenantId = tenantId,
            PartyId = partyId,
            Type = type,
            Debit = amount > 0 ? amount : 0,
            Credit = amount < 0 ? -amount : 0,
            Date = Dates.ToUtcDate(request.Date),
            DueDate = request.DueDate is { } due ? Dates.ToUtcDate(due) : null,
            Description = request.Description.NullIfEmpty(),
            CreatedAtUtc = now,
        };
        db.PartyTransactions.Add(transaction);

        await db.SaveChangesAsync(cancellationToken);

        // Bu hareket dahil kümülatif bakiye (ekstredeki çalışan bakiye).
        var balance = await db.PartyTransactions
            .AsNoTracking()
            .Where(t => t.TenantId == tenantId && t.PartyId == partyId)
            .Select(t => t.Debit - t.Credit)
            .SumAsync(cancellationToken);

        return transaction.ToDto(balance);
    }

    private static PartyTransactionType ParseTransactionType(string value) =>
        Enum.TryParse<PartyTransactionType>(value, ignoreCase: false, out var type)
            ? type
            : throw new AppException(
                "Hareket türü geçersiz. Geçerli değerler: OpeningBalance, Debit, Credit, Adjustment.");
}

/// <summary>
/// Cari ekstre — tarih sırasına göre hareketler ve sayfa içi çalışan bakiye.
/// Sıralama (Date, Id) belirleyicidir; Guid v7 kimlikler giriş sırasını korur.
/// </summary>
public sealed class GetPartyStatementHandler(IApplicationDbContext db, ICurrentTenant currentTenant)
{
    private const int MaxPageSize = 200;

    public async Task<PartyStatementResponse> HandleAsync(
        Guid partyId, int page, int pageSize, CancellationToken cancellationToken)
    {
        var tenantId = PartyQueries.RequireTenantId(currentTenant);

        var party = await db.Parties
            .AsNoTracking()
            .Where(p => p.TenantId == tenantId && p.Id == partyId)
            .Select(p => new { p.Id, p.Name })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Cari kartı bulunamadı.");

        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize <= 0 ? 50 : pageSize, 1, MaxPageSize);
        var offset = (page - 1) * pageSize;

        var query = db.PartyTransactions
            .AsNoTracking()
            .Where(t => t.TenantId == tenantId && t.PartyId == partyId)
            .OrderBy(t => t.Date)
            .ThenBy(t => t.Id);

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .Skip(offset)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        // Sayfadaki ilk satırdan önceki kümülatif bakiye.
        var balanceBeforePage = await query
            .Take(offset)
            .Select(t => t.Debit - t.Credit)
            .SumAsync(cancellationToken);

        var runningBalance = balanceBeforePage;
        var dtos = items.Select(t =>
        {
            runningBalance += t.Debit - t.Credit;
            return t.ToDto(runningBalance);
        }).ToList();

        return new PartyStatementResponse(party.Id, party.Name, balanceBeforePage, page, pageSize, totalCount, dtos);
    }
}

internal static class PartyTransactionMappingExtensions
{
    public static PartyTransactionDto ToDto(this PartyTransaction transaction, decimal balance) =>
        new(
            transaction.Id,
            transaction.Type.ToString(),
            transaction.Date,
            transaction.DueDate,
            transaction.Debit,
            transaction.Credit,
            transaction.Description,
            transaction.ReferenceType,
            transaction.ReferenceId,
            balance,
            transaction.CreatedAtUtc);
}
