using CommunityOS.Community.Domain.Enumerations;
using CommunityOS.Community.Domain.Events;
using CommunityOS.Community.Domain.Exceptions;
using CommunityOS.Community.Domain.ValueObjects;
using CommunityOS.SharedKernel.Domain.Primitives;
using CommunityOS.SharedKernel.Guards;

namespace CommunityOS.Community.Domain.Aggregates;

/// <summary>
/// A directional family relationship between two persons. Relationship types
/// are controlled (not free text) and the data is sensitive: reads require an
/// explicit authorization grant. Family relationships are never encoded inside
/// the Organization hierarchy.
/// </summary>
public sealed class FamilyRelationship : AggregateRoot<Guid>
{
    private FamilyRelationship() : base(Guid.Empty)
    {
        RelationshipType = null!;
        Period = null!;
    }

    private FamilyRelationship(
        Guid id,
        Guid personIdA,
        Guid personIdB,
        RelationshipType relationshipType,
        EffectivePeriod period) : base(id)
    {
        PersonIdA = personIdA;
        PersonIdB = personIdB;
        RelationshipType = relationshipType;
        Period = period;
    }

    public Guid PersonIdA { get; private set; }
    public Guid PersonIdB { get; private set; }
    public RelationshipType RelationshipType { get; private set; }
    public EffectivePeriod Period { get; private set; }

    public bool IsActiveAt(DateTime moment) => Period.IsEffectiveAt(moment);

    public static FamilyRelationship Create(
        Guid personIdA,
        Guid personIdB,
        RelationshipType relationshipType,
        EffectivePeriod period)
    {
        Guard.NotDefault(personIdA, nameof(personIdA));
        Guard.NotDefault(personIdB, nameof(personIdB));
        Guard.NotNull(relationshipType, nameof(relationshipType));
        Guard.NotNull(period, nameof(period));

        if (personIdA == personIdB)
            throw new SelfFamilyRelationshipException(personIdA);

        var relationship = new FamilyRelationship(
            Guid.NewGuid(),
            personIdA,
            personIdB,
            relationshipType,
            period);

        relationship.RaiseDomainEvent(new FamilyRelationshipCreatedEvent(
            relationship.Id,
            relationship.PersonIdA,
            relationship.PersonIdB,
            relationship.RelationshipType.Name,
            DateTime.UtcNow));
        return relationship;
    }

    /// <summary>
    /// Ends the relationship by closing the effective window at
    /// <paramref name="endedOn"/>.
    /// </summary>
    public void End(DateTime endedOn)
    {
        if (!IsActiveAt(endedOn))
            throw new FamilyRelationshipAlreadyEndedException(Id);

        Period = EffectivePeriod.Create(Period.EffectiveFrom, endedOn);
        RaiseDomainEvent(new FamilyRelationshipEndedEvent(Id, PersonIdA, PersonIdB, DateTime.UtcNow));
    }
}
