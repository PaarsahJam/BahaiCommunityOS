using CommunityOS.SharedKernel.Domain.Primitives;
using CommunityOS.SharedKernel.Guards;

namespace CommunityOS.Enrollment.Domain.ValueObjects;

public sealed class CourseName : ValueObject
{
    public string Value { get; }
    public int BookNumber { get; }

    private CourseName(string value, int bookNumber)
    {
        Value = value;
        BookNumber = bookNumber;
    }

    public static CourseName Create(string value, int bookNumber)
    {
        Guard.NotNullOrWhiteSpace(value, nameof(value));
        Guard.Positive(bookNumber, nameof(bookNumber));
        return new CourseName(value.Trim(), bookNumber);
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
        yield return BookNumber;
    }

    public override string ToString() => $"Book {BookNumber}: {Value}";
}
