using CommunityOS.Finance.Domain.Aggregates;
using CommunityOS.Finance.Domain.Compute;
using CommunityOS.Finance.Domain.Enumerations;
using CommunityOS.Finance.Domain.ValueObjects;

namespace CommunityOS.Finance.Tests.Domain;

/// <summary>
/// Locks the derived-balance derivation (ADR-032): only <c>Approved</c> entries
/// count; the sign is inflow plus / outflow minus; a transfer into the fund
/// contributes via the destination reference while a transfer out of the fund
/// counts as an outflow on the source fund. The balance is never stored — the
/// persistence layer derives the same numbers with SQL.
/// </summary>
public class LedgerBalanceTests
{
    private static readonly Guid Fund = Guid.NewGuid();
    private static readonly Guid OtherFund = Guid.NewGuid();
    private static readonly DateTime Now = DateTime.UtcNow;

    private static FinancialTransaction Entry(
        long minorUnits,
        FinancialTransactionType type,
        Guid? destination = null,
        FinancialTransactionStatus? status = null)
    {
        status ??= FinancialTransactionStatus.Approved;
        var tx = FinancialTransaction.Create(
            Guid.NewGuid(), Fund, Money.Create("USD", minorUnits), type,
            null, destination, Guid.NewGuid(), Now);
        if (status == FinancialTransactionStatus.Approved)
        {
            tx.Submit(Guid.NewGuid(), Now);
            tx.Approve(Guid.NewGuid(), Now);
        }

        return tx;
    }

    private static FinancialTransaction ApprovedTransferIn(long minorUnits)
    {
        var tx = FinancialTransaction.Create(
            Guid.NewGuid(), OtherFund, Money.Create("USD", minorUnits),
            FinancialTransactionType.Transfer, null, Fund, Guid.NewGuid(), Now);
        tx.Submit(Guid.NewGuid(), Now);
        tx.Approve(Guid.NewGuid(), Now);
        return tx;
    }

    [Fact]
    public void Compute_sums_approved_inflow_minus_outflow_plus_transfers_in()
    {
        var ledger = new[]
        {
            Entry(1000, FinancialTransactionType.Contribution),
            Entry(250, FinancialTransactionType.Expense),
            Entry(75, FinancialTransactionType.Reimbursement),
            Entry(100, FinancialTransactionType.Transfer, destination: OtherFund),
            ApprovedTransferIn(500),
        };

        LedgerBalanceCalculator.Compute(Fund, ledger).Should().Be(1000 - 250 - 75 - 100 + 500);
    }

    [Fact]
    public void Non_approved_entries_contribute_nothing()
    {
        var ledger = new[]
        {
            Entry(1000, FinancialTransactionType.Contribution),
            Entry(300, FinancialTransactionType.Contribution, status: FinancialTransactionStatus.PendingApproval),
            Entry(200, FinancialTransactionType.Contribution, status: FinancialTransactionStatus.Recorded),
            Entry(50, FinancialTransactionType.Expense, status: FinancialTransactionStatus.Rejected),
        };

        LedgerBalanceCalculator.Compute(Fund, ledger).Should().Be(1000);
    }

    [Fact]
    public void EffectFor_returns_zero_for_foreign_entries()
    {
        var foreign = FinancialTransaction.Create(
            Guid.NewGuid(), OtherFund, Money.Create("USD", 100),
            FinancialTransactionType.Contribution, null, null, Guid.NewGuid(), Now);
        foreign.Submit(Guid.NewGuid(), Now);
        foreign.Approve(Guid.NewGuid(), Now);

        LedgerBalanceCalculator.EffectFor(foreign, Fund).Should().Be(0);
    }

    [Fact]
    public void Cross_context_ledger_never_leaks_entries_between_funds()
    {
        // If the ledger crossed contexts (violating ADR-032's closed bound),
        // the target fund's balance could still only see its own entries.
        var own = Entry(1000, FinancialTransactionType.Contribution);
        var other = ApprovedTransferIn(250);

        // Fund sees its own contribution plus the transfer-in via the
        // destination reference.
        LedgerBalanceCalculator.Compute(Fund, [own, other]).Should().Be(1250);

        // The source fund's own view: the transfer is an outflow there.
        LedgerBalanceCalculator.Compute(OtherFund, [own, other]).Should().Be(-250);
    }
}