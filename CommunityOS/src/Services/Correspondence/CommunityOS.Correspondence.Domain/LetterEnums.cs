namespace CommunityOS.Correspondence.Domain;

/// <summary>
/// Letter lifecycle states (ADR-028 decision 3). The machine is deterministic:
/// Draft → Confirmed → Submitted → Materialized → Dispatched → Delivered, with
/// terminal DeliveryFailed and Cancelled. Cancellation is legal only from
/// Draft, Confirmed, Submitted and Materialized; content edits only in Draft.
/// </summary>
public enum LetterStatus
{
    Draft = 0,
    Confirmed = 1,
    Submitted = 2,
    Materialized = 3,
    Dispatched = 4,
    Delivered = 5,
    DeliveryFailed = 6,
    Cancelled = 7
}

public enum RecipientKind
{
    Person = 0,
    Unit = 1,
    External = 2
}

public enum LetterSensitivity
{
    Normal = 0,
    Sensitive = 1
}

/// <summary>What produced a lifecycle transition (history cause).</summary>
public enum HistoryCause
{
    Command = 0,
    Event = 1,
    Provider = 2,
    PurgeMarker = 3
}

public enum DeliveryOutcome
{
    Dispatched = 0,
    Confirmed = 1,
    Failed = 2
}
