using CommunityOS.Organization.Domain.Exceptions;
using CommunityOS.Organization.Domain.ValueObjects;

namespace CommunityOS.Organization.Tests.Domain;

public class EffectivePeriodTests
{
    [Fact]
    public void Create_requires_until_later_than_from()
    {
        var from = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        var actEqual = () => EffectivePeriod.Create(from, from);
        var actEarlier = () => EffectivePeriod.Create(from, from.AddHours(-1));

        actEqual.Should().Throw<InvalidEffectivePeriodException>();
        actEarlier.Should().Throw<InvalidEffectivePeriodException>();
    }

    [Fact]
    public void Create_converts_to_utc()
    {
        var from = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Unspecified);

        var period = EffectivePeriod.Create(from);

        period.EffectiveFrom.Kind.Should().Be(DateTimeKind.Utc);
        period.IsOpenEnded.Should().BeTrue();
    }

    [Fact]
    public void IsEffectiveAt_is_half_open()
    {
        var from = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var until = from.AddDays(10);
        var period = EffectivePeriod.Create(from, until);

        period.IsEffectiveAt(from).Should().BeTrue();
        period.IsEffectiveAt(from.AddDays(9).AddMinutes(59)).Should().BeTrue();
        period.IsEffectiveAt(until).Should().BeFalse();
        period.IsEffectiveAt(from.AddDays(-1)).Should().BeFalse();
    }

    [Fact]
    public void OpenEnded_period_is_effective_for_all_later_moments()
    {
        var period = EffectivePeriod.Create(new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));

        period.IsEffectiveAt(DateTime.UtcNow).Should().BeTrue();
        period.IsEffectiveAt(DateTime.UtcNow.AddYears(10)).Should().BeTrue();
    }

    [Theory]
    [InlineData(0, 10, 0, 10, true)]
    [InlineData(5, 15, 0, 10, true)]
    [InlineData(-5, 2, 0, 10, true)]
    [InlineData(10, 20, 0, 10, false)]
    [InlineData(11, 20, 0, 10, false)]
    [InlineData(-10, 0, 0, 10, false)]
    [InlineData(-10, -1, 0, 10, false)]
    [InlineData(-5, 15, 0, 10, true)]
    public void Overlaps_detects_window_collision(
        int subjectFrom, int subjectUntil, int otherFrom, int otherUntil, bool expected)
    {
        var baseDay = new DateTime(2026, 1, 10, 0, 0, 0, DateTimeKind.Utc);

        var subject = EffectivePeriod.Create(
            baseDay.AddDays(subjectFrom), baseDay.AddDays(subjectUntil));
        var other = EffectivePeriod.Create(
            baseDay.AddDays(otherFrom), baseDay.AddDays(otherUntil));

        subject.Overlaps(other).Should().Be(expected);
    }

    [Fact]
    public void Value_equality_is_by_window()
    {
        var a = EffectivePeriod.Create(new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        var b = EffectivePeriod.Create(new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));

        a.Equals(b).Should().BeTrue();
        a.GetHashCode().Should().Be(b.GetHashCode());
    }
}
