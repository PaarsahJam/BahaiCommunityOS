using CommunityOS.SharedKernel.Domain.Events;

namespace CommunityOS.Identity.Domain.Events;

public sealed record MemberCreatedEvent(Guid MemberId, string Email) : DomainEvent;

public sealed record MemberActivatedEvent(Guid MemberId) : DomainEvent;

public sealed record MemberSuspendedEvent(Guid MemberId, string Reason) : DomainEvent;

public sealed record MemberTransferredEvent(Guid MemberId, Guid TargetLocalUnitId) : DomainEvent;
