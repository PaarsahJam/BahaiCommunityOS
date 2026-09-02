using CommunityOS.Audit.Application;
using CommunityOS.Contracts.Identity;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace CommunityOS.Audit.Infrastructure.Integration;

/// <summary>
/// Journals the seven ratified Identity account-security facts (ADR-027
/// decisions 5/6/10): lock, unlock, credential change, MFA enrolment/removal,
/// and external-identity linkage. Every consume maps through the pure ingest
/// mapping; persistence is idempotent by source-event hash and exactly-once via
/// the transactional inbox. All seven are Normal. Privacy per ADR-027:
/// Provider/Subject (external identity) and MethodType (MFA) are never
/// persisted or hashed; CredentialChanged persists no credential material. This
/// consumer never publishes and never touches Identity state or the Identity
/// database.
/// </summary>
public sealed class IdentityAuditConsumer(IAuditIngestor ingestor, ILogger<IdentityAuditConsumer> logger) :
    IConsumer<UserAccountLocked>,
    IConsumer<UserAccountUnlocked>,
    IConsumer<CredentialChanged>,
    IConsumer<MfaMethodEnrolled>,
    IConsumer<MfaMethodRemoved>,
    IConsumer<ExternalIdentityLinked>,
    IConsumer<ExternalIdentityUnlinked>
{
    public Task Consume(ConsumeContext<UserAccountLocked> context) => Ingest(context, AuditEventMapper.Map(context.Message));
    public Task Consume(ConsumeContext<UserAccountUnlocked> context) => Ingest(context, AuditEventMapper.Map(context.Message));
    public Task Consume(ConsumeContext<CredentialChanged> context) => Ingest(context, AuditEventMapper.Map(context.Message));
    public Task Consume(ConsumeContext<MfaMethodEnrolled> context) => Ingest(context, AuditEventMapper.Map(context.Message));
    public Task Consume(ConsumeContext<MfaMethodRemoved> context) => Ingest(context, AuditEventMapper.Map(context.Message));
    public Task Consume(ConsumeContext<ExternalIdentityLinked> context) => Ingest(context, AuditEventMapper.Map(context.Message));
    public Task Consume(ConsumeContext<ExternalIdentityUnlinked> context) => Ingest(context, AuditEventMapper.Map(context.Message));

    private async Task Ingest<T>(ConsumeContext<T> context, IngestCandidate candidate)
        where T : class
    {
        var outcome = await ingestor.IngestAsync(candidate, context.CancellationToken);
        logger.EntryIngested(outcome.ToString(), candidate.SourceEventType, candidate.ResourceId);
    }
}