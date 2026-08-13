using CommunityOS.Community.Domain.Aggregates;
using CommunityOS.Community.Domain.Enumerations;
using CommunityOS.Community.Domain.Events;
using CommunityOS.Community.Domain.Exceptions;
using CommunityOS.Community.Domain.ValueObjects;

namespace CommunityOS.Community.Tests.Domain;

public class FamilyRelationshipTests
{
    private static readonly EffectivePeriod Period = EffectivePeriod.Create(DateTime.UtcNow.AddDays(-30));

    [Fact]
    public void Create_initializes_relationship()
    {
        var personA = Guid.NewGuid();
        var personB = Guid.NewGuid();

        var relationship = FamilyRelationship.Create(personA, personB, RelationshipType.Parent, Period);

        relationship.Id.Should().NotBeEmpty();
        relationship.PersonIdA.Should().Be(personA);
        relationship.PersonIdB.Should().Be(personB);
        relationship.IsActiveAt(DateTime.UtcNow).Should().BeTrue();
        relationship.DomainEvents.Should().ContainSingle(e => e is FamilyRelationshipCreatedEvent);
    }

    [Fact]
    public void Create_rejects_self_relationship()
    {
        var person = Guid.NewGuid();

        var act = () => FamilyRelationship.Create(person, person, RelationshipType.Spouse, Period);

        act.Should().Throw<SelfFamilyRelationshipException>();
    }

    [Fact]
    public void Create_requires_default_guards()
    {
        var act = () => FamilyRelationship.Create(
            Guid.Empty, Guid.NewGuid(), RelationshipType.Sibling, Period);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void End_closes_window_and_raises_event()
    {
        var relationship = FamilyRelationship.Create(
            Guid.NewGuid(), Guid.NewGuid(), RelationshipType.Sibling, Period);

        relationship.End(DateTime.UtcNow);

        relationship.IsActiveAt(DateTime.UtcNow).Should().BeFalse();
        relationship.DomainEvents.Should().Contain(e => e is FamilyRelationshipEndedEvent);
    }

    [Fact]
    public void End_throws_when_already_ended()
    {
        var relationship = FamilyRelationship.Create(
            Guid.NewGuid(), Guid.NewGuid(), RelationshipType.Sibling, Period);
        relationship.End(DateTime.UtcNow);

        var act = () => relationship.End(DateTime.UtcNow);

        act.Should().Throw<FamilyRelationshipAlreadyEndedException>();
    }
}
