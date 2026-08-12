using CommunityOS.Community.Domain.Aggregates;
using CommunityOS.Community.Domain.Repositories;
using CommunityOS.Contracts.Organization;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace CommunityOS.Community.Infrastructure.Integration.Organization;

/// <summary>
/// Keeps the Community-side read-model of organization facts in sync by
/// consuming Organization integration events. Community does not own the
/// organizational hierarchy; it only references it (see ADR-016).
/// </summary>
public sealed class OrganizationIntegrationEventConsumer(
    IOrganizationReferenceRepository repository,
    ILogger<OrganizationIntegrationEventConsumer> logger) :
    IConsumer<OrganizationCreated>,
    IConsumer<OrganizationUpdated>,
    IConsumer<OrganizationUnitCreated>,
    IConsumer<OrganizationUnitUpdated>,
    IConsumer<OrganizationUnitParentChanged>
{
    public async Task Consume(ConsumeContext<OrganizationCreated> context)
    {
        var message = context.Message;
        await UpsertOrganizationAsync(
            message.OrganizationId,
            message.Name,
            message.OrganizationType,
            message.JurisdictionType,
            message.JurisdictionScopeId,
            message.OccurredOn,
            context.CancellationToken);

        logger.OrganizationReferenceCreated(message.OrganizationId);
    }

    public async Task Consume(ConsumeContext<OrganizationUpdated> context)
    {
        var message = context.Message;
        var existing = await repository.GetByIdAsync(message.OrganizationId, context.CancellationToken);
        if (existing is null)
        {
            await UpsertOrganizationAsync(
                message.OrganizationId,
                message.Name,
                message.OrganizationType,
                message.JurisdictionType,
                message.JurisdictionScopeId,
                message.OccurredOn,
                context.CancellationToken);
            logger.OrganizationReferenceCreated(message.OrganizationId);
            return;
        }

        existing.Sync(
            message.Name,
            message.OrganizationType,
            message.JurisdictionType,
            message.JurisdictionScopeId,
            message.OccurredOn);
        await repository.UpsertAsync(existing, context.CancellationToken);

        logger.OrganizationReferenceUpdated(message.OrganizationId);
    }

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

    private async Task UpsertOrganizationAsync(
        Guid organizationId,
        string name,
        string organizationType,
        string jurisdictionType,
        Guid? jurisdictionScopeId,
        DateTime occurredOn,
        CancellationToken ct)
    {
        var existing = await repository.GetByIdAsync(organizationId, ct);
        if (existing is null)
        {
            await repository.UpsertAsync(
                OrganizationReference.Create(
                    organizationId,
                    name,
                    organizationType,
                    jurisdictionType,
                    jurisdictionScopeId,
                    occurredOn),
                ct);
            return;
        }

        existing.Sync(name, organizationType, jurisdictionType, jurisdictionScopeId, occurredOn);
        await repository.UpsertAsync(existing, ct);
    }
}

internal static partial class OrganizationIntegrationEventConsumerLogging
{
    [LoggerMessage(
        EventId = 0,
        Level = LogLevel.Information,
        Message = "Organization reference created for organization {OrganizationId}.")]
    public static partial void OrganizationReferenceCreated(this ILogger logger, Guid organizationId);

    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Information,
        Message = "Organization reference updated for organization {OrganizationId}.")]
    public static partial void OrganizationReferenceUpdated(this ILogger logger, Guid organizationId);

    [LoggerMessage(
        EventId = 2,
        Level = LogLevel.Information,
        Message = "Organization unit reference created for unit {OrganizationUnitId}.")]
    public static partial void OrganizationUnitReferenceCreated(this ILogger logger, Guid organizationUnitId);

    [LoggerMessage(
        EventId = 3,
        Level = LogLevel.Information,
        Message = "Organization unit reference updated for unit {OrganizationUnitId}.")]
    public static partial void OrganizationUnitReferenceUpdated(this ILogger logger, Guid organizationUnitId);

    [LoggerMessage(
        EventId = 4,
        Level = LogLevel.Information,
        Message = "Organization unit reference parent updated for unit {OrganizationUnitId}.")]
    public static partial void OrganizationUnitReferenceParentUpdated(this ILogger logger, Guid organizationUnitId);

    [LoggerMessage(
        EventId = 5,
        Level = LogLevel.Warning,
        Message = "Organization unit reference {OrganizationUnitId} not found; event ignored.")]
    public static partial void OrganizationUnitReferenceNotFound(this ILogger logger, Guid organizationUnitId);
}
