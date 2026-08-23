using CommunityOS.Correspondence.Application;
using CommunityOS.Correspondence.Domain;
using CommunityOS.Correspondence.Domain.Events;
using FluentAssertions;
using Xunit;

namespace CommunityOS.Correspondence.Tests.Application;

/// <summary>
/// Ratified payload allowlist (ADR-028 decisions 8 and 14): integration events
/// carry identifiers, codes, counts and timestamps only — never subjects,
/// bodies or recipient display lines. Verified here at the domain-event
/// factory boundary; the Infrastructure forwarder maps these records 1:1 onto
/// the contract types.
/// </summary>
public sealed class EventPayloadAllowlistTests
{
    [Fact]
    public void Submitted_payloads_carry_identifiers_codes_and_counts_only()
    {
        var letter = Letter.CreateDraft(Guid.NewGuid(), "general",
            "SECRET SUBJECT", "SECRET BODY", LetterSensitivity.Sensitive,
            Guid.NewGuid(), DateTime.UtcNow);
        letter.AddRecipient(RecipientKind.Person, Guid.NewGuid(), null, null, DateTime.UtcNow);
        letter.AddRecipient(RecipientKind.Unit, null, Guid.NewGuid(), null, DateTime.UtcNow);
        letter.AddRecipient(RecipientKind.External, null, null, "Jane Doe <jane@example.com>", DateTime.UtcNow);
        letter.Confirm(Guid.NewGuid(), DateTime.UtcNow);
        letter.Submit(Guid.NewGuid(), 2026, 11, "default", null, DateTime.UtcNow);

        var payload = LetterEventFactory.Submitted(letter);

        payload.LetterId.Should().Be(letter.Id);
        payload.LetterYear.Should().Be(2026);
        payload.LetterSequence.Should().Be(11);
        payload.CategoryCode.Should().Be("general");
        payload.Sensitivity.Should().Be("sensitive");
        payload.RecipientCount.Should().Be(3);
        payload.RecipientPersonIds.Should().ContainSingle();
        payload.RecipientUnitIds.Should().ContainSingle();

        // Allowlist enforcement: no free-text surface may leak.
        payload.GetType().GetProperties().Select(p => p.Name).Should().BeEquivalentTo(
        [
            nameof(LetterSubmittedDomainEvent.EventId),
            nameof(LetterSubmittedDomainEvent.OccurredOn),
            nameof(LetterSubmittedDomainEvent.LetterId),
            nameof(LetterSubmittedDomainEvent.LetterYear),
            nameof(LetterSubmittedDomainEvent.LetterSequence),
            nameof(LetterSubmittedDomainEvent.OrganizationUnitId),
            nameof(LetterSubmittedDomainEvent.CategoryCode),
            nameof(LetterSubmittedDomainEvent.Sensitivity),
            nameof(LetterSubmittedDomainEvent.RecipientCount),
            nameof(LetterSubmittedDomainEvent.RecipientPersonIds),
            nameof(LetterSubmittedDomainEvent.RecipientUnitIds),
            nameof(LetterSubmittedDomainEvent.SubmittedBy)
        ]);
    }

    [Fact]
    public void Summary_dto_is_metadata_only_with_null_subject()
    {
        var row = new LetterSummaryRow(
            Guid.NewGuid(), Guid.NewGuid(), "general", 2026, 1, "draft", "normal",
            false, 0, 0, 0, 0, null, DateTime.UtcNow, null);

        var dto = LetterMapper.ToSummaryDto(row);

        dto.Subject.Should().BeNull("list responses are metadata-only");
        dto.Reference.Should().Be("2026-00001");
    }

    [Fact]
    public void Reference_format_matches_the_ratified_composition()
    {
        var row = new LetterSummaryRow(
            Guid.NewGuid(), Guid.NewGuid(), "general", 2026, 42, "draft", "normal",
            false, 0, 0, 0, 0, null, DateTime.UtcNow, null);

        row.Reference.Should().Be("2026-00042");
    }
}
