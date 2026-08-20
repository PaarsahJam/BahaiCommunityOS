using CommunityOS.Workflow.Domain.Aggregates;
using CommunityOS.Workflow.Domain.Repositories;
using MassTransit;
using MediatR;
using Microsoft.Extensions.Logging;
using CommunityOS.Workflow.Application.Pipeline;
using static CommunityOS.Workflow.Infrastructure.Integration.WorkflowReconciliation;

namespace CommunityOS.Workflow.Infrastructure.Integration.Records;

/// <summary>
/// Reconciles <c>record-review</c> tasks from Records integration events
/// (ADR-024). Workflow never calls the Records verify/reject APIs and never
/// alters record facts; Records owns verification truth. Task completion with
/// outcome <c>verified</c>/<c>rejected</c> only records that the human review
/// task is done — the <c>RecordVerified</c>/<c>RecordRejected</c> events are the
/// authoritative signals. Reconciliation is create-if-absent /
/// close-if-open and idempotent under duplicates.
/// </summary>
public sealed class RecordsReviewIntegrationEventConsumer(
    IWorkflowTaskRepository tasks,
    IMediator mediator,
    ILogger<RecordsReviewIntegrationEventConsumer> logger) :
    IConsumer<Contracts.Records.RecordSubmitted>,
    IConsumer<Contracts.Records.RecordVerified>,
    IConsumer<Contracts.Records.RecordRejected>
{
    public Task Consume(ConsumeContext<Contracts.Records.RecordSubmitted> context) =>
        CreateRecordReviewTaskIfAbsentAsync(context.Message.RecordId, context.Message.OccurredOn, context.CancellationToken);

    public async Task Consume(ConsumeContext<Contracts.Records.RecordVerified> context)
    {
        var message = context.Message;
        await CompleteRecordReviewAsync(message.RecordId, "verified", message.VerifiedBy, message.OccurredOn, context.CancellationToken);
    }

    public async Task Consume(ConsumeContext<Contracts.Records.RecordRejected> context)
    {
        var message = context.Message;
        await CompleteRecordReviewAsync(message.RecordId, "rejected", message.RejectedBy, message.OccurredOn, context.CancellationToken);
    }

    private async Task CreateRecordReviewTaskIfAbsentAsync(
        Guid recordId, DateTime occurredOn, CancellationToken ct)
    {
        var task = await CreateIfAbsentAsync(
            tasks, RecordReviewDefinitionCode, RecordDomainType, recordId,
            originatorId: SystemActorId, occurredOn, ct);
        if (task is not null)
            logger.RecordReviewTaskCreated(task.Id, recordId);
    }

    private async Task CompleteRecordReviewAsync(
        Guid recordId, string outcome, Guid completedBy, DateTime occurredOn, CancellationToken ct)
    {
        var task = await CloseIfOpenAsync(
            tasks, mediator, RecordReviewDefinitionCode, RecordDomainType, recordId,
            outcome, completedBy, occurredOn, ct);
        if (task is not null)
            logger.RecordReviewTaskCompleted(task.Id, recordId, outcome);
    }
}

internal static partial class RecordsReviewIntegrationEventConsumerLogging
{
    [LoggerMessage(
        EventId = 0,
        Level = LogLevel.Information,
        Message = "Created record-review task {TaskId} for record {RecordId}.")]
    public static partial void RecordReviewTaskCreated(this ILogger logger, Guid taskId, Guid recordId);

    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Information,
        Message = "Completed record-review task {TaskId} for record {RecordId} with outcome {Outcome}.")]
    public static partial void RecordReviewTaskCompleted(this ILogger logger, Guid taskId, Guid recordId, string outcome);
}