using CommunityOS.Contracts.Documents;
using CommunityOS.Records.Domain.Repositories;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace CommunityOS.Records.Infrastructure.Integration.Documents;

/// <summary>
/// Reconciles Records evidence references when a referenced document is
/// deactivated or restored (ADR-023). Guaranteed delivery for this consumer is
/// the ratified outbox gate: it is registered only when the transactional
/// outbox is enabled, so a deactivation event is never lost.
/// </summary>
public sealed class DocumentLifecycleIntegrationEventConsumer(
    IRecordRepository records,
    ILogger<DocumentLifecycleIntegrationEventConsumer> logger) :
    IConsumer<DocumentDeactivated>,
    IConsumer<DocumentRestored>
{
    public async Task Consume(ConsumeContext<DocumentDeactivated> context)
    {
        var message = context.Message;
        var recordsWithEvidence = await records.ListByEvidenceDocumentAsync(message.DocumentId, context.CancellationToken);
        if (recordsWithEvidence.Count == 0)
            return;

        foreach (var record in recordsWithEvidence)
        {
            var flagged = false;
            foreach (var evidence in record.Evidence.Where(e => e.DocumentId == message.DocumentId))
            {
                evidence.MarkDocumentDeactivated(message.OccurredOn);
                flagged = true;
            }

            if (flagged)
                await records.UpdateAsync(record, context.CancellationToken);
        }

        logger.EvidenceReferencesFlagged(message.DocumentId, recordsWithEvidence.Count);
    }

    public async Task Consume(ConsumeContext<DocumentRestored> context)
    {
        var message = context.Message;
        var recordsWithEvidence = await records.ListByEvidenceDocumentAsync(message.DocumentId, context.CancellationToken);
        if (recordsWithEvidence.Count == 0)
            return;

        foreach (var record in recordsWithEvidence)
        {
            var cleared = false;
            foreach (var evidence in record.Evidence.Where(e => e.DocumentId == message.DocumentId))
            {
                evidence.MarkDocumentRestored(message.OccurredOn);
                cleared = true;
            }

            if (cleared)
                await records.UpdateAsync(record, context.CancellationToken);
        }

        logger.EvidenceReferencesRestored(message.DocumentId, recordsWithEvidence.Count);
    }
}

internal static partial class DocumentLifecycleIntegrationEventConsumerLogging
{
    [LoggerMessage(
        EventId = 0,
        Level = LogLevel.Information,
        Message = "Flagged {Count} evidence references for deactivated document {DocumentId}.")]
    public static partial void EvidenceReferencesFlagged(this ILogger logger, Guid documentId, int count);

    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Information,
        Message = "Cleared deactivation flags on {Count} evidence references for restored document {DocumentId}.")]
    public static partial void EvidenceReferencesRestored(this ILogger logger, Guid documentId, int count);
}