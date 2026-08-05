using CommunityOS.SharedKernel.Domain.Primitives;
using CommunityOS.SharedKernel.Guards;

namespace CommunityOS.Reporting.Domain.ValueObjects;

public sealed class Metric : ValueObject
{
    public string Name { get; }
    public decimal Value { get; }
    public string Unit { get; }

    private Metric(string name, decimal value, string unit)
    {
        Name = name;
        Value = value;
        Unit = unit;
    }

    public static Metric Create(string name, decimal value, string unit)
    {
        Guard.NotNullOrWhiteSpace(name, nameof(name));
        Guard.NotNullOrWhiteSpace(unit, nameof(unit));
        return new Metric(name.Trim(), value, unit.Trim());
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Name;
        yield return Value;
        yield return Unit;
    }

    public override string ToString() => $"{Name}: {Value} {Unit}";
}
