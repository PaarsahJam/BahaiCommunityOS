using CommunityOS.Identity.Domain.Entities;
using CommunityOS.Identity.Domain.Events;
using CommunityOS.Identity.Domain.ValueObjects;
using CommunityOS.SharedKernel.Domain.Primitives;
using CommunityOS.SharedKernel.Guards;

namespace CommunityOS.Identity.Domain.Aggregates;

public sealed class Member : AggregateRoot<Guid>
{
    private readonly List<Role> _roles = [];

    public PersonName Name { get; private set; }
    public Email Email { get; private set; }
    public PhoneNumber? PhoneNumber { get; private set; }
    public Address? Address { get; private set; }
    public MembershipStatus Status { get; private set; }
    public DateTime EnrolledOn { get; private set; }
    public DateTime? TransferredOn { get; private set; }
    public Guid? LocalUnitId { get; private set; }

    public IReadOnlyList<Role> Roles => _roles.AsReadOnly();

    private Member(Guid id, PersonName name, Email email, Guid? localUnitId) : base(id)
    {
        Name = name;
        Email = email;
        LocalUnitId = localUnitId;
        Status = MembershipStatus.Pending;
        EnrolledOn = DateTime.UtcNow;
    }

    public static Member Create(PersonName name, Email email, Guid? localUnitId = null)
    {
        Guard.NotNull(name, nameof(name));
        Guard.NotNull(email, nameof(email));
        var member = new Member(Guid.NewGuid(), name, email, localUnitId);
        member.RaiseDomainEvent(new MemberCreatedEvent(member.Id, email.Value));
        return member;
    }

    public void Activate()
    {
        Status = MembershipStatus.Active;
        RaiseDomainEvent(new MemberActivatedEvent(Id));
    }

    public void Suspend(string reason)
    {
        Guard.NotNullOrWhiteSpace(reason, nameof(reason));
        Status = MembershipStatus.Suspended;
        RaiseDomainEvent(new MemberSuspendedEvent(Id, reason));
    }

    public void Transfer(Guid targetLocalUnitId)
    {
        Guard.NotDefault(targetLocalUnitId, nameof(targetLocalUnitId));
        LocalUnitId = targetLocalUnitId;
        Status = MembershipStatus.Transferred;
        TransferredOn = DateTime.UtcNow;
        RaiseDomainEvent(new MemberTransferredEvent(Id, targetLocalUnitId));
    }

    public void UpdateContact(PhoneNumber? phoneNumber, Address? address)
    {
        PhoneNumber = phoneNumber;
        Address = address;
    }

    public void AssignRole(Role role)
    {
        Guard.NotNull(role, nameof(role));
        if (!_roles.Contains(role))
            _roles.Add(role);
    }

    public void RemoveRole(Role role) => _roles.Remove(role);
}
