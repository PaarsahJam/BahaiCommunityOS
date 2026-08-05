using CommunityOS.SharedKernel.Domain.Primitives;

namespace CommunityOS.Reporting.Domain.ValueObjects;

public sealed class DateRange : ValueObject
{
    public DateOnly From { get; }
    public DateOnly To { get; }

    private DateRange(DateOnly from, DateOnly to)
    {
        From = from;
        To = to;
    }

    public static DateRange Create(DateOnly from, DateOnly to)
    {
        if (to < from)
            throw new ArgumentException("To must be on or after From.");
        return new DateRange(from, to);
    }

    public int TotalDays => To.DayNumber - From.DayNumber + 1;
    public bool Contains(DateOnly date) => date >= From && date <= To;

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return From;
        yield return To;
    }
}
