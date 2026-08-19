using CommunityOS.Contracts.Records;
using CommunityOS.Records.Domain.Aggregates;
using CommunityOS.Records.Domain.Events;
using CommunityOS.Records.Infrastructure.Integration;
using MassTransit;
using NSubstitute;

namespace CommunityOS.Records.Tests.Security;

/// <summary>
/// Locks the integration-event export boundary (ADR-023): published payloads
/// carry only stable identifiers and minimal lifecycle metadata — never field
/// values, secrets, names or hold reasons. The publisher is the only sanctioned
/// path from domain events to the bus.
/// </summary>
public class RecordsIntegrationEventSecurityTests
{
    private static readonly Guid Actor = Guid.NewGuid();
    private static readonly DateTime Now = DateTime.UtcNow;

    private static Record DraftRecord(params RecordFieldValue[] fields) =>
        Record.Create("membership", "person", Guid.NewGuid(), Guid.NewGuid(),
            fields, isSensitive: false, Actor, Now);

    [Fact]
    public async Task RecordCreated_publishes_only_stable_identifiers_and_never_field_values()
    {
        const string fieldValue = "Confidential Person Name";
        var subjectId = Guid.NewGuid();
        var unitId = Guid.NewGuid();
        var record = Record.Create("membership", "person", subjectId, unitId,
            [RecordFieldValue.Create("name", fieldValue, true)], isSensitive: true, Actor, Now);
        var created = record.DomainEvents.OfType<RecordCreatedEvent>().Single();

        var captured = new List<RecordCreated>();
        var endpoint = Substitute.For<IPublishEndpoint>();
        endpoint.When(x => x.Publish(Arg.Any<RecordCreated>(), Arg.Any<CancellationToken>()))
            .Do(call => captured.Add(call.Arg<RecordCreated>()));

        await new RecordsIntegrationEventPublisher<RecordCreatedEvent>(endpoint)
            .Handle(created, CancellationToken.None);

        var published = captured.Single();
        published.RecordId.Should().Be(record.Id);
        published.SubjectId.Should().Be(subjectId);
        published.OrganizationUnitId.Should().Be(unitId);
        published.CreatedBy.Should().Be(Actor);
        published.ToString().Should().NotContain(fieldValue);
    }

    [Fact]
    public async Task RecordHoldPlaced_publishes_no_reason_and_no_document_references()
    {
        const string reason = "Confidential legal subpoena details.";
        var record = DraftRecord();
        record.PlaceHold("legal", reason,
            [RecordHoldDocumentReference.Create(Guid.NewGuid(), 1)], Actor, Now);
        var placed = record.DomainEvents.OfType<RecordHoldPlacedEvent>().Single();

        var captured = new List<RecordHoldPlaced>();
        var endpoint = Substitute.For<IPublishEndpoint>();
        endpoint.When(x => x.Publish(Arg.Any<RecordHoldPlaced>(), Arg.Any<CancellationToken>()))
            .Do(call => captured.Add(call.Arg<RecordHoldPlaced>()));

        await new RecordsIntegrationEventPublisher<RecordHoldPlacedEvent>(endpoint)
            .Handle(placed, CancellationToken.None);

        var published = captured.Single();
        published.HoldType.Should().Be("legal");
        published.PlacedBy.Should().Be(Actor);
        published.ToString().Should().NotContain(reason);
    }

    [Fact]
    public async Task RecordCorrected_publishes_version_numbers_and_no_field_values()
    {
        const string correctedValue = "Confidential Corrected Value";
        var reviewer = Guid.NewGuid();
        var record = DraftRecord();
        record.Submit(Actor, Now);
        record.MoveUnderReview(reviewer, Now);
        record.Verify(reviewer, Now);
        record.Correct([RecordFieldValue.Create("name", correctedValue, true)],
            "Corrected after verification.", Actor, Now);
        var corrected = record.DomainEvents.OfType<RecordCorrectedEvent>().Single();

        var captured = new List<RecordCorrected>();
        var endpoint = Substitute.For<IPublishEndpoint>();
        endpoint.When(x => x.Publish(Arg.Any<RecordCorrected>(), Arg.Any<CancellationToken>()))
            .Do(call => captured.Add(call.Arg<RecordCorrected>()));

        await new RecordsIntegrationEventPublisher<RecordCorrectedEvent>(endpoint)
            .Handle(corrected, CancellationToken.None);

        var published = captured.Single();
        published.VersionNumber.Should().Be(2);
        published.SupersedesVersionNumber.Should().Be(1);
        published.CorrectedBy.Should().Be(Actor);
        published.ToString().Should().NotContain(correctedValue);
    }
}