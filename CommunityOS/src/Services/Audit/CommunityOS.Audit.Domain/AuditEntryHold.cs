namespace CommunityOS.Audit.Domain;

/// <summary>
/// A legal or administrative hold on an audit entry (ADR-027 decisions 2 and
/// 14). Holds are the deliberately mutable companion state of the immutable
/// journal: they live in their own table, never touch
/// <see cref="AuditEntry"/>, and exempt their entry from retention purge for
/// as long as they are active. Reason codes come from a fixed set — free-text
/// reasons never enter the journal.
/// </summary>
public sealed class AuditEntryHold
{
    public const string Legal = "legal";
    public const string Administrative = "administrative";

    /// <summary>Ratified reason-code vocabulary; extensible only by
    /// configuration, never free text.</summary>
    public static readonly IReadOnlySet<string> ReasonCodes =
        new HashSet<string>(StringComparer.Ordinal)
        { "investigation", "legal-request", "dispute", "regulatory-inquiry", "other" };

    private AuditEntryHold()
    {
    }

    public Guid Id { get; private set; }
    public Guid EntryId { get; private set; }
    public string HoldType { get; private set; } = null!;
    public Guid PlacedBy { get; private set; }
    public DateTime PlacedOn { get; private set; }
    public Guid? ReleasedBy { get; private set; }
    public DateTime? ReleasedOn { get; private set; }
    public string ReasonCode { get; private set; } = null!;

    public bool IsActive => ReleasedOn is null;

    public static AuditEntryHold Create(
        Guid entryId,
        string holdType,
        string reasonCode,
        Guid placedBy,
        DateTime placedOn)
    {
        if (entryId == Guid.Empty)
        {
            throw new ArgumentException("Hold entry id must not be empty.", nameof(entryId));
        }

        if (holdType is not (Legal or Administrative))
        {
            throw new ArgumentException($"Hold type must be '{Legal}' or '{Administrative}'.", nameof(holdType));
        }

        if (string.IsNullOrWhiteSpace(reasonCode) || reasonCode.Length > 50 ||
            !ReasonCodes.Contains(reasonCode))
        {
            throw new ArgumentException("Hold reason code must come from the ratified code set.", nameof(reasonCode));
        }

        if (placedBy == Guid.Empty)
        {
            throw new ArgumentException("Hold placer must not be empty.", nameof(placedBy));
        }

        return new AuditEntryHold
        {
            Id = Guid.NewGuid(),
            EntryId = entryId,
            HoldType = holdType,
            PlacedBy = placedBy,
            PlacedOn = placedOn,
            ReasonCode = reasonCode
        };
    }

    /// <summary>The only mutation in the Audit domain: releasing an active
    /// hold. Re-releasing throws.</summary>
    public void Release(Guid releasedBy, DateTime releasedOn)
    {
        if (!IsActive)
        {
            throw new InvalidOperationException("Hold has already been released.");
        }

        if (releasedBy == Guid.Empty)
        {
            throw new ArgumentException("Hold releaser must not be empty.", nameof(releasedBy));
        }

        ReleasedBy = releasedBy;
        ReleasedOn = releasedOn;
    }
}
