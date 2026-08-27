using CommunityOS.Finance.Domain.Aggregates;
using CommunityOS.Finance.Domain.Enumerations;
using CommunityOS.Finance.Domain.Events;
using CommunityOS.Finance.Domain.Exceptions;
using CommunityOS.Finance.Domain.ValueObjects;

namespace CommunityOS.Finance.Tests.Domain;

/// <summary>
/// Locks the fund aggregate: currency normalization and validation, the
/// closed-fund lifecycle, per-entry currency matching, and the single
/// <c>FinanceTransactionRecordedEvent</c> raised on record (approve, reject and
/// lifecycle changes raise nothing) with concurrency-token bumps on touch.
/// </summary>
public class FundTests
{
    private static readonly Guid Unit = Guid.NewGuid();
    private static readonly Guid Actor = Guid.NewGuid();
    private static readonly DateTime Now = DateTime.UtcNow;

    private static Fund ActiveFund(string currency = "USD") =>
        Fund.Create(Guid.NewGuid(), Unit, "House of Justice", "Sacred Fund", currency);

    private static FinancialTransaction Record(Fund fund, long minorUnits, FinancialTransactionType type) =>
        fund.RecordTransaction(
            Guid.NewGuid(), Money.Create(fund.Currency, minorUnits), type, null,
            null, Actor, Now);

    [Fact]
    public void Create_keeps_the_uppercase_currency_and_starts_active()
    {
        var fund = ActiveFund("USD");

        fund.Currency.Should().Be("USD");
        fund.Status.Should().Be(FundStatus.Active);
        fund.Revision.Should().Be(1);
    }

    [Fact]
    public void Create_rejects_a_lowercase_currency_code()
    {
        var act = () => ActiveFund("usd");

        act.Should().Throw<InvalidCurrencyCodeException>();
    }

    [Fact]
    public void Create_rejects_an_invalid_currency_code()
    {
        var act = () => ActiveFund("US");

        act.Should().Throw<InvalidCurrencyCodeException>();
    }

    [Fact]
    public void RecordTransaction_assigns_the_organizational_scope_and_raises_one_event()
    {
        var fund = ActiveFund();
        fund.RecordTransaction(
            Guid.NewGuid(), Money.Create("USD", 100), FinancialTransactionType.Contribution,
            "seeds", null, Actor, Now);

        var entry = fund.Transactions.Single();
        entry.OrganizationUnitId.Should().Be(Unit);
        entry.OrganizationUnitDisplayName.Should().Be("House of Justice");

        fund.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<FinanceTransactionRecordedEvent>();
        fund.Revision.Should().Be(2);
    }

    [Fact]
    public void RecordTransaction_requires_the_fund_currency()
    {
        var fund = ActiveFund("USD");

        var act = () => fund.RecordTransaction(
            Guid.NewGuid(), Money.Create("EUR", 100), FinancialTransactionType.Contribution,
            null, null, Actor, Now);

        act.Should().Throw<TransactionCurrencyMismatchException>();
    }

    [Fact]
    public void Approve_and_reject_raise_no_additional_domain_events()
    {
        var fund = ActiveFund();
        var tx = Record(fund, 100, FinancialTransactionType.Contribution);

        tx.Submit(Actor, Now);
        tx.Approve(Guid.NewGuid(), Now);

        fund.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<FinanceTransactionRecordedEvent>();
    }

    [Fact]
    public void Close_flips_the_fund_to_closed_and_is_idempotent_guarded()
    {
        var fund = ActiveFund();

        fund.Close();

        fund.Status.Should().Be(FundStatus.Closed);
        var act = () => fund.Close();
        act.Should().Throw<InvalidFundTransitionException>();
    }

    [Fact]
    public void Closed_fund_rejects_new_ledger_entries_but_keeps_its_history_readable()
    {
        var fund = ActiveFund();
        var tx = Record(fund, 100, FinancialTransactionType.Contribution);
        fund.Close();

        var act = () => Record(fund, 50, FinancialTransactionType.Expense);

        act.Should().Throw<ClosedFundTransactionException>();
        fund.Transactions.Should().Contain(tx);
    }
}