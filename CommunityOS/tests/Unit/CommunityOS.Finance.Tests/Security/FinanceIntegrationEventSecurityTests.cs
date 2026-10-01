using CommunityOS.Contracts.Finance;
using CommunityOS.Finance.Domain.Aggregates;
using CommunityOS.Finance.Domain.Enumerations;
using CommunityOS.Finance.Domain.Events;
using CommunityOS.Finance.Domain.ValueObjects;
using CommunityOS.Finance.Infrastructure.Integration;
using MassTransit;
using NSubstitute;

namespace CommunityOS.Finance.Tests.Security;

/// <summary>
/// Locks the integration-event export boundary (ADR-032): publishing a recorded
/// ledger entry carries only stable identifiers and lifecycle metadata — never
/// amounts, currencies or descriptions. The publisher is the only sanctioned
/// path from finance domain events to the bus, and approve/reject raise nothing.
/// </summary>
public class FinanceIntegrationEventSecurityTests
{
    private static readonly Guid Actor = Guid.NewGuid();
    private static readonly DateTime Now = DateTime.UtcNow;

    private static Fund FundWith(Action<Fund> configure)
    {
        var fund = Fund.Create(Guid.NewGuid(), Guid.NewGuid(), "House of Justice", "Sacred Fund", "USD");
        configure(fund);
        return fund;
    }

    private static async Task<FinanceTransactionRecorded> PublishAsync(FinanceTransactionRecordedEvent e)
    {
        var captured = new List<FinanceTransactionRecorded>();
        var endpoint = Substitute.For<IPublishEndpoint>();
        endpoint.When(x => x.Publish(Arg.Any<FinanceTransactionRecorded>(), Arg.Any<CancellationToken>()))
            .Do(call => captured.Add(call.Arg<FinanceTransactionRecorded>()));

        await new FinanceIntegrationEventPublisher<FinanceTransactionRecordedEvent>(endpoint)
            .Handle(e, CancellationToken.None);

        return captured.Single();
    }

    [Fact]
    public async Task Recorded_publishes_only_stable_identifiers_and_never_financial_figures()
    {
        const string description = "Seeds of giving — confidential purpose text.";
        var fund = FundWith(f =>
            f.RecordTransaction(
                Guid.NewGuid(), Money.Create("USD", 12345), FinancialTransactionType.Contribution,
                description, null, Actor, Now));
        var recorded = fund.DomainEvents.OfType<FinanceTransactionRecordedEvent>().Single();
        var transaction = fund.Transactions.Single();

        var published = await PublishAsync(recorded);

        published.TransactionId.Should().Be(transaction.Id);
        published.FundId.Should().Be(fund.Id);
        published.TransactionType.Should().Be("contribution");
        published.Direction.Should().Be("inflow");
        published.Status.Should().Be("recorded");
        published.RecordedBy.Should().Be(Actor);
        published.OccurredOn.Should().NotBe(default);
        published.ToString().Should().NotContain(description);
        published.ToString().Should().NotContain("USD");
        published.ToString().Should().NotContain("12345");
    }

    [Fact]
    public async Task Transfer_recorded_publishes_no_destination_and_no_amount()
    {
        var destination = Guid.NewGuid();
        var fund = FundWith(f =>
            f.RecordTransaction(
                Guid.NewGuid(), Money.Create("USD", 999), FinancialTransactionType.Transfer,
                null, destination, Actor, Now));
        var recorded = fund.DomainEvents.OfType<FinanceTransactionRecordedEvent>().Single();

        var published = await PublishAsync(recorded);

        published.TransactionType.Should().Be("transfer");
        published.ToString().Should().NotContain(destination.ToString());
        published.ToString().Should().NotContain("999");
    }

    [Fact]
    public void Export_contract_is_limited_to_the_single_recorded_event()
    {
        // Approve and reject intentionally raise no events; the outbox can
        // therefore export exactly one finance contract in this gate.
        var fund = FundWith(f =>
        {
            var tx = f.RecordTransaction(
                Guid.NewGuid(), Money.Create("USD", 100), FinancialTransactionType.Contribution,
                null, null, Actor, Now);
            tx.Submit(Actor, Now);
            tx.Approve(Guid.NewGuid(), Now);
        });

        fund.DomainEvents.OfType<FinanceTransactionRecordedEvent>().Should().ContainSingle();
    }
}