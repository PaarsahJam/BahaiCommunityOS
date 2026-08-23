using CommunityOS.Correspondence.Domain;
using CommunityOS.Contracts.Organization;
using CommunityOS.Correspondence.Infrastructure.Persistence;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CommunityOS.Correspondence.Infrastructure.Integration;

/// <summary>
/// Maintains the local organization-unit reference projection (ADR-016).
/// Projection-only: scope checks need the stable unit id; authorization is
/// always resolved live by the Authorization service. Consumed but never
/// journaled as letters.
/// </summary>
public sealed class OrganizationUnitProjectionConsumer(
    CorrespondenceDbContext db,
    ILogger<OrganizationUnitProjectionConsumer> logger) :
    IConsumer<OrganizationUnitCreated>,
    IConsumer<OrganizationUnitUpdated>,
    IConsumer<OrganizationUnitParentChanged>
{
    public Task Consume(ConsumeContext<OrganizationUnitCreated> context) =>
        UpsertAsync(context.Message.OrganizationUnitId, context.Message.ParentId,
            context.Message.OccurredOn, context.CancellationToken);

    public Task Consume(ConsumeContext<OrganizationUnitUpdated> context) =>
        UpsertAsync(context.Message.OrganizationUnitId, null,
            context.Message.OccurredOn, context.CancellationToken);

    public Task Consume(ConsumeContext<OrganizationUnitParentChanged> context) =>
        UpsertAsync(context.Message.OrganizationUnitId, context.Message.ParentId,
            context.Message.OccurredOn, context.CancellationToken);

    private async Task UpsertAsync(Guid id, Guid? parent, DateTime occurredOn, CancellationToken ct)
    {
        var row = await db.OrganizationUnitReferences.FindAsync([id], ct);
        if (row is null)
        {
            db.OrganizationUnitReferences.Add(OrganizationUnitReference.Create(id, parent, occurredOn));
        }
        else if (!row.Apply(parent, occurredOn))
        {
            return;
        }

        await db.SaveChangesAsync(ct);
        logger.ProjectionApplied(id);
    }
}

internal static partial class CorrespondenceConsumersLog
{
    [LoggerMessage(
        EventId = 0,
        Level = LogLevel.Information,
        Message = "Organization unit reference projected for {OrganizationUnitId}.")]
    public static partial void ProjectionApplied(this ILogger logger, Guid organizationUnitId);
}
