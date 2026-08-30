using CommunityOS.Audit.Application;
using CommunityOS.Contracts.Authorization;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace CommunityOS.Audit.Infrastructure.Integration;

/// <summary>
/// Journals the seven ratified Authorization facts (ADR-027 decisions 5/6/10/11):
/// role assignment, role revocation, delegation grant/revocation, and the break-glass
/// request lifecycle. Every consume maps through the pure ingest mapping; persistence
/// is idempotent by source-event hash and exactly-once via the transactional inbox.
/// Break-glass facts are journaled Sensitive. This consumer never publishes and never
/// touches Authorization state or the Authorization database.
/// </summary>
public sealed class AuthorizationAuditConsumer(IAuditIngestor ingestor, ILogger<AuthorizationAuditConsumer> logger) :
    IConsumer<RoleAssigned>,
    IConsumer<RoleRevoked>,
    IConsumer<DelegationGranted>,
    IConsumer<DelegationRevoked>,
    IConsumer<BreakGlassRequested>,
    IConsumer<BreakGlassApproved>,
    IConsumer<BreakGlassRevoked>
{
    public Task Consume(ConsumeContext<RoleAssigned> context) => Ingest(context, AuditEventMapper.Map(context.Message));
    public Task Consume(ConsumeContext<RoleRevoked> context) => Ingest(context, AuditEventMapper.Map(context.Message));
    public Task Consume(ConsumeContext<DelegationGranted> context) => Ingest(context, AuditEventMapper.Map(context.Message));
    public Task Consume(ConsumeContext<DelegationRevoked> context) => Ingest(context, AuditEventMapper.Map(context.Message));
    public Task Consume(ConsumeContext<BreakGlassRequested> context) => Ingest(context, AuditEventMapper.Map(context.Message));
    public Task Consume(ConsumeContext<BreakGlassApproved> context) => Ingest(context, AuditEventMapper.Map(context.Message));
    public Task Consume(ConsumeContext<BreakGlassRevoked> context) => Ingest(context, AuditEventMapper.Map(context.Message));

    private async Task Ingest<T>(ConsumeContext<T> context, IngestCandidate candidate)
        where T : class
    {
        var outcome = await ingestor.IngestAsync(candidate, context.CancellationToken);
        logger.EntryIngested(outcome.ToString(), candidate.SourceEventType, candidate.ResourceId);
    }
}