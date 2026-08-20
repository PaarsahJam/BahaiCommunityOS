namespace CommunityOS.Contracts.Notifications;

/// <summary>
/// Raised when a notification completes dispatch (every recipient is terminal).
/// Carries identifiers and a count only — the ratified privacy rule
/// (ADR-025): recipient member ids (distribution is sensitive), subject/body
/// copy, names, delivery failures and read/delivery provenance are never
/// exported. Audit (slot 11) and Analytics (slot 19) consume this event; it is
/// protected by the transactional outbox from the Notifications integration
/// gate.
/// </summary>
public sealed record NotificationDispatched(
    Guid NotificationId,
    string TypeCode,
    string Channel,
    string SourceType,
    Guid? SourceId,
    int RecipientCount,
    DateTime OccurredOn);