using CommunityOS.SharedKernel.Domain.Events;

namespace CommunityOS.Correspondence.Domain.Events;

/// <summary>
/// In-process domain events raised by letter transitions; forwarded to
/// integration contracts by the infrastructure publisher. Payloads mirror the
/// ratified contract shapes exactly: identifiers, codes, counts and timestamps
/// only — never subject, body or display lines.
/// </summary>
public sealed record LetterSubmittedDomainEvent(
    Guid LetterId,
    int LetterYear,
    int LetterSequence,
    Guid OrganizationUnitId,
    string CategoryCode,
    string Sensitivity,
    int RecipientCount,
    IReadOnlyList<Guid> RecipientPersonIds,
    IReadOnlyList<Guid> RecipientUnitIds,
    Guid SubmittedBy) : DomainEvent;

public sealed record LetterDispatchedDomainEvent(
    Guid LetterId,
    int LetterYear,
    int LetterSequence,
    Guid OrganizationUnitId,
    string MethodCode,
    Guid DispatchedBy) : DomainEvent;

public sealed record LetterDeliveryConfirmedDomainEvent(
    Guid LetterId,
    int LetterYear,
    int LetterSequence,
    Guid OrganizationUnitId,
    string MethodCode,
    Guid ConfirmedBy) : DomainEvent;

public sealed record LetterDeliveryFailedDomainEvent(
    Guid LetterId,
    int LetterYear,
    int LetterSequence,
    Guid OrganizationUnitId,
    string MethodCode,
    string ReasonCode,
    Guid ConfirmedBy) : DomainEvent;

public sealed record LetterCancelledDomainEvent(
    Guid LetterId,
    int LetterYear,
    int LetterSequence,
    Guid OrganizationUnitId,
    string CancelledFromStatus,
    string ReasonCode,
    Guid CancelledBy) : DomainEvent;
