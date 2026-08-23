using CommunityOS.Audit.Application;
using CommunityOS.Audit.Infrastructure.Integration;
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

    private static ConsumeContext<T> Context<T>(T message) where T : class
    {
        var context = Substitute.For<ConsumeContext<T>>();
        context.Message.Returns(message);
        context.CancellationToken.Returns(CancellationToken.None);
        return context;
    }
}
