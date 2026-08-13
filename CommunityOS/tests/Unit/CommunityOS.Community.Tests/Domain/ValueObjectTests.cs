using CommunityOS.Community.Domain.Entities;
using CommunityOS.Community.Domain.Enumerations;
using CommunityOS.Community.Domain.Exceptions;
using CommunityOS.Community.Domain.ValueObjects;

namespace CommunityOS.Community.Tests.Domain;

public class EffectivePeriodTests
{
    private static readonly DateTime Now = DateTime.UtcNow;

    [Fact]
    public void Create_normalizes_to_utc()
    {
        var period = EffectivePeriod.Create(Now);

        period.EffectiveFrom.Kind.Should().Be(DateTimeKind.Utc);
        period.IsOpenEnded.Should().BeTrue();
    }

    [Fact]
    public void Create_rejects_end_before_start()
    {
        var act = () => EffectivePeriod.Create(Now, Now.AddMinutes(-1));

        act.Should().Throw<InvalidEffectivePeriodException>();
    }

    [Fact]
    public void IsEffectiveAt_is_half_open()
    {
        var period = EffectivePeriod.Create(Now, Now.AddDays(1));

        period.IsEffectiveAt(Now).Should().BeTrue();
        period.IsEffectiveAt(Now.AddDays(1)).Should().BeFalse();
    }

    [Fact]
    public void Overlaps_detects_adjacent_windows()
    {
        var left = EffectivePeriod.Create(Now, Now.AddDays(1));
        var right = EffectivePeriod.Create(Now.AddDays(1), Now.AddDays(2));

        left.Overlaps(right).Should().BeFalse();
    }

    [Fact]
    public void Open_ended_period_is_always_effective()
    {
        var period = EffectivePeriod.Create(Now.AddDays(-1));

        period.IsEffectiveAt(Now.AddYears(1)).Should().BeTrue();
    }
}

public class DateTimeRangeTests
{
    private static readonly DateTime Now = DateTime.UtcNow;

    [Fact]
    public void Create_rejects_end_before_start()
    {
        var act = () => DateTimeRange.Create(Now, Now.AddMinutes(-1));

        act.Should().Throw<InvalidTimeRangeException>();
    }

    [Fact]
    public void Overlaps_detects_overlap()
    {
        var range = DateTimeRange.Create(Now, Now.AddHours(2));
        var other = DateTimeRange.Create(Now.AddHours(1), Now.AddHours(3));

        range.Overlaps(other).Should().BeTrue();
    }

    [Fact]
    public void Overlaps_detects_adjacent_ranges()
    {
        var range = DateTimeRange.Create(Now, Now.AddHours(1));
        var other = DateTimeRange.Create(Now.AddHours(1), Now.AddHours(2));

        range.Overlaps(other).Should().BeFalse();
    }
}

public class PostalAddressTests
{
    [Fact]
    public void Create_requires_country()
    {
        var act = () => PostalAddress.Create(null, null, null, null, null, " ");

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_blank_fields_become_null()
    {
        var address = PostalAddress.Create("1 Main St", " ", null, null, null, "US");

        address.Line1.Should().Be("1 Main St");
        address.Line2.Should().BeNull();
        address.Country.Should().Be("US");
    }
}

public class ContactMethodTests
{
    [Fact]
    public void Create_validates_email_format()
    {
        var act = () => ContactMethod.Create(
            ContactMethodType.Email, "not-an-email", false, ContactVisibility.Private);

        act.Should().Throw<InvalidContactMethodException>();
    }

    [Fact]
    public void Create_accepts_valid_email()
    {
        var method = ContactMethod.Create(
            ContactMethodType.Email, " user@example.com ", true, ContactVisibility.Members);

        method.Value.Should().Be("user@example.com");
        method.IsPreferred.Should().BeTrue();
    }
}
