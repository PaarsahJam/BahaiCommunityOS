using CommunityOS.Organization.Domain.Enumerations;
using CommunityOS.Organization.Domain.Events;
using CommunityOS.Organization.Domain.Exceptions;
using CommunityOS.Organization.Domain.ValueObjects;
using CommunityOS.SharedKernel.Domain.Primitives;
using CommunityOS.SharedKernel.Guards;

namespace CommunityOS.Organization.Domain.Aggregates;

/// <summary>
/// A fact recording that a subject has delegated authority to another subject
/// within the scope of an organization unit. Delegation facts are effective
/// dated, revocable and auditable.
/// </summary>
public sealed class DelegationFact : AggregateRoot<Guid>
{
    private DelegationFact() : base(Guid.Empty)
    {
        DelegationType = null!;
        Period = null!;
        Status = null!;
    }

    private DelegationFact(
        Guid id,
        Guid delegatorId,
        Guid delegateId,
        Guid organizationUnitId,
        string delegationType,
        EffectivePeriod period,
        Guid grantedBy,
        string? reason) : base(id)
    {
        DelegatorId = delegatorId;
        DelegateId = delegateId;
        OrganizationUnitId = organizationUnitId;
        DelegationType = delegationType;
        Period = period;
        GrantedBy = grantedBy;
        GrantedOn = DateTime.UtcNow;
        Reason = reason;
        Status = DelegationFactStatus.Active;
    }

    public Guid DelegatorId { get; }
    public Guid DelegateId { get; }
    public Guid OrganizationUnitId { get; }
    public string DelegationType { get; }
    public EffectivePeriod Period { get; }
    public DelegationFactStatus Status { get; private set; }
    public Guid GrantedBy { get; }
    public DateTime GrantedOn { get; }
    public Guid? RevokedBy { get; private set; }
    public DateTime? RevokedOn { get; private set; }
    public string? Reason { get; private set; }

    public bool IsRevoked => Status == DelegationFactStatus.Revoked;

    public bool IsActiveAt(DateTime moment) =>
        !IsRevoked && Period.IsEffectiveAt(moment);

    public static DelegationFact Create(
        Guid delegatorId,
        Guid delegateId,
        Guid organizationUnitId,
        string delegationType,
        EffectivePeriod period,
        Guid grantedBy,
        string? reason = null)
    {
        Guard.NotDefault(delegatorId, nameof(delegatorId));
        Guard.NotDefault(delegateId, nameof(delegateId));
        Guard.NotDefault(organizationUnitId, nameof(organizationUnitId));
        Guard.NotNullOrWhiteSpace(delegationType, nameof(delegationType));
        Guard.MaxLength(delegationType, 100, nameof(delegationType));
        Guard.NotNull(period, nameof(period));
        Guard.NotDefault(grantedBy, nameof(grantedBy));
        if (reason is not null) Guard.MaxLength(reason, 500, nameof(reason));

        if (delegatorId == delegateId)
            throw new SelfDelegationFactException();

        var fact = new DelegationFact(
            Guid.NewGuid(),
            delegatorId,
            delegateId,
            organizationUnitId,
            delegationType.Trim(),
            period,
            grantedBy,
            reason?.Trim());

        fact.RaiseDomainEvent(new DelegationFactGrantedEvent(
            fact.Id,
            delegatorId,
            delegateId,
            organizationUnitId,
            fact.DelegationType,
            period.EffectiveFrom,
            period.EffectiveUntil));
        return fact;
    }

    public void Revoke(Guid revokedBy, string? reason)
    {
        Guard.NotDefault(revokedBy, nameof(revokedBy));
        if (reason is not null) Guard.MaxLength(reason, 500, nameof(reason));

        if (IsRevoked)
            throw new DelegationFactAlreadyRevokedException(Id);

        Status = DelegationFactStatus.Revoked;
        RevokedBy = revokedBy;
        RevokedOn = DateTime.UtcNow;
        Reason = reason?.Trim();

        RaiseDomainEvent(new DelegationFactRevokedEvent(
            Id, DelegatorId, DelegateId, OrganizationUnitId));
    }
}
