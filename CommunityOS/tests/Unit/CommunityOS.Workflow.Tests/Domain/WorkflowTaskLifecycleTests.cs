using CommunityOS.Workflow.Domain.Aggregates;
using CommunityOS.Workflow.Domain.Enumerations;
using CommunityOS.Workflow.Domain.Exceptions;
using CommunityOS.Workflow.Domain.Events;

namespace CommunityOS.Workflow.Tests.Domain;

/// <summary>
/// Domain-invariant tests for the WorkflowTask aggregate (ADR-024): the
/// guarded lifecycle <c>Created → Assigned → In Progress → Completed |
/// Cancelled</c>, assignee-only start/complete with an administrative override,
/// definition-gated outcomes, escalation as a reassign that keeps the lifecycle
/// status, idempotent reconciliation completion, and sensitive-note handling.
/// </summary>
public class WorkflowTaskLifecycleTests
{
    private static readonly Guid Actor = Guid.NewGuid();
    private static readonly Guid Assignee = Guid.NewGuid();
    private static readonly Guid Other = Guid.NewGuid();
    private static readonly Guid Originator = Guid.NewGuid();
    private static readonly DateTime Now = DateTime.UtcNow;

    private static WorkflowTask CreateTask(
        params Guid[] assignees) =>
        WorkflowTask.Create(
            WorkflowDefinitionCodes.RecordReview,
            WorkflowDomainTypes.Record,
            Guid.NewGuid(),
            organizationUnitId: Guid.NewGuid(),
            additionalScopes: [],
            assigneeIds: assignees,
            dueOn: null,
            notes: null,
            originatorId: Originator,
            createdBy: Actor,
            occurredOn: Now);

    // --- Creation ---

    [Fact]
    public void Create_without_assignees_starts_as_Created()
    {
        var task = CreateTask();

        task.Status.Should().Be(WorkflowStatus.Created);
        task.AssigneeIds.Should().BeEmpty();
        task.DomainEvents.Should().ContainSingle(e => e is WorkflowTaskCreatedEvent);
    }

    [Fact]
    public void Create_with_assignees_starts_as_Assigned()
    {
        var task = CreateTask(Assignee);

        task.Status.Should().Be(WorkflowStatus.Assigned);
        task.AssigneeIds.Should().Equal(Assignee);
        task.Assignments.Should().ContainSingle();
    }

    [Fact]
    public void Create_requires_domain_entity_id_for_domain_bound_types()
    {
        var act = () => WorkflowTask.Create(
            WorkflowDefinitionCodes.RecordReview,
            WorkflowDomainTypes.Record,
            domainEntityId: null,
            organizationUnitId: null,
            additionalScopes: [],
            assigneeIds: [],
            dueOn: null,
            notes: null,
            Originator,
            Actor,
            Now);

        act.Should().Throw<TaskDomainEntityIdRequiredException>();
    }

    [Fact]
    public void Create_general_task_does_not_require_domain_entity_id()
    {
        var task = WorkflowTask.Create(
            WorkflowDefinitionCodes.General,
            WorkflowDomainTypes.General,
            domainEntityId: null,
            organizationUnitId: null,
            additionalScopes: [],
            assigneeIds: [],
            dueOn: null,
            notes: null,
            Originator,
            Actor,
            Now);

        task.DomainEntityId.Should().BeNull();
        task.Status.Should().Be(WorkflowStatus.Created);
    }

    [Fact]
    public void Create_rejects_unknown_domain_type()
    {
        var act = () => WorkflowTask.Create(
            "record-review", "unknown-domain", Guid.NewGuid(), null, [], [], null, null,
            Originator, Actor, Now);

        act.Should().Throw<InvalidTaskDomainTypeException>();
    }

    // --- Lifecycle ---

    [Fact]
    public void Assign_then_Start_then_Complete_walks_the_lifecycle()
    {
        var task = CreateTask(Assignee);

        task.Start(Assignee, adminOverride: false, Now);
        task.Status.Should().Be(WorkflowStatus.InProgress);
        task.StartedBy.Should().Be(Assignee);

        task.Complete("verified", notes: null, ["verified", "rejected"], Assignee, adminOverride: false, Now);
        task.Status.Should().Be(WorkflowStatus.Completed);
        task.Outcome.Should().Be("verified");
        task.CompletedBy.Should().Be(Assignee);
        task.DomainEvents.Should().Contain(e => e is WorkflowTaskStartedEvent);
        task.DomainEvents.Should().Contain(e => e is WorkflowTaskCompletedEvent);
    }

    [Fact]
    public void Non_assignee_cannot_start()
    {
        var task = CreateTask(Assignee);

        var act = () => task.Start(Other, adminOverride: false, Now);

        act.Should().Throw<TaskNotAssignableToActorException>();
    }

    [Fact]
    public void Non_assignee_can_start_with_administrative_override()
    {
        var task = CreateTask(Assignee);

        task.Start(Other, adminOverride: true, Now);

        task.Status.Should().Be(WorkflowStatus.InProgress);
        task.StartedBy.Should().Be(Other);
    }

    [Fact]
    public void Complete_requires_permitted_outcome()
    {
        var task = CreateTask(Assignee);
        task.Start(Assignee, adminOverride: false, Now);

        var act = () => task.Complete(
            "not-an-outcome", notes: null, ["verified", "rejected"], Assignee, adminOverride: false, Now);

        act.Should().Throw<InvalidWorkflowOutcomeException>();
    }

    [Fact]
    public void Complete_from_Created_is_invalid()
    {
        var task = CreateTask(Assignee);

        var act = () => task.Complete(
            "verified", notes: null, ["verified", "rejected"], Assignee, adminOverride: true, Now);

        act.Should().Throw<InvalidWorkflowTaskTransitionException>();
    }

    [Fact]
    public void Completed_tasks_are_immutable()
    {
        var task = CreateTask(Assignee);
        task.Start(Assignee, adminOverride: false, Now);
        task.Complete("verified", null, ["verified", "rejected"], Assignee, false, Now);

        var act = () => task.Cancel(Actor, Now);

        act.Should().Throw<InvalidWorkflowTaskTransitionException>();
    }

    [Fact]
    public void Cancel_is_legal_from_any_non_terminal_state()
    {
        var task = CreateTask(Assignee);

        task.Cancel(Actor, Now);

        task.Status.Should().Be(WorkflowStatus.Cancelled);
        task.IsTerminal.Should().BeTrue();
    }

    [Fact]
    public void Escalate_reassigns_without_changing_lifecycle_status()
    {
        var task = CreateTask(Assignee);
        task.Start(Assignee, adminOverride: false, Now);

        task.Escalate(Other, "needs a second reviewer", Actor, Now);

        task.Status.Should().Be(WorkflowStatus.InProgress);
        task.AssigneeIds.Should().Equal(Other);
        task.EscalatedTo.Should().Be(Other);
        task.EscalationReason.Should().Be("needs a second reviewer");
        task.DomainEvents.Should().Contain(e => e is WorkflowTaskEscalatedEvent);
    }

    [Fact]
    public void Reassignment_is_legal_from_Assigned_and_InProgress()
    {
        var task = CreateTask(Assignee);
        task.Start(Assignee, adminOverride: false, Now);

        task.Assign([Other], Actor, Now);

        task.Status.Should().Be(WorkflowStatus.Assigned);
        task.AssigneeIds.Should().Equal(Other);
    }

    // --- Reconciliation ---

    [Fact]
    public void CompleteForReconciliation_bypasses_checks_and_is_idempotent()
    {
        var task = CreateTask();
        task.CompleteForReconciliation("verified", SystemReconciler, Now);

        task.Status.Should().Be(WorkflowStatus.Completed);
        task.Outcome.Should().Be("verified");
        var eventCount = task.DomainEvents.Count(e => e is WorkflowTaskCompletedEvent);

        task.CompleteForReconciliation("rejected", SystemReconciler, Now);

        task.Status.Should().Be(WorkflowStatus.Completed);
        task.Outcome.Should().Be("verified");
        task.DomainEvents.Count(e => e is WorkflowTaskCompletedEvent).Should().Be(eventCount);
    }

    private static readonly Guid SystemReconciler = Guid.Parse("00000000-0000-0000-0000-000000000002");

    // --- Sensitive notes ---

    [Fact]
    public void Notes_are_sensitive_and_appended_to_activity()
    {
        var task = CreateTask(Assignee);
        task.Start(Assignee, adminOverride: false, Now);

        task.Complete("verified", "Sensitive completion note.", ["verified"], Assignee, false, Now);

        task.Notes.Should().Be("Sensitive completion note.");
        task.Activity.Should().Contain(a => a.Notes == "Sensitive completion note.");
    }

    // --- Overdue ---

    [Fact]
    public void IsOverdue_is_true_when_past_due_and_not_terminal()
    {
        var task = WorkflowTask.Create(
            WorkflowDefinitionCodes.RecordReview,
            WorkflowDomainTypes.Record,
            Guid.NewGuid(),
            null, [], [], dueOn: Now.AddDays(-1), notes: null, Originator, Actor, Now);

        task.IsOverdue(Now).Should().BeTrue();

        task.Cancel(Actor, Now);
        task.IsOverdue(Now).Should().BeFalse();
    }
}