using CommunityOS.Finance.Domain.Aggregates;
using CommunityOS.Finance.Domain.Enumerations;
using CommunityOS.Finance.Domain.ValueObjects;

namespace CommunityOS.Finance.Tests.Domain;

/// <summary>
/// Locks the append-only ledger invariant (ADR-032): a recorded transaction
/// exposes no writable public property, so the entries are immutable after
/// creation. Corrections must append a reversal entry in a future gate — they
/// can never mutate the recorded entry.
/// </summary>
public class FinancialTransactionAppendOnlyTests
{
    [Fact]
    public void Recorded_transaction_exposes_no_writable_properties()
    {
        var writable = typeof(FinancialTransaction).GetProperties()
            .Where(p => p.SetMethod is not null && p.SetMethod.IsPublic)
            .Select(p => p.Name)
            .ToList();

        writable.Should().BeEmpty();
    }

    [Fact]
    public void Recorded_transaction_money_facts_are_stable_after_creation()
    {
        var fund = Fund.Create(Guid.NewGuid(), Guid.NewGuid(), "House of Justice", "Sacred Fund", "USD");
        var tx = fund.RecordTransaction(
            Guid.NewGuid(), Money.Create("USD", 250), FinancialTransactionType.Expense,
            "Fees", null, Guid.NewGuid(), DateTime.UtcNow);

        // The closure path is the only state change; the monetary facts and
        // identity never move.
        tx.Submit(Guid.NewGuid(), DateTime.UtcNow);
        tx.Approve(Guid.NewGuid(), DateTime.UtcNow);

        tx.Type.Name.Should().Be("expense");
        tx.Direction.Name.Should().Be("outflow");
        tx.Amount.Currency.Should().Be("USD");
        tx.Amount.MinorUnits.Should().Be(250);
        tx.Description.Should().Be("Fees");
        tx.TransferDestinationFundId.Should().BeNull();
        tx.Status.Name.Should().Be("approved");
    }
}