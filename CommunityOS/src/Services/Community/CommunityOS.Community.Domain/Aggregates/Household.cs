using CommunityOS.Community.Domain.Entities;
using CommunityOS.Community.Domain.Enumerations;
using CommunityOS.Community.Domain.Events;
using CommunityOS.Community.Domain.Exceptions;
using CommunityOS.Community.Domain.ValueObjects;
using CommunityOS.SharedKernel.Domain.Primitives;
using CommunityOS.SharedKernel.Guards;

namespace CommunityOS.Community.Domain.Aggregates;

/// <summary>
/// A cohabitation unit in the community. A household is distinct from a
/// family: members need not be related and need not share an address.
/// </summary>
public sealed class Household : AggregateRoot<Guid>
{
    private readonly List<HouseholdMember> _members = [];

    private Household() : base(Guid.Empty)
    {
    }

    private Household(Guid id, string? name, PostalAddress? address) : base(id)
    {
        Name = name;
        Address = address;
    }

    public string? Name { get; private set; }
    public PostalAddress? Address { get; private set; }

    public IReadOnlyList<HouseholdMember> Members => _members.AsReadOnly();

    public static Household Create(string? name, PostalAddress? address)
    {
        Guard.MaxLength(name ?? string.Empty, 200, nameof(name));

        var household = new Household(Guid.NewGuid(), NullIfBlank(name), address);
        household.RaiseDomainEvent(new HouseholdCreatedEvent(household.Id, DateTime.UtcNow));
        return household;
    }

    public void Rename(string? name)
    {
        Guard.MaxLength(name ?? string.Empty, 200, nameof(name));
        Name = NullIfBlank(name);
    }

    public void ChangeAddress(PostalAddress? address) => Address = address;

    public void AddMember(Guid personId, HouseholdMemberRole role, EffectivePeriod period)
    {
        Guard.NotDefault(personId, nameof(personId));
        Guard.NotNull(role, nameof(role));
        Guard.NotNull(period, nameof(period));

        if (_members.Any(m => m.PersonId == personId))
            throw new DuplicateHouseholdMemberException(Id, personId);

        _members.Add(HouseholdMember.Create(personId, role, period));
        RaiseDomainEvent(new HouseholdMemberAddedEvent(Id, personId, role.Name, DateTime.UtcNow));
    }

    public void RemoveMember(Guid personId)
    {
        var member = _members.FirstOrDefault(m => m.PersonId == personId);
        if (member is null)
            throw new HouseholdMemberNotFoundException(Id, personId);

        _members.Remove(member);
        RaiseDomainEvent(new HouseholdMemberRemovedEvent(Id, personId, DateTime.UtcNow));
    }

    public void ChangeMemberRole(Guid personId, HouseholdMemberRole role)
    {
        Guard.NotNull(role, nameof(role));

        var member = _members.FirstOrDefault(m => m.PersonId == personId);
        if (member is null)
            throw new HouseholdMemberNotFoundException(Id, personId);

        member.ChangeRole(role);
    }

    private static string? NullIfBlank(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
