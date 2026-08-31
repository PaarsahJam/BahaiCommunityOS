using CommunityOS.Audit.Application;
using CommunityOS.Audit.Domain;
using CommunityOS.Audit.Infrastructure.Integration;
using CommunityOS.Contracts.Authorization;
using CommunityOS.Contracts.Documents;
using CommunityOS.Contracts.Notifications;
using CommunityOS.Contracts.Records;
using FluentAssertions;
using MassTransit;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace CommunityOS.Audit.Tests.Integration;

public sealed class AuditConsumerTests
{
    [Fact]
    public async Task Records_consumer_ingests_the_mapped_candidate()
    {
        var ingestor = Substitute.For<IAuditIngestor>();
        ingestor.IngestAsync(Arg.Any<IngestCandidate>(), Arg.Any<CancellationToken>())
            .Returns(IngestOutcome.Persisted);
        var consumer = new RecordsAuditConsumer(ingestor, NullLogger<RecordsAuditConsumer>.Instance);

        await consumer.Consume(Context(new RecordCreated(
            Guid.NewGuid(), "membership", "Draft", "person", Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            DateTime.UtcNow)));

        await ingestor.Received(1).IngestAsync(
            Arg.Is<IngestCandidate>(c => c.SourceService == "records" && c.Action == "record-created"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Notifications_consumer_ingests_dispatch_completions()
    {
        var ingestor = Substitute.For<IAuditIngestor>();
        ingestor.IngestAsync(Arg.Any<IngestCandidate>(), Arg.Any<CancellationToken>())
            .Returns(IngestOutcome.Duplicate);
        var consumer = new NotificationsAuditConsumer(ingestor, NullLogger<NotificationsAuditConsumer>.Instance);

        await consumer.Consume(Context(new NotificationDispatched(
            Guid.NewGuid(), "announcement", "email", "community-event", null, 5, DateTime.UtcNow)));

        await ingestor.Received(1).IngestAsync(
            Arg.Is<IngestCandidate>(c => c.SourceService == "notifications"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Authorization_consumer_ingests_all_seven_ratified_events_mapped_and_tolerates_duplicates()
    {
        var ingestor = Substitute.For<IAuditIngestor>();
        ingestor.IngestAsync(Arg.Any<IngestCandidate>(), Arg.Any<CancellationToken>())
            .Returns(IngestOutcome.Duplicate);
        var consumer = new AuthorizationAuditConsumer(ingestor, NullLogger<AuthorizationAuditConsumer>.Instance);

        var unit = Guid.NewGuid();
        object[] events =
        [
            new RoleAssigned(Guid.NewGuid(), Guid.NewGuid(), "treasurer", "Local", unit, DateTime.UtcNow, null, DateTime.UtcNow),
            new RoleRevoked(Guid.NewGuid(), Guid.NewGuid(), "treasurer", DateTime.UtcNow),
            new DelegationGranted(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), DateTime.UtcNow),
            new DelegationRevoked(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), DateTime.UtcNow),
            new BreakGlassRequested(Guid.NewGuid(), Guid.NewGuid(), DateTime.UtcNow),
            new BreakGlassApproved(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), DateTime.UtcNow.AddHours(1), DateTime.UtcNow),
            new BreakGlassRevoked(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), DateTime.UtcNow)
        ];

        var expected = new (string Action, bool Sensitive, Guid? OrgUnit)[]
        {
            ("role-assigned", false, unit),
            ("role-revoked", false, null),
            ("delegation-granted", false, null),
            ("delegation-revoked", false, null),
            ("break-glass-requested", true, null),
            ("break-glass-approved", true, null),
            ("break-glass-revoked", true, null)
        };

        for (var i = 0; i < events.Length; i++)
        {
            await Dispatch(consumer, events[i]);
            await ingestor.Received(1).IngestAsync(
                Arg.Is<IngestCandidate>(c =>
                    c.SourceService == "authorization" && c.Action == expected[i].Action
                    && c.Sensitivity == (expected[i].Sensitive ? AuditSensitivity.Sensitive : AuditSensitivity.Normal)
                    && c.OrganizationUnitId == expected[i].OrgUnit),
                Arg.Any<CancellationToken>());
        }
    }

    private static Task Dispatch(AuthorizationAuditConsumer consumer, object message) => message switch
    {
        RoleAssigned e => consumer.Consume(Context(e)),
        RoleRevoked e => consumer.Consume(Context(e)),
        DelegationGranted e => consumer.Consume(Context(e)),
        DelegationRevoked e => consumer.Consume(Context(e)),
        BreakGlassRequested e => consumer.Consume(Context(e)),
        BreakGlassApproved e => consumer.Consume(Context(e)),
        BreakGlassRevoked e => consumer.Consume(Context(e)),
        _ => throw new InvalidOperationException($"Unhandled event {message.GetType().Name}")
    };

    [Fact]
    public async Task Documents_consumer_ingests_exactly_the_five_ratified_events_mapped_and_tolerates_duplicates()
    {
        var ingestor = Substitute.For<IAuditIngestor>();
        ingestor.IngestAsync(Arg.Any<IngestCandidate>(), Arg.Any<CancellationToken>())
            .Returns(IngestOutcome.Duplicate);
        var consumer = new DocumentsAuditConsumer(ingestor, NullLogger<DocumentsAuditConsumer>.Instance);

        var versionId = Guid.NewGuid();
        object[] events =
        [
            new DocumentClassified(Guid.NewGuid(), "restricted", true, DateTime.UtcNow),
            new DocumentDeactivated(Guid.NewGuid(), DateTime.UtcNow),
            new DocumentRestored(Guid.NewGuid(), "Active", DateTime.UtcNow),
            new DocumentContentDownloaded(Guid.NewGuid(), versionId, Guid.NewGuid(), DateTime.UtcNow),
            new DocumentScanCompleted(Guid.NewGuid(), versionId, "Clean", DateTime.UtcNow)
        ];

        var expected = new (string Action, bool Sensitive, Guid? Secondary, string? Outcome)[]
        {
            ("document-classified", true, null, null),
            ("document-deactivated", false, null, null),
            ("document-restored", false, null, null),
            ("document-content-downloaded", true, versionId, null),
            ("document-scan-completed", false, versionId, "Clean")
        };

        for (var i = 0; i < events.Length; i++)
        {
            await Dispatch(consumer, events[i]);
            await ingestor.Received(1).IngestAsync(
                Arg.Is<IngestCandidate>(c =>
                    c.SourceService == "documents" && c.ResourceType == "document"
                    && c.Action == expected[i].Action
                    && c.Sensitivity == (expected[i].Sensitive ? AuditSensitivity.Sensitive : AuditSensitivity.Normal)
                    && c.SecondaryResourceId == expected[i].Secondary
                    && string.Equals(c.Outcome, expected[i].Outcome, StringComparison.Ordinal)),
                Arg.Any<CancellationToken>());
        }
    }

    private static Task Dispatch(DocumentsAuditConsumer consumer, object message) => message switch
    {
        DocumentClassified e => consumer.Consume(Context(e)),
        DocumentDeactivated e => consumer.Consume(Context(e)),
        DocumentRestored e => consumer.Consume(Context(e)),
        DocumentContentDownloaded e => consumer.Consume(Context(e)),
        DocumentScanCompleted e => consumer.Consume(Context(e)),
        _ => throw new InvalidOperationException($"Unhandled event {message.GetType().Name}")
    };

    [Fact]
    public void Documents_consumer_depends_only_on_ingestor_and_logger_without_publish_surface()
    {
        var ingestor = Substitute.For<IAuditIngestor>();
        var consumer = new DocumentsAuditConsumer(ingestor, NullLogger<DocumentsAuditConsumer>.Instance);

        consumer.Should().BeAssignableTo<IConsumer<DocumentClassified>>();
        consumer.Should().BeAssignableTo<IConsumer<DocumentDeactivated>>();
        consumer.Should().BeAssignableTo<IConsumer<DocumentRestored>>();
        consumer.Should().BeAssignableTo<IConsumer<DocumentContentDownloaded>>();
        consumer.Should().BeAssignableTo<IConsumer<DocumentScanCompleted>>();
        consumer.Should().NotBeAssignableTo<IConsumer<DocumentArchived>>();
        consumer.Should().NotBeAssignableTo<IConsumer<DocumentCreated>>();
        consumer.Should().NotBeAssignableTo<IConsumer<DocumentMetadataUpdated>>();
        consumer.Should().NotBeAssignableTo<IConsumer<DocumentVersionAdded>>();
    }

    private static ConsumeContext<T> Context<T>(T message) where T : class
    {
        var context = Substitute.For<ConsumeContext<T>>();
        context.Message.Returns(message);
        context.CancellationToken.Returns(CancellationToken.None);
        return context;
    }
}
