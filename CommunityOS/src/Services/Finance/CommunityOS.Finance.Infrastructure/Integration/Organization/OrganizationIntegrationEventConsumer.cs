using CommunityOS.Contracts.Organization;
using CommunityOS.Finance.Domain.Aggregates;
using CommunityOS.Finance.Domain.Repositories;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace CommunityOS.Finance.Infrastructure.Integration.Organization;

/// <summary>
/// Maintains the Finance-side read-model of organization unit scopes
/// (ADR-016/032). Funds are scoped to units, so only unit events are consumed
/// — never organization-level facts or hierarchy. The projection stores the
/// stable unit id and its current display name; authorization is resolved live
/// by the Authorization service, never from this read model. A unit that has
/// not yet been projected cannot own a fund (creation fails closed).
/// </summary>
public sealed class OrganizationIntegrationEventConsumer(
    IFinanceOrganizationUnitReferenceRepository repository,
    ILogger<OrganizationIntegrationEventConsumer> logger) :
    IConsumer<OrganizationUnitCreated>,
    IConsumer<OrganizationUnitUpdated>
{
    public async Task Consume(ConsumeContext<OrganizationUnitCreated> context)
    {
        var message = context.Message;
        if (await repository.ExistsAsync(message.OrganizationUnitId, context.CancellationToken))
            return;

        await repository.AddAsync(
            OrganizationUnitReference.Create(
                Guid.NewGuid(),
                message.OrganizationUnitId,
                message.Name),
            context.CancellationToken);
        logger.OrganizationUnitReferenceCreated(message.OrganizationUnitId);
    }

    public async Task Consume(ConsumeContext<OrganizationUnitUpdated> context)
    {
        var message = context.Message;
        var existing = await repository.GetByOrganizationUnitIdAsync(
            message.OrganizationUnitId, context.CancellationToken);

        if (existing is null)
        {
            await repository.AddAsync(
                OrganizationUnitReference.Create(
                    Guid.NewGuid(),
                    message.OrganizationUnitId,
                    message.Name),
                context.CancellationToken);
            logger.OrganizationUnitReferenceCreated(message.OrganizationUnitId);
            return;
        }

        if (string.Equals(existing.DisplayName, message.Name, StringComparison.Ordinal))
            return;

        existing.UpdateDisplayName(message.Name);
        await repository.UpdateAsync(existing, context.CancellationToken);
        logger.OrganizationUnitReferenceUpdated(message.OrganizationUnitId);
    }
}

internal static partial class FinanceOrganizationIntegrationEventConsumerLogging
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
}