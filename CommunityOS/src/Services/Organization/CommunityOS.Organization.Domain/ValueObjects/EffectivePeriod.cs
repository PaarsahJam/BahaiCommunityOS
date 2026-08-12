using CommunityOS.Organization.Domain.Exceptions;
using CommunityOS.SharedKernel.Domain.Primitives;

namespace CommunityOS.Organization.Domain.ValueObjects;

/// <summary>
/// An inclusive half-open effective window (<see cref="EffectiveFrom"/> is
/// effective, <see cref="EffectiveUntil"/> is not). Encodes the effective
/// dating used across organization facts: hierarchy links, appointments,
/// committee membership and delegation facts.
/// </summary>
public sealed class EffectivePeriod : ValueObject
{
    private EffectivePeriod(DateTime effectiveFrom, DateTime? effectiveUntil)
    {
        EffectiveFrom = effectiveFrom;
        EffectiveUntil = effectiveUntil;
    }

    public DateTime EffectiveFrom { get; }
    public DateTime? EffectiveUntil { get; }

    public bool IsOpenEnded => EffectiveUntil is null;

    public static EffectivePeriod Create(DateTime effectiveFrom, DateTime? effectiveUntil = null)
    {
        var from = effectiveFrom.ToUniversalTime();
        if (effectiveUntil is { } until && until.ToUniversalTime() <= from)
            throw new InvalidEffectivePeriodException();

        return new EffectivePeriod(from, effectiveUntil?.ToUniversalTime());
    }

    /// <summary>
    /// True when the window contains <paramref name="moment"/> (half-open: the
    /// until boundary itself is not included).
    /// </summary>
    public bool IsEffectiveAt(DateTime moment)
    {
        var m = moment.ToUniversalTime();
        return m >= EffectiveFrom && (EffectiveUntil is null || m < EffectiveUntil.Value);
    }

    public bool Overlaps(EffectivePeriod other)
    {
        ArgumentNullException.ThrowIfNull(other);
        return EffectiveFrom < (other.EffectiveUntil ?? DateTime.MaxValue) &&
               (EffectiveUntil ?? DateTime.MaxValue) > other.EffectiveFrom;
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return EffectiveFrom;
        yield return EffectiveUntil;
    }

    public override string ToString() =>
        EffectiveUntil is null
            ? $"[{EffectiveFrom:O}, open-ended)"
            : $"[{EffectiveFrom:O}, {EffectiveUntil:O})";
}
