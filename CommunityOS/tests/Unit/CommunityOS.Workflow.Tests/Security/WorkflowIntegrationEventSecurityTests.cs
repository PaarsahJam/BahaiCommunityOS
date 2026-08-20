using CommunityOS.Contracts.Workflow;
using CommunityOS.Workflow.Domain.Aggregates;
using CommunityOS.Workflow.Domain.Enumerations;
using CommunityOS.Workflow.Domain.Events;
using CommunityOS.Workflow.Infrastructure.Integration;
using MassTransit;
using NSubstitute;

namespace CommunityOS.Workflow.Tests.Security;

/// <summary>
/// Locks the integration-event export boundary (ADR-024): published payloads
/// carry only stable identifiers and minimal lifecycle metadata — never task
/// notes, escalation reasons, names or secrets. The publisher is the only
/// sanctioned path from domain events to the bus.
/// </summary>
public class WorkflowIntegrationEventSecurityTests
{
    private static readonly Guid Actor = Guid.NewGuid();
    private static readonly Guid Assignee = Guid.NewGuid();
    private static readonly DateTime Now = DateTime.UtcNow;

    private static WorkflowTask CreateAssignedTask()
    {
        var task = WorkflowTask.Create(
            WorkflowDefinitionCodes.RecordReview,
            WorkflowDomainTypes.Record,
            Guid.NewGuid(),
            Guid.NewGuid(),
            [],
            [Assignee],
            dueOn: null,
            notes: "Confidential originator note.",
            originatorId: Guid.NewGuid(),
            createdBy: Actor,
            occurredOn: Now);
        task.Start(Assignee, adminOverride: false, Now);
        return task;
    }

    [Fact]
    public async Task WorkflowTaskCreated_publishes_only_stable_identifiers_and_never_notes()
    {
        const string note = "Confidential originator note.";
        var task = WorkflowTask.Create(
            WorkflowDefinitionCodes.RecordReview,
            WorkflowDomainTypes.Record,
            domainEntityId: Guid.NewGuid(),
            organizationUnitId: Guid.NewGuid(),
            additionalScopes: [],
            assigneeIds: [],
            dueOn: null,
            notes: note,
            originatorId: Guid.NewGuid(),
            createdBy: Actor,
            occurredOn: Now);
        var created = task.DomainEvents.OfType<WorkflowTaskCreatedEvent>().Single();

        var captured = new List<WorkflowTaskCreated>();
        var endpoint = Substitute.For<IPublishEndpoint>();
        endpoint.When(x => x.Publish(Arg.Any<WorkflowTaskCreated>(), Arg.Any<CancellationToken>()))
            .Do(call => captured.Add(call.Arg<WorkflowTaskCreated>()));

        await new WorkflowIntegrationEventPublisher<WorkflowTaskCreatedEvent>(endpoint)
            .Handle(created, CancellationToken.None);

        var published = captured.Single();
        published.TaskId.Should().Be(task.Id);
        published.DefinitionCode.Should().Be(WorkflowDefinitionCodes.RecordReview);
        published.DomainType.Should().Be(WorkflowDomainTypes.Record);
        published.DomainEntityId.Should().Be(task.DomainEntityId!.Value);
        published.OrganizationUnitId.Should().Be(task.OrganizationUnitId);
        published.CreatedBy.Should().Be(Actor);
        published.ToString().Should().NotContain(note);
    }

    [Fact]
    public async Task WorkflowTaskAssigned_publishes_actor_ids_only()
    {
        var task = WorkflowTask.Create(
            WorkflowDefinitionCodes.RecordReview,
            WorkflowDomainTypes.Record,
            Guid.NewGuid(),
            Guid.NewGuid(),
            [],
            [],
            dueOn: null,
            notes: null,
            originatorId: Guid.NewGuid(),
            createdBy: Actor,
            occurredOn: Now);
        task.Assign([Assignee], Actor, Now);
        var assigned = task.DomainEvents.OfType<WorkflowTaskAssignedEvent>().Single();

        var captured = new List<WorkflowTaskAssigned>();
        var endpoint = Substitute.For<IPublishEndpoint>();
        endpoint.When(x => x.Publish(Arg.Any<WorkflowTaskAssigned>(), Arg.Any<CancellationToken>()))
            .Do(call => captured.Add(call.Arg<WorkflowTaskAssigned>()));

        await new WorkflowIntegrationEventPublisher<WorkflowTaskAssignedEvent>(endpoint)
            .Handle(assigned, CancellationToken.None);

        var published = captured.Single();
        published.AssigneeIds.Should().Equal(Assignee);
        published.AssignedBy.Should().Be(Actor);
    }

    [Fact]
    public async Task WorkflowTaskStarted_publishes_actor_id_only()
    {
        var task = CreateAssignedTask();
        var started = task.DomainEvents.OfType<WorkflowTaskStartedEvent>().Single();

        var captured = new List<WorkflowTaskStarted>();
        var endpoint = Substitute.For<IPublishEndpoint>();
        endpoint.When(x => x.Publish(Arg.Any<WorkflowTaskStarted>(), Arg.Any<CancellationToken>()))
            .Do(call => captured.Add(call.Arg<WorkflowTaskStarted>()));

        await new WorkflowIntegrationEventPublisher<WorkflowTaskStartedEvent>(endpoint)
            .Handle(started, CancellationToken.None);

        var published = captured.Single();
        published.StartedBy.Should().Be(Assignee);
    }

    [Fact]
    public async Task WorkflowTaskCompleted_publishes_outcome_and_no_notes()
    {
        const string completionNote = "Confidential reviewer note.";
        var task = CreateAssignedTask();
        task.Complete("verified", completionNote, ["verified", "rejected"], Assignee, adminOverride: false, Now);
        var completed = task.DomainEvents.OfType<WorkflowTaskCompletedEvent>().Single();

        var captured = new List<WorkflowTaskCompleted>();
        var endpoint = Substitute.For<IPublishEndpoint>();
        endpoint.When(x => x.Publish(Arg.Any<WorkflowTaskCompleted>(), Arg.Any<CancellationToken>()))
            .Do(call => captured.Add(call.Arg<WorkflowTaskCompleted>()));

        await new WorkflowIntegrationEventPublisher<WorkflowTaskCompletedEvent>(endpoint)
            .Handle(completed, CancellationToken.None);

        var published = captured.Single();
        published.Outcome.Should().Be("verified");
        published.CompletedBy.Should().Be(Assignee);
        published.ToString().Should().NotContain(completionNote);
    }

    [Fact]
    public async Task WorkflowTaskEscalated_publishes_target_and_no_reason()
    {
        const string reason = "Confidential escalation reason.";
        var task = CreateAssignedTask();
        task.Escalate(Guid.NewGuid(), reason, Actor, Now);
        var escalated = task.DomainEvents.OfType<WorkflowTaskEscalatedEvent>().Single();

        var captured = new List<WorkflowTaskEscalated>();
        var endpoint = Substitute.For<IPublishEndpoint>();
        endpoint.When(x => x.Publish(Arg.Any<WorkflowTaskEscalated>(), Arg.Any<CancellationToken>()))
            .Do(call => captured.Add(call.Arg<WorkflowTaskEscalated>()));

        await new WorkflowIntegrationEventPublisher<WorkflowTaskEscalatedEvent>(endpoint)
            .Handle(escalated, CancellationToken.None);

        var published = captured.Single();
        published.EscalatedTo.Should().ContainSingle();
        published.EscalatedBy.Should().Be(Actor);
        published.ToString().Should().NotContain(reason);
    }
}