using CommunityOS.Contracts.Finance;
using CommunityOS.Finance.Domain.Events;
using CommunityOS.SharedKernel.Domain.Events;
using MassTransit;
using MediatR;

namespace CommunityOS.Finance.Infrastructure.Integration;

/// <summary>
/// Publishes Finance domain events onto the message bus as integration events
/// (ADR-032). Exactly one contract is exported in the ratified gate —
/// <c>FinanceTransactionRecorded</c> — carrying only stable ids and lifecycle
/// metadata and never amounts, currencies, descriptions or attribution. The
/// open generic is closed per concrete domain event by MediatR dispatch on the
/// runtime notification type. With the bus outbox enabled,
/// <see cref="IPublishEndpoint"/> publishes are captured into the DbContext
/// and delivered after the business transaction commits (ADR-015, ratified).
/// </summary>
public sealed class FinanceIntegrationEventPublisher<TDomainEvent>(IPublishEndpoint publishEndpoint)
    : INotificationHandler<TDomainEvent>
    where TDomainEvent : IDomainEvent
{
    public async Task Handle(TDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        switch (domainEvent)
        {
            case FinanceTransactionRecordedEvent e:
                await publishEndpoint.Publish(
                    new FinanceTransactionRecorded(
                        e.TransactionId,
                        e.FundId,
                        e.TransactionType,
                        e.Direction,
                        e.Status,
                        e.RecordedBy,
                        e.OccurredOn),
                    cancellationToken);
                break;
        }
    }
}