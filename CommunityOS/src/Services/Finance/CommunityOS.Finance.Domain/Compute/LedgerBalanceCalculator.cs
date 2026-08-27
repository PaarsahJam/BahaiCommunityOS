using CommunityOS.Finance.Domain.Aggregates;
using CommunityOS.Finance.Domain.Enumerations;

namespace CommunityOS.Finance.Domain.Compute;

/// <summary>
/// Pure derivation of a fund's approved balance (ADR-032). The balance is
/// never stored: it is the sum over <c>Approved</c> entries of the signed
/// effect relative to the fund. Entries on the fund itself contribute
/// +inflow/−outflow; a transfer into this fund contributes +amount via the
/// destination reference. The persistence layer implements an equivalent SQL
/// derivation (see <c>IFinancialTransactionRepository.GetApprovedBalanceAsync</c>);
/// this pure form exists so unit tests can lock the derivation against an
/// in-memory ledger without a database.
/// </summary>
public static class LedgerBalanceCalculator
{
    /// <summary>
    /// Signed minor-unit effect of one approved entry relative to
    /// <paramref name="fundId"/>. Non-approved entries and entries that neither
    /// belong to nor target the fund contribute zero.
    /// </summary>
    public static long EffectFor(FinancialTransaction transaction, Guid fundId)
    {
        if (transaction.Status != FinancialTransactionStatus.Approved)
            return 0;

        if (transaction.TransferDestinationFundId is { } destination && destination == fundId)
            return transaction.Amount.MinorUnits;

        if (transaction.FundId == fundId)
            return transaction.Direction == FinancialTransactionDirection.Inflow
                ? transaction.Amount.MinorUnits
                : -transaction.Amount.MinorUnits;

        return 0;
    }

    /// <summary>Sums the approved ledger into a single signed minor-unit balance.</summary>
    public static long Compute(Guid fundId, IEnumerable<FinancialTransaction> transactions) =>
        transactions.Sum(tx => EffectFor(tx, fundId));
}