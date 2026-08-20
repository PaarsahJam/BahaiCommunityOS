using CommunityOS.Contracts.Organization;
using CommunityOS.Workflow.Domain.Aggregates;
using CommunityOS.Workflow.Domain.Repositories;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace CommunityOS.Workflow.Infrastructure.Integration.Organization;

/// <summary>
/// Maintains the Workflow-side read-model of organization unit references
/// (ADR-016). Workflow scopes tasks to units, so only unit events are consumed
/// — never organization-level facts. The projection stores only the stable unit
/// id (scope checks never need name/hierarchy); authorization is resolved live
/// by the Authorization service, never from this read model.
/// </summary>
public sealed class OrganizationIntegrationEventConsumer(
    IOrganizationUnitReferenceRepository repository,
    ILogger<OrganizationIntegrationEventConsumer> logger) :
    IConsumer<OrganizationUnitCreated>,
    IConsumer<OrganizationUnitUpdated>,
    IConsumer<OrganizationUnitParentChanged>
{
    public async Task Consume(ConsumeContext<OrganizationUnitCreated> context)
    {
        var message = context.Message;
        if (await repository.ExistsAsync(message.OrganizationUnitId, context.CancellationToken))
            return;

        await repository.AddAsync(
            OrganizationUnitReference.Create(message.OrganizationUnitId, message.OccurredOn),
            context.CancellationToken);
        logger.OrganizationUnitReferenceCreated(message.OrganizationUnitId);
    }

    public async Task Consume(ConsumeContext<OrganizationUnitUpdated> context)
    {
        var message = context.Message;
        if (!await repository.ExistsAsync(message.OrganizationUnitId, context.CancellationToken))
            await repository.AddAsync(
                OrganizationUnitReference.Create(message.OrganizationUnitId, message.OccurredOn),
                context.CancellationToken);
    }

    public async Task Consume(ConsumeContext<OrganizationUnitParentChanged> context)
    {
        var message = context.Message;
        if (!await repository.ExistsAsync(message.OrganizationUnitId, context.CancellationToken))
            await repository.AddAsync(
                OrganizationUnitReference.Create(message.OrganizationUnitId, message.OccurredOn),
                context.CancellationToken);
    }
}

internal static partial class WorkflowOrganizationIntegrationEventConsumerLogging
{
    [LoggerMessage(
        EventId = 0,
        Level = LogLevel.Information,
        Message = "Organization unit reference created for unit {OrganizationUnitId}.")]
    public static partial void OrganizationUnitReferenceCreated(this ILogger logger, Guid organizationUnitId);
}