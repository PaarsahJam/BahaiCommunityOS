using CommunityOS.Finance.Domain.Aggregates;
using CommunityOS.Finance.Domain.Enumerations;
using CommunityOS.Finance.Domain.Repositories;
using CommunityOS.Finance.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CommunityOS.Finance.Infrastructure.Repositories;

/// <summary>
/// EF Core implementation of <see cref="IFundRepository"/>. Mutation paths run
/// against tracked aggregates so the <c>Revision</c> concurrency token is
/// captured from the database, bumped on change, and enforced by the UPDATE
/// predicate (ADR-032 appendix on append-only integrity).
/// </summary>
public sealed class FundRepository(FinanceDbContext db) : IFundRepository
{
    public Task<Fund?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        db.Funds.FirstOrDefaultAsync(f => f.Id == id, cancellationToken);

    public async Task<IReadOnlyList<Fund>> ListAsync(
        Guid? organizationUnitId = null, CancellationToken cancellationToken = default)
    {
        var query = db.Funds.AsNoTracking();
        if (organizationUnitId is { } unit)
            query = query.Where(f => f.OrganizationUnitId == unit);
        query = query.OrderByDescending(f => f.UpdatedOn);

        return (await query.ToListAsync(cancellationToken)).AsReadOnly();
    }

    public async Task AddAsync(Fund fund, CancellationToken cancellationToken = default)
    {
        db.Funds.Add(fund);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(Fund fund, CancellationToken cancellationToken = default)
    {
        db.Funds.Update(fund);
        await db.SaveChangesAsync(cancellationToken);
    }
}

/// <summary>
/// EF Core implementation of <see cref="IFinancialTransactionRepository"/>.
/// The repo deliberately has no in-place mutation, no delete and no
/// destination-fund foreign key: the ledger is append-only and cross-context
/// references are never enforced as constraints (ADR-032).
/// </summary>
public sealed class FinancialTransactionRepository(FinanceDbContext db) : IFinancialTransactionRepository
{
    public Task<FinancialTransaction?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        db.Transactions.FirstOrDefaultAsync(t => t.Id == id, cancellationToken);

    public async Task<IReadOnlyList<FinancialTransaction>> ListByFundAsync(
        Guid fundId,
        FinancialTransactionStatus? status = null,
        int limit = 100,
        CancellationToken cancellationToken = default)
    {
        var query = db.Transactions.AsNoTracking()
            .Where(t => t.FundId == fundId)
            .Where(t => status == null || t.Status == status)
            .OrderByDescending(t => t.OccurredOn);

        return (await query.Take(limit).ToListAsync(cancellationToken)).AsReadOnly();
    }

    public async Task AddAsync(FinancialTransaction transaction, CancellationToken cancellationToken = default)
    {
        db.Transactions.Add(transaction);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(FinancialTransaction transaction, CancellationToken cancellationToken = default)
    {
        db.Transactions.Update(transaction);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<long> GetApprovedBalanceMinorUnitsAsync(
        Guid fundId, CancellationToken cancellationToken = default)
    {
        // Derived balance (ADR-032): approved inflow − approved outflow +
        // approved transfer-into-fund. Three aggregate queries in one round
        // trip; the result is never persisted.
        var inflow = await db.Transactions.AsNoTracking()
            .Where(t => t.FundId == fundId &&
                        t.Status == FinancialTransactionStatus.Approved &&
                        t.Direction == FinancialTransactionDirection.Inflow)
            .SumAsync(t => (long?)t.Amount.MinorUnits, cancellationToken) ?? 0;

        var outflow = await db.Transactions.AsNoTracking()
            .Where(t => t.FundId == fundId &&
                        t.Status == FinancialTransactionStatus.Approved &&
                        t.Direction == FinancialTransactionDirection.Outflow)
            .SumAsync(t => (long?)t.Amount.MinorUnits, cancellationToken) ?? 0;

        var transferIn = await db.Transactions.AsNoTracking()
            .Where(t => t.Status == FinancialTransactionStatus.Approved &&
                        t.TransferDestinationFundId == fundId)
            .SumAsync(t => (long?)t.Amount.MinorUnits, cancellationToken) ?? 0;

        return inflow - outflow + transferIn;
    }
}

/// <summary>
/// EF Core implementation of <see cref="IFinanceOrganizationUnitReferenceRepository"/>.
/// </summary>
public sealed class FinanceOrganizationUnitReferenceRepository(FinanceDbContext db)
    : IFinanceOrganizationUnitReferenceRepository
{
    public Task<OrganizationUnitReference?> GetByOrganizationUnitIdAsync(
        Guid organizationUnitId, CancellationToken cancellationToken = default) =>
        db.OrganizationUnitReferences.AsNoTracking()
            .FirstOrDefaultAsync(r => r.OrganizationUnitId == organizationUnitId, cancellationToken);

    public Task<bool> ExistsAsync(Guid organizationUnitId, CancellationToken cancellationToken = default) =>
        db.OrganizationUnitReferences.AsNoTracking()
            .AnyAsync(r => r.OrganizationUnitId == organizationUnitId, cancellationToken);

    public async Task AddAsync(OrganizationUnitReference reference, CancellationToken cancellationToken = default)
    {
        db.OrganizationUnitReferences.Add(reference);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(OrganizationUnitReference reference, CancellationToken cancellationToken = default)
    {
        db.OrganizationUnitReferences.Update(reference);
        await db.SaveChangesAsync(cancellationToken);
    }
}