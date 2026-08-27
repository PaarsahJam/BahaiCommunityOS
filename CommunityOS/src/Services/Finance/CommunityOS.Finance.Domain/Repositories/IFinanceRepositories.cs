using CommunityOS.Finance.Domain.Aggregates;
using CommunityOS.Finance.Domain.Enumerations;

namespace CommunityOS.Finance.Domain.Repositories;

public interface IFundRepository
{
    Task<Fund?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Fund>> ListAsync(
        Guid? organizationUnitId = null,
        CancellationToken cancellationToken = default);

    Task AddAsync(Fund fund, CancellationToken cancellationToken = default);

    Task UpdateAsync(Fund fund, CancellationToken cancellationToken = default);
}

public interface IFinancialTransactionRepository
{
    Task<FinancialTransaction?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<FinancialTransaction>> ListByFundAsync(
        Guid fundId,
        FinancialTransactionStatus? status = null,
        int limit = 100,
        CancellationToken cancellationToken = default);

    Task AddAsync(FinancialTransaction transaction, CancellationToken cancellationToken = default);

    Task UpdateAsync(FinancialTransaction transaction, CancellationToken cancellationToken = default);

    /// <summary>
    /// Derives the approved balance of a fund in minor units (ADR-032):
    /// approved inflow − approved outflow + approved transfer-into-fund. It is
    /// intentionally never persisted; the same derivation is implemented by
    /// <c>LedgerBalanceCalculator</c> in the domain for testability.
    /// </summary>
    Task<long> GetApprovedBalanceMinorUnitsAsync(Guid fundId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Read-model of organization unit scopes projected from Organization unit
/// events (ADR-016). Fund creation fails closed when the owning unit is not
/// yet projected here, so a fund is never created against an unverifiable
/// scope.
/// </summary>
public interface IFinanceOrganizationUnitReferenceRepository
{
    Task<OrganizationUnitReference?> GetByOrganizationUnitIdAsync(
        Guid organizationUnitId, CancellationToken cancellationToken = default);

    Task<bool> ExistsAsync(Guid organizationUnitId, CancellationToken cancellationToken = default);

    Task AddAsync(OrganizationUnitReference reference, CancellationToken cancellationToken = default);

    Task UpdateAsync(OrganizationUnitReference reference, CancellationToken cancellationToken = default);
}