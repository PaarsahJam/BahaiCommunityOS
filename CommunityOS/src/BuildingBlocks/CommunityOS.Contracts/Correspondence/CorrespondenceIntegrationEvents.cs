namespace CommunityOS.Contracts.Correspondence;

/// <summary>
/// Correspondence integration events (ADR-028, slot 13). The ratified catalog
/// is exactly these five published facts; payloads carry identifiers, codes,
/// counts and timestamps only — never letter bodies, subjects, recipient
/// display lines or external addresses (the ratified privacy rule). All five
/// are published through the Correspondence transactional outbox from day one
/// (ADR-015); consumption by Audit requires the recorded future ADR-027
/// amendment and is deliberately not part of this gate.
/// </summary>
public sealed record LetterSubmitted(
    Guid LetterId,
    int LetterYear,
    int LetterSequence,
    Guid OrganizationUnitId,
    string CategoryCode,
    string Sensitivity,
    int RecipientCount,
    Guid[] RecipientPersonIds,
    Guid[] RecipientUnitIds,
    Guid SubmittedBy,
    DateTime OccurredOn);

public sealed record LetterDispatched(
    Guid LetterId,
    int LetterYear,
    int LetterSequence,
    Guid OrganizationUnitId,
    string MethodCode,
    Guid DispatchedBy,
    DateTime OccurredOn);

public sealed record LetterDeliveryConfirmed(
    Guid LetterId,
    int LetterYear,
    int LetterSequence,
    Guid OrganizationUnitId,
    string MethodCode,
    Guid ConfirmedBy,
    DateTime OccurredOn);

public sealed record LetterDeliveryFailed(
    Guid LetterId,
    int LetterYear,
    int LetterSequence,
    Guid OrganizationUnitId,
    string MethodCode,
    string ReasonCode,
    Guid ConfirmedBy,
    DateTime OccurredOn);

public sealed record LetterCancelled(
    Guid LetterId,
    int LetterYear,
    int LetterSequence,
    Guid OrganizationUnitId,
    string CancelledFromStatus,
    string ReasonCode,
    Guid CancelledBy,
    DateTime OccurredOn);
