using CommunityOS.Organization.Domain.Entities;
using CommunityOS.Organization.Domain.Events;
using CommunityOS.Organization.Domain.Exceptions;
using CommunityOS.Organization.Domain.ValueObjects;
using CommunityOS.SharedKernel.Domain.Primitives;
using CommunityOS.SharedKernel.Guards;

namespace CommunityOS.Organization.Domain.Aggregates;

/// <summary>
/// A committee within an organization. Committees carry an effective-dated
/// membership roster and operate within a jurisdiction. They may be attached
/// to a specific organization unit or span an organization.
/// </summary>
public sealed class Committee : AggregateRoot<Guid>
{
    private readonly List<CommitteeMember> _members = [];

    private Committee() : base(Guid.Empty)
    {
        Name = null!;
        CommitteeType = null!;
        Jurisdiction = null!;
    }

    private Committee(
        Guid id,
        string name,
        string committeeType,
        Guid organizationId,
        Guid? organizationUnitId,
        Jurisdiction jurisdiction) : base(id)
    {
        Name = name;
        CommitteeType = committeeType;
        OrganizationId = organizationId;
        OrganizationUnitId = organizationUnitId;
        Jurisdiction = jurisdiction;
        IsActive = true;
        CreatedOn = DateTime.UtcNow;
    }

    public string Name { get; private set; }
    public string CommitteeType { get; private set; }
    public Guid OrganizationId { get; }
    public Guid? OrganizationUnitId { get; }
    public Jurisdiction Jurisdiction { get; private set; }
    public bool IsActive { get; private set; }
    public DateTime CreatedOn { get; private set; }

    public IReadOnlyList<CommitteeMember> Members => _members.AsReadOnly();

    public static Committee Create(
        string name,
        string committeeType,
        Guid organizationId,
        Guid? organizationUnitId,
        Jurisdiction jurisdiction)
    {
        Guard.NotNullOrWhiteSpace(name, nameof(name));
        Guard.MaxLength(name, 200, nameof(name));
        Guard.NotNullOrWhiteSpace(committeeType, nameof(committeeType));
        Guard.MaxLength(committeeType, 100, nameof(committeeType));
        Guard.NotDefault(organizationId, nameof(organizationId));
        Guard.NotNull(jurisdiction, nameof(jurisdiction));

        var committee = new Committee(
            Guid.NewGuid(), name.Trim(), committeeType.Trim(), organizationId, organizationUnitId, jurisdiction);

        committee.RaiseDomainEvent(new CommitteeCreatedEvent(
            committee.Id,
            committee.Name,
            committee.CommitteeType,
            committee.OrganizationId,
            committee.OrganizationUnitId,
            committee.Jurisdiction.Type.Name,
            committee.Jurisdiction.ScopeId));
        return committee;
    }

    public void AddMember(Guid personId, string roleCode, EffectivePeriod period)
    {
        Guard.NotDefault(personId, nameof(personId));
        Guard.NotNullOrWhiteSpace(roleCode, nameof(roleCode));
        Guard.MaxLength(roleCode, 100, nameof(roleCode));
        Guard.NotNull(period, nameof(period));

        _members.Add(CommitteeMember.Add(personId, roleCode.Trim(), period));

        RaiseDomainEvent(new CommitteeMemberAddedEvent(
            Id, personId, roleCode.Trim(), period.EffectiveFrom, period.EffectiveUntil));
    }

    public void RemoveMember(Guid personId, string roleCode)
    {
        var member = _members.FirstOrDefault(
            m => m.PersonId == personId &&
                 string.Equals(m.RoleCode, roleCode, StringComparison.OrdinalIgnoreCase));
        if (member is null)
            throw new CommitteeMemberNotFoundException(Id, personId);

        _members.Remove(member);

        RaiseDomainEvent(new CommitteeMemberRemovedEvent(Id, personId, member.RoleCode));
    }

    public CommitteeMember? MemberAt(Guid personId, DateTime moment) =>
        _members.FirstOrDefault(m => m.PersonId == personId && m.IsEffectiveAt(moment));

    public void UpdateDetails(string name, Jurisdiction jurisdiction)
    {
        Guard.NotNullOrWhiteSpace(name, nameof(name));
        Guard.MaxLength(name, 200, nameof(name));
        Guard.NotNull(jurisdiction, nameof(jurisdiction));

        Name = name.Trim();
        Jurisdiction = jurisdiction;
    }

    public void Deactivate()
    {
        if (!IsActive)
            return;

        IsActive = false;
    }

    public void Activate()
    {
        IsActive = true;
    }
}
