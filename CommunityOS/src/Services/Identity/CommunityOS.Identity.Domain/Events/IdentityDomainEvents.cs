using CommunityOS.SharedKernel.Domain.Events;

namespace CommunityOS.Identity.Domain.Events;

public sealed record UserAccountRegisteredEvent(Guid UserAccountId, string Email) : DomainEvent;

public sealed record UserAccountVerifiedEvent(Guid UserAccountId) : DomainEvent;

public sealed record UserAccountLockedEvent(Guid UserAccountId) : DomainEvent;

public sealed record UserAccountUnlockedEvent(Guid UserAccountId) : DomainEvent;

public sealed record UserAccountDeactivatedEvent(Guid UserAccountId) : DomainEvent;

/// <summary>ADR-036 D1 seam: the epoch was advanced by exactly one. Minimal
/// domain seam for future Q4 propagation; carries no transport/outbox concern.</summary>
public sealed record SessionRevocationEpochAdvancedEvent(Guid UserAccountId, long Epoch) : DomainEvent;

public sealed record CredentialChangedEvent(Guid UserAccountId) : DomainEvent;

public sealed record ExternalIdentityLinkedEvent(Guid UserAccountId, string Provider, string Subject) : DomainEvent;

public sealed record ExternalIdentityUnlinkedEvent(Guid UserAccountId, string Provider, string Subject) : DomainEvent;

public sealed record MfaMethodEnrolledEvent(Guid UserAccountId, string MethodType) : DomainEvent;

public sealed record MfaMethodRemovedEvent(Guid UserAccountId, string MethodType) : DomainEvent;

public sealed record DeviceRegisteredEvent(Guid UserAccountId, Guid DeviceId, string Name) : DomainEvent;

public sealed record RefreshTokenIssuedEvent(Guid UserAccountId, Guid SessionId) : DomainEvent;

public sealed record AuthorizationCodeIssuedEvent(Guid UserAccountId, string ClientId) : DomainEvent;
