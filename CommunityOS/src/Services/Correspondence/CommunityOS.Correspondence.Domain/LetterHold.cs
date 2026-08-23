using CommunityOS.Correspondence.Domain.Exceptions;

namespace CommunityOS.Correspondence.Domain;

/// <summary>
/// A legal/administrative hold on a letter (ADR-028 decision 13). An active
/// hold overrides retention expiry; placement and release mirror the ratified
/// AuditEntryHold pattern. Reason codes come from the fixed vocabulary — never
/// free text.
/// </summary>
public sealed class LetterHold
{
    public const string HoldTypeLegal = "legal";
    public const string HoldTypeAdministrative = "administrative";

    private LetterHold()
    {
    }

    public Guid Id { get; private set; }

    public Guid LetterId { get; private set; }

    public string HoldType { get; private set; } = null!;

    public string ReasonCode { get; private set; } = null!;

    public Guid PlacedBy { get; private set; }

    public DateTime PlacedOn { get; private set; }

    public Guid? ReleasedBy { get; private set; }

    public DateTime? ReleasedOn { get; private set; }

    /// <summary>Active holds block the retention purge.</summary>
    public bool IsActive => ReleasedOn is null;

    public static LetterHold Create(
        Guid letterId, string holdType, string reasonCode, Guid placedBy, DateTime placedOn)
    {
        if (letterId == Guid.Empty)
        {
            throw new ArgumentException("Letter id is required.", nameof(letterId));
        }

        if (holdType is not (HoldTypeLegal or HoldTypeAdministrative))
        {
            throw new ArgumentException(
                $"Hold type must be '{HoldTypeLegal}' or '{HoldTypeAdministrative}'.", nameof(holdType));
        }

        if (!CorrespondenceReasonCodes.IsKnown(CorrespondenceReasonCodes.HoldPlacement, reasonCode))
        {
            throw new ArgumentException($"Unknown hold reason code '{reasonCode}'.", nameof(reasonCode));
        }

        if (placedBy == Guid.Empty)
        {
            throw new ArgumentException("Placed-by is required.", nameof(placedBy));
        }

        return new LetterHold
        {
            Id = Guid.NewGuid(),
            LetterId = letterId,
            HoldType = holdType,
            ReasonCode = reasonCode,
            PlacedBy = placedBy,
            PlacedOn = placedOn
        };
    }

    public void Release(Guid releasedBy, DateTime releasedOn)
    {
        if (!IsActive)
        {
            throw new LetterConflictException("The hold has already been released.");
        }

        if (releasedBy == Guid.Empty)
        {
            throw new ArgumentException("Released-by is required.", nameof(releasedBy));
        }

        ReleasedBy = releasedBy;
        ReleasedOn = releasedOn;
    }
}
