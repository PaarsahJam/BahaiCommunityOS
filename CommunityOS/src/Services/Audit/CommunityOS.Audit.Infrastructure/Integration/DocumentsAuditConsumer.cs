using CommunityOS.Audit.Application;
using CommunityOS.Contracts.Documents;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace CommunityOS.Audit.Infrastructure.Integration;

/// <summary>
/// Journals the five ratified Documents compliance facts (ADR-027 decisions 5/6/10):
/// classification, deactivation, restore, sensitive-content download, and scan
/// completion. Every consume maps through the pure ingest mapping; persistence
/// is idempotent by source-event hash and exactly-once via the transactional inbox.
/// Sensitive-content downloads are journaled Sensitive. This consumer never
/// publishes and never touches Documents state or the Documents database.
/// </summary>
public sealed class DocumentsAuditConsumer(IAuditIngestor ingestor, ILogger<DocumentsAuditConsumer> logger) :
    IConsumer<DocumentClassified>,
    IConsumer<DocumentDeactivated>,
    IConsumer<DocumentRestored>,
    IConsumer<DocumentContentDownloaded>,
    IConsumer<DocumentScanCompleted>
{
    public Task Consume(ConsumeContext<DocumentClassified> context) => Ingest(context, AuditEventMapper.Map(context.Message));
    public Task Consume(ConsumeContext<DocumentDeactivated> context) => Ingest(context, AuditEventMapper.Map(context.Message));
    public Task Consume(ConsumeContext<DocumentRestored> context) => Ingest(context, AuditEventMapper.Map(context.Message));
    public Task Consume(ConsumeContext<DocumentContentDownloaded> context) => Ingest(context, AuditEventMapper.Map(context.Message));
    public Task Consume(ConsumeContext<DocumentScanCompleted> context) => Ingest(context, AuditEventMapper.Map(context.Message));

    private async Task Ingest<T>(ConsumeContext<T> context, IngestCandidate candidate)
        where T : class
    {
        var outcome = await ingestor.IngestAsync(candidate, context.CancellationToken);
        logger.EntryIngested(outcome.ToString(), candidate.SourceEventType, candidate.ResourceId);
    }
}
