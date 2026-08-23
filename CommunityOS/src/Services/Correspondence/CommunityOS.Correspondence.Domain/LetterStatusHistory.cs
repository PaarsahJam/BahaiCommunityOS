namespace CommunityOS.Correspondence.Domain;

/// <summary>
/// One immutable lifecycle-history row (ADR-028 decision 3). Rows are only
/// ever appended; the table has no database-level foreign key to
/// <c>letters</c> by design — history outlives purged letters so the purge
/// tombstones (cause <see cref="HistoryCause.PurgeMarker"/>) persist after the
/// batch deletion, keeping the audit trail complete without any ordinary
/// application path being able to rewrite it.
/// </summary>
public sealed class LetterStatusHistory
{
    private LetterStatusHistory()
    {
    }

    public Guid Id { get; private set; }

    /// <summary>Plain reference (deliberately not a database FK — see the
    /// type comment); null on batch tombstones.</summary>
    public Guid? LetterId { get; private set; }

    public LetterStatus FromStatus { get; private set; }

    public LetterStatus ToStatus { get; private set; }

    public HistoryCause Cause { get; private set; }

    /// <summary>Acting person for command causes; null for system/event rows.</summary>
    public Guid? ActorId { get; private set; }

    public string? ReasonCode { get; private set; }

    public DateTime OccurredOn { get; private set; }

    public static LetterStatusHistory Create(
        Guid letterId, LetterStatus from, LetterStatus to,
        HistoryCause cause, Guid? actorId, string? reasonCode, DateTime occurredOn) =>
        new()
        {
            Id = Guid.NewGuid(),
            LetterId = letterId,
            FromStatus = from,
            ToStatus = to,
            Cause = cause,
            ActorId = actorId,
            ReasonCode = reasonCode,
            OccurredOn = occurredOn
        };

    internal static LetterStatusHistory CreateTombstone(Guid letterId, LetterStatus status, DateTime occurredOn) =>
        Create(letterId, status, status, HistoryCause.PurgeMarker, null, "purged", occurredOn);
}
