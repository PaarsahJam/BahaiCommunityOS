using CommunityOS.Community.Domain.Exceptions;
using CommunityOS.SharedKernel.Domain.Primitives;

namespace CommunityOS.Community.Domain.ValueObjects;

/// <summary>
/// A scheduling window for activities and events. Shared scheduling logic
/// (start/end ordering) lives here so it is not duplicated across activities,
/// events and meetings.
/// </summary>
public sealed class DateTimeRange : ValueObject
{
    private DateTimeRange(DateTime startsAt, DateTime? endsAt)
    {
        StartsAt = startsAt;
        EndsAt = endsAt;
    }

    public DateTime StartsAt { get; }
    public DateTime? EndsAt { get; }

    public bool IsOpenEnded => EndsAt is null;

    public static DateTimeRange Create(DateTime startsAt, DateTime? endsAt = null)
    {
        var start = startsAt.ToUniversalTime();
        if (endsAt is { } end && end.ToUniversalTime() <= start)
            throw new InvalidTimeRangeException();

        return new DateTimeRange(start, endsAt?.ToUniversalTime());
    }

    public bool Overlaps(DateTimeRange other)
    {
        ArgumentNullException.ThrowIfNull(other);
        return StartsAt < (other.EndsAt ?? DateTime.MaxValue) &&
               (EndsAt ?? DateTime.MaxValue) > other.StartsAt;
    }

    /// <summary>True when the window overlaps <paramref name="window"/>.</summary>
    public bool Overlaps(EffectivePeriod window)
    {
        ArgumentNullException.ThrowIfNull(window);
        return StartsAt < (window.EffectiveUntil ?? DateTime.MaxValue) &&
               (EndsAt ?? DateTime.MaxValue) > window.EffectiveFrom;
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return StartsAt;
        yield return EndsAt;
    }

    public override string ToString() =>
        EndsAt is null
            ? $"[{StartsAt:O}, open-ended)"
            : $"[{StartsAt:O}, {EndsAt:O})";
}
