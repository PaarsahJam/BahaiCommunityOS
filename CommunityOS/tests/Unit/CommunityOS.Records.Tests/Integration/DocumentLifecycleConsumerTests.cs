using CommunityOS.Contracts.Documents;
using CommunityOS.Records.Domain.Aggregates;
using CommunityOS.Records.Domain.Repositories;
using CommunityOS.Records.Infrastructure.Integration.Documents;
using MassTransit;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace CommunityOS.Records.Tests.Integration;

/// <summary>
/// Reconciliation of evidence references when a referenced document is
/// deactivated or restored (ADR-023). The consumer is registered only when the
/// transactional outbox gate is enabled, so a deactivation event is never lost.
/// </summary>
public class DocumentLifecycleConsumerTests
{
    private static readonly Guid Creator = Guid.NewGuid();
    private static readonly DateTime Now = DateTime.UtcNow;

    private static Record RecordWithEvidence(Guid documentId, int version = 1)
    {
        var record = Record.Create("membership", "person", Guid.NewGuid(), Guid.NewGuid(),
            [RecordFieldValue.Create("name", "A Flower", false)], isSensitive: false, Creator, Now);
        record.AttachEvidence(documentId, version, "proof_of_address", Creator, Now);
        return record;
    }

    private static ConsumeContext<T> Context<T>(T message)
        where T : class
    {
        var ctx = Substitute.For<ConsumeContext<T>>();
        ctx.Message.Returns(message);
        ctx.CancellationToken.Returns(CancellationToken.None);
        return ctx;
    }

    private static DocumentLifecycleIntegrationEventConsumer Consumer(IRecordRepository records) =>
        new(records, NullLogger<DocumentLifecycleIntegrationEventConsumer>.Instance);

    [Fact]
    public async Task Deactivated_document_flags_matching_evidence_and_persists()
    {
        var docId = Guid.NewGuid();
        var record = RecordWithEvidence(docId);
        var records = Substitute.For<IRecordRepository>();
        records.ListByEvidenceDocumentAsync(docId, Arg.Any<CancellationToken>()).Returns([record]);

        await Consumer(records).Consume(Context(new DocumentDeactivated(docId, Now)));

        record.Evidence.Single().DocumentDeactivatedOn.Should().Be(Now);
        await records.Received(1).UpdateAsync(record, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Deactivation_flags_only_the_matching_evidence_reference()
    {
        var docId = Guid.NewGuid();
        var otherDoc = Guid.NewGuid();
        var record = RecordWithEvidence(docId);
        record.AttachEvidence(otherDoc, 1, "proof_of_residence", Creator, Now);
        var records = Substitute.For<IRecordRepository>();
        records.ListByEvidenceDocumentAsync(docId, Arg.Any<CancellationToken>()).Returns([record]);

        await Consumer(records).Consume(Context(new DocumentDeactivated(docId, Now)));

        record.Evidence.Single(e => e.DocumentId == docId).DocumentDeactivatedOn.Should().Be(Now);
        record.Evidence.Single(e => e.DocumentId == otherDoc).DocumentDeactivatedOn.Should().BeNull();
        await records.Received(1).UpdateAsync(record, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Restored_document_clears_the_deactivation_flag_and_persists()
    {
        var docId = Guid.NewGuid();
        var record = RecordWithEvidence(docId);
        var evidence = record.Evidence.Single();
        evidence.MarkDocumentDeactivated(Now);
        var records = Substitute.For<IRecordRepository>();
        records.ListByEvidenceDocumentAsync(docId, Arg.Any<CancellationToken>()).Returns([record]);

        await Consumer(records).Consume(Context(new DocumentRestored(docId, "active", Now)));

        evidence.DocumentRestoredOn.Should().Be(Now);
        await records.Received(1).UpdateAsync(record, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Deactivation_with_no_matching_records_persists_nothing()
    {
        var docId = Guid.NewGuid();
        var records = Substitute.For<IRecordRepository>();
        records.ListByEvidenceDocumentAsync(docId, Arg.Any<CancellationToken>()).Returns([]);

        await Consumer(records).Consume(Context(new DocumentDeactivated(docId, Now)));

        await records.DidNotReceiveWithAnyArgs().UpdateAsync(default!);
    }
}