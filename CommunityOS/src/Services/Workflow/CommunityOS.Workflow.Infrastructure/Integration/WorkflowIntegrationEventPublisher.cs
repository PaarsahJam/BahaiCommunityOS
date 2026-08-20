using CommunityOS.Contracts.Workflow;
using CommunityOS.SharedKernel.Domain.Events;
using CommunityOS.Workflow.Domain.Events;
using MassTransit;
using MediatR;

namespace CommunityOS.Workflow.Infrastructure.Integration;

/// <summary>
/// Publishes Workflow domain events onto the message bus as integration events
/// (ADR-024). Only stable ids and minimal lifecycle metadata are exported —
/// never task notes, escalation reasons, names or secrets. The open generic is
/// closed per concrete domain event by MediatR dispatch on the runtime
/// notification type. With the bus outbox enabled, <see cref="IPublishEndpoint"/>
/// publishes are captured into the DbContext and delivered after the business
/// transaction commits (ADR-015, ratified at the Workflow outbox gate).
/// </summary>
public sealed class WorkflowIntegrationEventPublisher<TDomainEvent>(IPublishEndpoint publishEndpoint)
    : INotificationHandler<TDomainEvent>
    where TDomainEvent : IDomainEvent
{
    public async Task Handle(TDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        switch (domainEvent)
        {
            case WorkflowTaskCreatedEvent e:
                await publishEndpoint.Publish(
                    new WorkflowTaskCreated(
                        e.TaskId, e.DefinitionCode, e.DomainType, e.DomainEntityId,
                        e.OrganizationUnitId, e.CreatedBy, e.OccurredOn), cancellationToken);
                break;
            case WorkflowTaskAssignedEvent e:
                await publishEndpoint.Publish(
                    new WorkflowTaskAssigned(
                        e.TaskId, e.DefinitionCode, e.AssigneeIds, e.AssignedBy, e.OccurredOn), cancellationToken);
                break;
            case WorkflowTaskStartedEvent e:
                await publishEndpoint.Publish(
                    new WorkflowTaskStarted(e.TaskId, e.DefinitionCode, e.StartedBy, e.OccurredOn), cancellationToken);
                break;
            case WorkflowTaskCompletedEvent e:
                await publishEndpoint.Publish(
                    new WorkflowTaskCompleted(
                        e.TaskId, e.DefinitionCode, e.Outcome, e.CompletedBy, e.OccurredOn), cancellationToken);
                break;
            case WorkflowTaskCancelledEvent e:
                await publishEndpoint.Publish(
                    new WorkflowTaskCancelled(e.TaskId, e.DefinitionCode, e.CancelledBy, e.OccurredOn), cancellationToken);
                break;
            case WorkflowTaskEscalatedEvent e:
                await publishEndpoint.Publish(
                    new WorkflowTaskEscalated(
                        e.TaskId, e.DefinitionCode, e.EscalatedTo, e.EscalatedBy, e.OccurredOn), cancellationToken);
                break;
        }
    }
}