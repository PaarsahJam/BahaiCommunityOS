using CommunityOS.Workflow.Application.DTOs;
using CommunityOS.Workflow.Domain.Aggregates;

namespace CommunityOS.Workflow.Application;

internal static class WorkflowMappingExtensions
{
    internal static WorkflowTaskSummaryDto ToSummaryDto(this WorkflowTask t, DateTime asOf) =>
        new(t.Id,
            t.DefinitionCode,
            t.DomainType,
            t.DomainEntityId,
            t.Status.Name,
            t.OrganizationUnitId,
            t.AssigneeIds,
            t.DueOn,
            t.IsOverdue(asOf),
            t.Outcome,
            t.UpdatedOn);

    internal static WorkflowTaskDto ToDto(this WorkflowTask t, DateTime asOf) =>
        new(t.Id,
            t.DefinitionCode,
            t.DomainType,
            t.DomainEntityId,
            t.Status.Name,
            t.OrganizationUnitId,
            t.AllOrganizationUnitIds.Except(
                    t.OrganizationUnitId is { } organizationUnitId
                        ? new[] { organizationUnitId }
                        : Array.Empty<Guid>())
                .ToArray()
                .AsReadOnly(),
            t.AssigneeIds,
            t.DueOn,
            t.IsOverdue(asOf),
            t.EscalatedTo,
            t.EscalatedBy,
            t.EscalatedOn,
            t.Outcome,
            t.OriginatorId,
            t.CreatedBy,
            t.CreatedOn,
            t.UpdatedBy,
            t.UpdatedOn,
            t.StartedBy,
            t.StartedOn,
            t.CompletedBy,
            t.CompletedOn);

    internal static WorkflowTaskSensitiveFieldsDto ToSensitiveFieldsDto(this WorkflowTask t) =>
        new(t.Id, t.Notes, t.EscalationReason);

    internal static TaskDefinitionDto ToDto(this TaskDefinition d) =>
        new(d.Code,
            d.DisplayName,
            d.Description,
            d.DomainType,
            d.PermittedOutcomeCodes,
            d.DueIn,
            d.RequiresHumanReview,
            d.IsActive,
            d.CreatedBy,
            d.CreatedOn,
            d.UpdatedBy,
            d.UpdatedOn);

    internal static TaskActivityDto ToDto(this TaskActivity a, Guid taskId) =>
        new(a.Id, taskId, a.Action, a.ActorId, a.OccurredOn, a.Outcome);
}