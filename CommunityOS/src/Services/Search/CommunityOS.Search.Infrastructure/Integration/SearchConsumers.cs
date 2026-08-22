using CommunityOS.Contracts.Documents;
using CommunityOS.Contracts.Knowledge;
using CommunityOS.Contracts.Organization;
using CommunityOS.Contracts.Records;
using CommunityOS.Contracts.Workflow;
using CommunityOS.Search.Domain;
using CommunityOS.Search.Infrastructure.Persistence;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace CommunityOS.Search.Infrastructure.Integration;

/// <summary>
/// Projects Records integration events into the search index (ADR-026).
/// DisplayTitle is the category code only — subject names and other sensitive
/// text fields are never indexed.
/// </summary>
public sealed class RecordsIndexConsumer(ISearchProjectionWriter writer, ILogger<RecordsIndexConsumer> logger) :
    IConsumer<RecordCreated>, IConsumer<RecordClassified>, IConsumer<RecordArchived>, IConsumer<RecordDeactivated>, IConsumer<RecordRestored>
{
    public async Task Consume(ConsumeContext<RecordCreated> context)
    {
        await writer.UpsertAsync(SearchSourceTypes.Record, context.Message.RecordId, context.Message.Category, context.Message.Category,
            context.Message.Status, null, context.Message.OrganizationUnitId, true, context.Message.OccurredOn, true, context.CancellationToken);
        logger.ProjectionApplied("record.created", SearchSourceTypes.Record, context.Message.RecordId);
    }

    public async Task Consume(ConsumeContext<RecordClassified> context)
    {
        await writer.UpsertAsync(SearchSourceTypes.Record, context.Message.RecordId, null, null, null,
            context.Message.IsSensitive, null, false, context.Message.OccurredOn, false, context.CancellationToken);
        logger.ProjectionApplied("record.classified", SearchSourceTypes.Record, context.Message.RecordId);
    }

    public async Task Consume(ConsumeContext<RecordArchived> context)
    {
        await writer.UpsertAsync(SearchSourceTypes.Record, context.Message.RecordId, null, null, "Archived",
            null, null, false, context.Message.OccurredOn, false, context.CancellationToken);
        logger.ProjectionApplied("record.archived", SearchSourceTypes.Record, context.Message.RecordId);
    }

    public async Task Consume(ConsumeContext<RecordDeactivated> context)
    {
        await writer.UpsertAsync(SearchSourceTypes.Record, context.Message.RecordId, null, null, "Deactivated",
            null, null, false, context.Message.OccurredOn, false, context.CancellationToken);
        logger.ProjectionApplied("record.deactivated", SearchSourceTypes.Record, context.Message.RecordId);
    }

    public async Task Consume(ConsumeContext<RecordRestored> context)
    {
        await writer.UpsertAsync(SearchSourceTypes.Record, context.Message.RecordId, null, null, context.Message.Status,
            null, null, false, context.Message.OccurredOn, false, context.CancellationToken);
        logger.ProjectionApplied("record.restored", SearchSourceTypes.Record, context.Message.RecordId);
    }
}

/// <summary>
/// Projects Documents integration events into the search index (ADR-026).
/// The document title is a safe display value owned by the Documents service.
/// </summary>
public sealed class DocumentsIndexConsumer(ISearchProjectionWriter writer, ILogger<DocumentsIndexConsumer> logger) :
    IConsumer<DocumentCreated>, IConsumer<DocumentMetadataUpdated>, IConsumer<DocumentClassified>, IConsumer<DocumentArchived>, IConsumer<DocumentDeactivated>, IConsumer<DocumentRestored>
{
    public async Task Consume(ConsumeContext<DocumentCreated> context)
    {
        await writer.UpsertAsync(SearchSourceTypes.Document, context.Message.DocumentId, context.Message.Title, context.Message.OwnerType ?? "document",
            context.Message.Status, null, context.Message.OrganizationUnitId, true, context.Message.OccurredOn, true, context.CancellationToken);
        logger.ProjectionApplied("document.created", SearchSourceTypes.Document, context.Message.DocumentId);
    }

    public async Task Consume(ConsumeContext<DocumentMetadataUpdated> context)
    {
        await writer.UpsertAsync(SearchSourceTypes.Document, context.Message.DocumentId, null, null, context.Message.Status,
            null, context.Message.OrganizationUnitId, true, context.Message.OccurredOn, false, context.CancellationToken);
        logger.ProjectionApplied("document.metadata-updated", SearchSourceTypes.Document, context.Message.DocumentId);
    }

    public async Task Consume(ConsumeContext<DocumentClassified> context)
    {
        await writer.UpsertAsync(SearchSourceTypes.Document, context.Message.DocumentId, null, null, null,
            context.Message.IsSensitive, null, false, context.Message.OccurredOn, false, context.CancellationToken);
        logger.ProjectionApplied("document.classified", SearchSourceTypes.Document, context.Message.DocumentId);
    }

    public async Task Consume(ConsumeContext<DocumentArchived> context)
    {
        await writer.UpsertAsync(SearchSourceTypes.Document, context.Message.DocumentId, null, null, "Archived",
            null, null, false, context.Message.OccurredOn, false, context.CancellationToken);
        logger.ProjectionApplied("document.archived", SearchSourceTypes.Document, context.Message.DocumentId);
    }

    public async Task Consume(ConsumeContext<DocumentDeactivated> context)
    {
        await writer.UpsertAsync(SearchSourceTypes.Document, context.Message.DocumentId, null, null, "Deactivated",
            null, null, false, context.Message.OccurredOn, false, context.CancellationToken);
        logger.ProjectionApplied("document.deactivated", SearchSourceTypes.Document, context.Message.DocumentId);
    }

    public async Task Consume(ConsumeContext<DocumentRestored> context)
    {
        await writer.UpsertAsync(SearchSourceTypes.Document, context.Message.DocumentId, null, null, context.Message.Status,
            null, null, false, context.Message.OccurredOn, false, context.CancellationToken);
        logger.ProjectionApplied("document.restored", SearchSourceTypes.Document, context.Message.DocumentId);
    }
}

/// <summary>
/// Projects Workflow task integration events into the search index (ADR-026).
/// DisplayTitle is the definition code only; task variables are never indexed.
/// </summary>
public sealed class WorkflowIndexConsumer(ISearchProjectionWriter writer, ILogger<WorkflowIndexConsumer> logger) :
    IConsumer<WorkflowTaskCreated>, IConsumer<WorkflowTaskAssigned>, IConsumer<WorkflowTaskStarted>, IConsumer<WorkflowTaskCompleted>, IConsumer<WorkflowTaskCancelled>
{
    public async Task Consume(ConsumeContext<WorkflowTaskCreated> context)
    {
        await writer.UpsertAsync(SearchSourceTypes.WorkflowTask, context.Message.TaskId, context.Message.DefinitionCode, context.Message.DefinitionCode,
            "Created", false, context.Message.OrganizationUnitId, true, context.Message.OccurredOn, true, context.CancellationToken);
        logger.ProjectionApplied("workflow-task.created", SearchSourceTypes.WorkflowTask, context.Message.TaskId);
    }

    public async Task Consume(ConsumeContext<WorkflowTaskAssigned> context)
    {
        await writer.UpsertAsync(SearchSourceTypes.WorkflowTask, context.Message.TaskId, context.Message.DefinitionCode, context.Message.DefinitionCode,
            "Assigned", false, null, false, context.Message.OccurredOn, false, context.CancellationToken);
        logger.ProjectionApplied("workflow-task.assigned", SearchSourceTypes.WorkflowTask, context.Message.TaskId);
    }

    public async Task Consume(ConsumeContext<WorkflowTaskStarted> context)
    {
        await writer.UpsertAsync(SearchSourceTypes.WorkflowTask, context.Message.TaskId, context.Message.DefinitionCode, context.Message.DefinitionCode,
            "InProgress", false, null, false, context.Message.OccurredOn, false, context.CancellationToken);
        logger.ProjectionApplied("workflow-task.started", SearchSourceTypes.WorkflowTask, context.Message.TaskId);
    }

    public async Task Consume(ConsumeContext<WorkflowTaskCompleted> context)
    {
        await writer.UpsertAsync(SearchSourceTypes.WorkflowTask, context.Message.TaskId, context.Message.DefinitionCode, context.Message.DefinitionCode,
            "Completed", false, null, false, context.Message.OccurredOn, false, context.CancellationToken);
        logger.ProjectionApplied("workflow-task.completed", SearchSourceTypes.WorkflowTask, context.Message.TaskId);
    }

    public async Task Consume(ConsumeContext<WorkflowTaskCancelled> context)
    {
        await writer.UpsertAsync(SearchSourceTypes.WorkflowTask, context.Message.TaskId, context.Message.DefinitionCode, context.Message.DefinitionCode,
            "Cancelled", false, null, false, context.Message.OccurredOn, false, context.CancellationToken);
        logger.ProjectionApplied("workflow-task.cancelled", SearchSourceTypes.WorkflowTask, context.Message.TaskId);
    }
}

/// <summary>
/// Projects Knowledge question integration events into the search index
/// (ADR-026). DisplayTitle is the literal "Question" — question text is never
/// indexed in the first gate.
/// </summary>
public sealed class KnowledgeIndexConsumer(ISearchProjectionWriter writer, ILogger<KnowledgeIndexConsumer> logger) :
    IConsumer<QuestionSubmitted>, IConsumer<QuestionPublished>, IConsumer<QuestionFlagged>, IConsumer<QuestionUnderReview>, IConsumer<QuestionMerged>, IConsumer<QuestionArchived>
{
    public async Task Consume(ConsumeContext<QuestionSubmitted> context)
    {
        await writer.UpsertAsync(SearchSourceTypes.KnowledgeQuestion, context.Message.QuestionId, "Question", "question",
            "Submitted", false, context.Message.OrganizationUnitId, true, context.Message.OccurredOn, true, context.CancellationToken);
        logger.ProjectionApplied("knowledge-question.submitted", SearchSourceTypes.KnowledgeQuestion, context.Message.QuestionId);
    }

    public async Task Consume(ConsumeContext<QuestionPublished> context)
    {
        await writer.UpsertAsync(SearchSourceTypes.KnowledgeQuestion, context.Message.QuestionId, "Question", "question",
            "Published", false, context.Message.OrganizationUnitId, true, context.Message.OccurredOn, false, context.CancellationToken);
        logger.ProjectionApplied("knowledge-question.published", SearchSourceTypes.KnowledgeQuestion, context.Message.QuestionId);
    }

    public async Task Consume(ConsumeContext<QuestionFlagged> context)
    {
        await writer.UpsertAsync(SearchSourceTypes.KnowledgeQuestion, context.Message.QuestionId, "Question", "question",
            "Flagged", false, null, false, context.Message.OccurredOn, false, context.CancellationToken);
        logger.ProjectionApplied("knowledge-question.flagged", SearchSourceTypes.KnowledgeQuestion, context.Message.QuestionId);
    }

    public async Task Consume(ConsumeContext<QuestionUnderReview> context)
    {
        await writer.UpsertAsync(SearchSourceTypes.KnowledgeQuestion, context.Message.QuestionId, "Question", "question",
            "UnderReview", false, null, false, context.Message.OccurredOn, false, context.CancellationToken);
        logger.ProjectionApplied("knowledge-question.under-review", SearchSourceTypes.KnowledgeQuestion, context.Message.QuestionId);
    }

    public async Task Consume(ConsumeContext<QuestionMerged> context)
    {
        await writer.UpsertAsync(SearchSourceTypes.KnowledgeQuestion, context.Message.QuestionId, "Question", "question",
            "Merged", false, null, false, context.Message.OccurredOn, false, context.CancellationToken);
        logger.ProjectionApplied("knowledge-question.merged", SearchSourceTypes.KnowledgeQuestion, context.Message.QuestionId);
    }

    public async Task Consume(ConsumeContext<QuestionArchived> context)
    {
        await writer.UpsertAsync(SearchSourceTypes.KnowledgeQuestion, context.Message.QuestionId, "Question", "question",
            "Archived", false, null, false, context.Message.OccurredOn, false, context.CancellationToken);
        logger.ProjectionApplied("knowledge-question.archived", SearchSourceTypes.KnowledgeQuestion, context.Message.QuestionId);
    }
}

/// <summary>
/// Maintains the local organization-unit reference table used to resolve
/// unit hierarchy for scope checks (ADR-026). Search never calls the
/// Organization service at query time; references are built from events.
/// </summary>
public sealed class OrganizationUnitProjectionConsumer(SearchDbContext db, ILogger<OrganizationUnitProjectionConsumer> logger) :
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
            db.OrganizationUnitReferences.Add(OrganizationUnitReference.Create(id, parent, occurredOn));
        else if (!row.Apply(parent, occurredOn))
            return;

        await db.SaveChangesAsync(ct);
        logger.ProjectionApplied("organization-unit.projected", "organization-unit", id);
    }
}

internal static partial class SearchConsumersLog
{
    [LoggerMessage(
        EventId = 0,
        Level = LogLevel.Information,
        Message = "Search projection applied {Operation} for {SourceType} {SourceId}.")]
    public static partial void ProjectionApplied(this ILogger logger, string operation, string sourceType, Guid sourceId);
}
