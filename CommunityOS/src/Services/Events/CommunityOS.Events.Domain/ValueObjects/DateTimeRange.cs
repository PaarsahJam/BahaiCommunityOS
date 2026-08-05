using CommunityOS.SharedKernel.Domain.Primitives;

namespace CommunityOS.Events.Domain.ValueObjects;

public sealed class DateTimeRange : ValueObject
{
    public DateTime Start { get; }
    public DateTime End { get; }

    private DateTimeRange(DateTime start, DateTime end)
    {
        Start = start;
        End = end;
    }

    public static DateTimeRange Create(DateTime start, DateTime end)
    {
        if (end <= start)
            throw new ArgumentException("End must be after Start.");
        return new DateTimeRange(start, end);
    }

    public TimeSpan Duration => End - Start;
    public bool Contains(DateTime dt) => dt >= Start && dt <= End;

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Start;
        yield return End;
    }
}
