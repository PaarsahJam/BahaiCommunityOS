using CommunityOS.Contracts.Organization;
using CommunityOS.Knowledge.Domain.Aggregates;
using CommunityOS.Knowledge.Domain.Repositories;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace CommunityOS.Knowledge.Infrastructure.Integration.Organization;

/// <summary>
/// Keeps the Knowledge-side read-model of organization unit facts in sync by
/// consuming Organization integration events. Knowledge only scopes community
/// content (questions, answers, discussions) to units, so only unit events are
/// consumed — never organization-level facts. Knowledge does not own the
/// organizational hierarchy; it only references it (see ADR-016).
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
        var reference = OrganizationUnitReference.Create(
            message.OrganizationUnitId,
            message.OrganizationId,
            message.Name,
            message.UnitType,
            message.ParentId,
            message.OccurredOn);

        await repository.UpsertUnitAsync(reference, context.CancellationToken);
        logger.OrganizationUnitReferenceCreated(message.OrganizationUnitId);
    }

    public async Task Consume(ConsumeContext<OrganizationUnitUpdated> context)
    {
        var message = context.Message;
        var existing = await repository.GetUnitByIdAsync(message.OrganizationUnitId, context.CancellationToken);
        if (existing is null)
        {
            logger.OrganizationUnitReferenceNotFound(message.OrganizationUnitId);
            return;
        }

        existing.Sync(message.Name, message.UnitType, existing.ParentId, message.OccurredOn);
        await repository.UpsertUnitAsync(existing, context.CancellationToken);

        logger.OrganizationUnitReferenceUpdated(message.OrganizationUnitId);
    }

    public async Task Consume(ConsumeContext<OrganizationUnitParentChanged> context)
    {
        var message = context.Message;
        var existing = await repository.GetUnitByIdAsync(message.OrganizationUnitId, context.CancellationToken);
        if (existing is null)
        {
            logger.OrganizationUnitReferenceNotFound(message.OrganizationUnitId);
            return;
        }

        existing.Sync(existing.Name, existing.UnitType, message.ParentId, message.OccurredOn);
        await repository.UpsertUnitAsync(existing, context.CancellationToken);

        logger.OrganizationUnitReferenceParentUpdated(message.OrganizationUnitId);
    }
}

internal static partial class OrganizationIntegrationEventConsumerLogging
{
    [LoggerMessage(
        EventId = 0,
        Level = LogLevel.Information,
        Message = "Organization unit reference created for unit {OrganizationUnitId}.")]
    public static partial void OrganizationUnitReferenceCreated(this ILogger logger, Guid organizationUnitId);

    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Information,
        Message = "Organization unit reference updated for unit {OrganizationUnitId}.")]
    public static partial void OrganizationUnitReferenceUpdated(this ILogger logger, Guid organizationUnitId);

    [LoggerMessage(
        EventId = 2,
        Level = LogLevel.Information,
        Message = "Organization unit reference parent updated for unit {OrganizationUnitId}.")]
    public static partial void OrganizationUnitReferenceParentUpdated(this ILogger logger, Guid organizationUnitId);

    [LoggerMessage(
        EventId = 3,
        Level = LogLevel.Warning,
        Message = "Organization unit reference {OrganizationUnitId} not found; event ignored.")]
    public static partial void OrganizationUnitReferenceNotFound(this ILogger logger, Guid organizationUnitId);
}