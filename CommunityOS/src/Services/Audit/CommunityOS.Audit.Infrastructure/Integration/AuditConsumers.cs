using CommunityOS.Audit.Application;
using CommunityOS.Audit.Domain;
using CommunityOS.Audit.Infrastructure.Persistence;
using CommunityOS.Contracts.Notifications;
using CommunityOS.Contracts.Organization;
using CommunityOS.Contracts.Records;
using CommunityOS.Contracts.Workflow;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CommunityOS.Audit.Infrastructure.Integration;

/// <summary>
/// Journals the ratified Records lifecycle facts (ADR-027 decision 9). Every
/// consume maps through the pure ingest mapping; persistence is idempotent by
/// source-event hash and exactly-once via the transactional inbox.
/// </summary>
public sealed class RecordsAuditConsumer(IAuditIngestor ingestor, ILogger<RecordsAuditConsumer> logger) :
    IConsumer<RecordCreated>,
    IConsumer<RecordSubmitted>,
    IConsumer<RecordUnderReview>,
    IConsumer<RecordVerified>,
    IConsumer<RecordRejected>,
    IConsumer<RecordCorrected>,
    IConsumer<RecordArchived>,
    IConsumer<RecordDeactivated>,
    IConsumer<RecordRestored>,
    IConsumer<RecordClassified>,
    IConsumer<RecordHoldPlaced>,
    IConsumer<RecordHoldReleased>,
    IConsumer<RecordRetentionChanged>,
    IConsumer<RecordEvidenceAttached>,
    IConsumer<RecordEvidenceRemoved>,
    IConsumer<RecordRetentionExpired>
{
    public Task Consume(ConsumeContext<RecordCreated> context) => Ingest(context, AuditEventMapper.Map(context.Message));
    public Task Consume(ConsumeContext<RecordSubmitted> context) => Ingest(context, AuditEventMapper.Map(context.Message));
    public Task Consume(ConsumeContext<RecordUnderReview> context) => Ingest(context, AuditEventMapper.Map(context.Message));
    public Task Consume(ConsumeContext<RecordVerified> context) => Ingest(context, AuditEventMapper.Map(context.Message));
    public Task Consume(ConsumeContext<RecordRejected> context) => Ingest(context, AuditEventMapper.Map(context.Message));
    public Task Consume(ConsumeContext<RecordCorrected> context) => Ingest(context, AuditEventMapper.Map(context.Message));
    public Task Consume(ConsumeContext<RecordArchived> context) => Ingest(context, AuditEventMapper.Map(context.Message));
    public Task Consume(ConsumeContext<RecordDeactivated> context) => Ingest(context, AuditEventMapper.Map(context.Message));
    public Task Consume(ConsumeContext<RecordRestored> context) => Ingest(context, AuditEventMapper.Map(context.Message));
    public Task Consume(ConsumeContext<RecordClassified> context) => Ingest(context, AuditEventMapper.Map(context.Message));
    public Task Consume(ConsumeContext<RecordHoldPlaced> context) => Ingest(context, AuditEventMapper.Map(context.Message));
    public Task Consume(ConsumeContext<RecordHoldReleased> context) => Ingest(context, AuditEventMapper.Map(context.Message));
    public Task Consume(ConsumeContext<RecordRetentionChanged> context) => Ingest(context, AuditEventMapper.Map(context.Message));
    public Task Consume(ConsumeContext<RecordEvidenceAttached> context) => Ingest(context, AuditEventMapper.Map(context.Message));
    public Task Consume(ConsumeContext<RecordEvidenceRemoved> context) => Ingest(context, AuditEventMapper.Map(context.Message));
    public Task Consume(ConsumeContext<RecordRetentionExpired> context) => Ingest(context, AuditEventMapper.Map(context.Message));

    private async Task Ingest<T>(ConsumeContext<T> context, IngestCandidate candidate)
        where T : class
    {
        var outcome = await ingestor.IngestAsync(candidate, context.CancellationToken);
        logger.EntryIngested(outcome.ToString(), candidate.SourceEventType, candidate.ResourceId);
    }
}

/// <summary>Journals the ratified workflow task lifecycle facts.</summary>
public sealed class WorkflowAuditConsumer(IAuditIngestor ingestor, ILogger<WorkflowAuditConsumer> logger) :
    IConsumer<WorkflowTaskCreated>,
    IConsumer<WorkflowTaskAssigned>,
    IConsumer<WorkflowTaskCompleted>,
    IConsumer<WorkflowTaskCancelled>,
    IConsumer<WorkflowTaskEscalated>
{
    public Task Consume(ConsumeContext<WorkflowTaskCreated> context) => Ingest(context, AuditEventMapper.Map(context.Message));
    public Task Consume(ConsumeContext<WorkflowTaskAssigned> context) => Ingest(context, AuditEventMapper.Map(context.Message));
    public Task Consume(ConsumeContext<WorkflowTaskCompleted> context) => Ingest(context, AuditEventMapper.Map(context.Message));
    public Task Consume(ConsumeContext<WorkflowTaskCancelled> context) => Ingest(context, AuditEventMapper.Map(context.Message));
    public Task Consume(ConsumeContext<WorkflowTaskEscalated> context) => Ingest(context, AuditEventMapper.Map(context.Message));

    private async Task Ingest<T>(ConsumeContext<T> context, IngestCandidate candidate)
        where T : class
    {
        var outcome = await ingestor.IngestAsync(candidate, context.CancellationToken);
        logger.EntryIngested(outcome.ToString(), candidate.SourceEventType, candidate.ResourceId);
    }
}

/// <summary>Journals notification dispatch completions (counts only — ADR-025).</summary>
public sealed class NotificationsAuditConsumer(IAuditIngestor ingestor, ILogger<NotificationsAuditConsumer> logger) :
    IConsumer<NotificationDispatched>
{
    public async Task Consume(ConsumeContext<NotificationDispatched> context)
    {
        var candidate = AuditEventMapper.Map(context.Message);
        var outcome = await ingestor.IngestAsync(candidate, context.CancellationToken);
        logger.EntryIngested(outcome.ToString(), candidate.SourceEventType, candidate.ResourceId);
    }
}

/// <summary>
/// Maintains the local organization-unit reference projection used to resolve
/// unit hierarchy for scope display (ADR-016). Consumed but not journaled.
/// </summary>
public sealed class OrganizationUnitProjectionConsumer(AuditDbContext db, ILogger<OrganizationUnitProjectionConsumer> logger) :
    IConsumer<OrganizationUnitCreated>, IConsumer<OrganizationUnitUpdated>, IConsumer<OrganizationUnitParentChanged>
{
    public Task Consume(ConsumeContext<OrganizationUnitCreated> context) =>
        UpsertAsync(context.Message.OrganizationUnitId, context.Message.ParentId, context.Message.OccurredOn, context.CancellationToken);

    public Task Consume(ConsumeContext<OrganizationUnitUpdated> context) =>
        UpsertAsync(context.Message.OrganizationUnitId, null, context.Message.OccurredOn, context.CancellationToken);

    public Task Consume(ConsumeContext<OrganizationUnitParentChanged> context) =>
        UpsertAsync(context.Message.OrganizationUnitId, context.Message.ParentId, context.Message.OccurredOn, context.CancellationToken);

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

internal static partial class AuditConsumersLog
{
    [LoggerMessage(
        EventId = 0,
        Level = LogLevel.Information,
        Message = "Audit entry {Outcome} for {SourceType} {SourceId}.")]
    public static partial void EntryIngested(this ILogger logger, string outcome, string sourceType, Guid sourceId);

    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Information,
        Message = "Organization unit reference projected for {OrganizationUnitId}.")]
    public static partial void ProjectionApplied(this ILogger logger, Guid organizationUnitId);
}
