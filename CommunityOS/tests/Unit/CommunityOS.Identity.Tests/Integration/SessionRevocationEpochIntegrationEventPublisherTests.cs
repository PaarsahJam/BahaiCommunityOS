using CommunityOS.Contracts.Identity;
using CommunityOS.Identity.Domain.Events;
using CommunityOS.Identity.Infrastructure.Integration;
using FluentAssertions;
using MassTransit;
using NSubstitute;

namespace CommunityOS.Identity.Tests.Integration;

/// <summary>
/// ADR-036 Q4: the <see cref="IdentityIntegrationEventPublisher{TDomainEvent}"/>
/// must forward the epoch-advanced domain decision to the bus as exactly one
/// <see cref="SessionRevocationEpochAdvanced"/> integration event carrying the
/// affected account, the resulting epoch, and the original decision timestamp —
/// never regenerated and never duplicated.
/// </summary>
public sealed class SessionRevocationEpochIntegrationEventPublisherTests
{
    [Fact]
    public async Task Publish_ForwardsTheEpochDecisionAsExactlyOneIntegrationEvent()
    {
        var accountId = Guid.NewGuid();
        var domainEvent = new SessionRevocationEpochAdvancedEvent(accountId, Epoch: 4);
        var publishEndpoint = Substitute.For<IPublishEndpoint>();
        var publisher = new IdentityIntegrationEventPublisher<SessionRevocationEpochAdvancedEvent>(publishEndpoint);

        await publisher.Handle(domainEvent, CancellationToken.None);

        await publishEndpoint.Received(1).Publish(
            Arg.Is<SessionRevocationEpochAdvanced>(e =>
                e.UserAccountId == accountId
                && e.SessionRevocationEpoch == 4
                && e.OccurredOn == domainEvent.OccurredOn),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Publish_PropagatesTheDecisionTimestampAsTheEventIdentity()
    {
        var accountId = Guid.NewGuid();
        var domainEvent = new SessionRevocationEpochAdvancedEvent(accountId, Epoch: 1);
        var publishEndpoint = Substitute.For<IPublishEndpoint>();
        var publisher = new IdentityIntegrationEventPublisher<SessionRevocationEpochAdvancedEvent>(publishEndpoint);

        await publisher.Handle(domainEvent, CancellationToken.None);

        await publishEndpoint.Received(1).Publish(
            Arg.Is<SessionRevocationEpochAdvanced>(e =>
                e.UserAccountId == accountId
                && e.OccurredOn == domainEvent.OccurredOn
                && e.OccurredOn != default),
            Arg.Any<CancellationToken>());
    }
}