using CommunityOS.Community.Domain.Aggregates;
using CommunityOS.Community.Domain.Enumerations;
using CommunityOS.Community.Domain.Events;
using CommunityOS.Community.Domain.Exceptions;
using CommunityOS.Community.Domain.ValueObjects;

namespace CommunityOS.Community.Tests.Domain;

public class HouseholdTests
{
    private static readonly EffectivePeriod Period = EffectivePeriod.Create(DateTime.UtcNow.AddDays(-30));

    private static Household CreateHousehold() =>
        Household.Create("Ridvan House",
            PostalAddress.Create("1 Main St", null, "Springfield", "IL", "62701", "US"));

    [Fact]
    public void Create_initializes_household()
    {
        var household = CreateHousehold();

        household.Id.Should().NotBeEmpty();
        household.Name.Should().Be("Ridvan House");
        household.Address.Should().NotBeNull();
        household.DomainEvents.Should().ContainSingle(e => e is HouseholdCreatedEvent);
    }

    [Fact]
    public void Rename_changes_name()
    {
        var household = CreateHousehold();

        household.Rename("Unity House");

        household.Name.Should().Be("Unity House");
    }

    [Fact]
    public void ChangeAddress_replaces_address()
    {
        var household = CreateHousehold();
        var newAddress = PostalAddress.Create("2 Elm St", null, "Naperville", "IL", "60540", "US");

        household.ChangeAddress(newAddress);

        household.Address.Should().Be(newAddress);
    }

    [Fact]
    public void AddMember_appends_member_and_raises_event()
    {
        var household = CreateHousehold();
        var personId = Guid.NewGuid();

        household.AddMember(personId, HouseholdMemberRole.Head, Period);

        household.Members.Should().ContainSingle(m => m.PersonId == personId && m.Role == HouseholdMemberRole.Head);
        household.DomainEvents.Should().Contain(e => e is HouseholdMemberAddedEvent);
    }

    [Fact]
    public void AddMember_rejects_duplicate_person()
    {
        var household = CreateHousehold();
        var personId = Guid.NewGuid();
        household.AddMember(personId, HouseholdMemberRole.Head, Period);

        var act = () => household.AddMember(personId, HouseholdMemberRole.Adult, Period);

        act.Should().Throw<DuplicateHouseholdMemberException>();
    }

    [Fact]
    public void RemoveMember_removes_member_and_raises_event()
    {
        var household = CreateHousehold();
        var personId = Guid.NewGuid();
        household.AddMember(personId, HouseholdMemberRole.Adult, Period);

        household.RemoveMember(personId);

        household.Members.Should().BeEmpty();
        household.DomainEvents.Should().Contain(e => e is HouseholdMemberRemovedEvent);
    }

    [Fact]
    public void RemoveMember_throws_when_absent()
    {
        var household = CreateHousehold();

        var act = () => household.RemoveMember(Guid.NewGuid());

        act.Should().Throw<HouseholdMemberNotFoundException>();
    }

    [Fact]
    public void ChangeMemberRole_updates_role()
    {
        var household = CreateHousehold();
        var personId = Guid.NewGuid();
        household.AddMember(personId, HouseholdMemberRole.Adult, Period);

        household.ChangeMemberRole(personId, HouseholdMemberRole.Head);

        household.Members.Single(m => m.PersonId == personId).Role.Should().Be(HouseholdMemberRole.Head);
    }
}
