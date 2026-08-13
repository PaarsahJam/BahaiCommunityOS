using CommunityOS.SharedKernel.Domain.Events;

namespace CommunityOS.Community.Domain.Events;

// Person

public sealed record PersonCreatedEvent(
    Guid PersonId,
    string Status,
    DateTime OccurredAt) : DomainEvent;

public sealed record PersonProfileUpdatedEvent(
    Guid PersonId,
    string Status,
    DateTime OccurredAt) : DomainEvent;

public sealed record PersonDeactivatedEvent(
    Guid PersonId,
    DateTime OccurredAt) : DomainEvent;

public sealed record PersonReactivatedEvent(
    Guid PersonId,
    DateTime OccurredAt) : DomainEvent;

public sealed record PersonIdentityLinkedEvent(
    Guid PersonId,
    Guid IdentityAccountId,
    DateTime OccurredAt) : DomainEvent;

public sealed record PersonIdentityUnlinkedEvent(
    Guid PersonId,
    Guid IdentityAccountId,
    DateTime OccurredAt) : DomainEvent;

// Household

public sealed record HouseholdCreatedEvent(
    Guid HouseholdId,
    DateTime OccurredAt) : DomainEvent;

public sealed record HouseholdMemberAddedEvent(
    Guid HouseholdId,
    Guid PersonId,
    string Role,
    DateTime OccurredAt) : DomainEvent;

public sealed record HouseholdMemberRemovedEvent(
    Guid HouseholdId,
    Guid PersonId,
    DateTime OccurredAt) : DomainEvent;

// Family relationships

public sealed record FamilyRelationshipCreatedEvent(
    Guid RelationshipId,
    Guid PersonIdA,
    Guid PersonIdB,
    string RelationshipType,
    DateTime OccurredAt) : DomainEvent;

public sealed record FamilyRelationshipEndedEvent(
    Guid RelationshipId,
    Guid PersonIdA,
    Guid PersonIdB,
    DateTime OccurredAt) : DomainEvent;

// Membership

public sealed record MembershipChangedEvent(
    Guid MembershipId,
    Guid PersonId,
    string Status,
    DateTime EffectiveFrom,
    DateTime? EffectiveUntil,
    DateTime OccurredAt) : DomainEvent;

// Activities

public sealed record ActivityCreatedEvent(
    Guid ActivityId,
    Guid? OrganizationUnitId,
    string Status,
    DateTime OccurredAt) : DomainEvent;

public sealed record ActivityUpdatedEvent(
    Guid ActivityId,
    Guid? OrganizationUnitId,
    string Status,
    DateTime OccurredAt) : DomainEvent;

// Events

public sealed record CommunityEventCreatedEvent(
    Guid CommunityEventId,
    string Title,
    DateTime StartsAt,
    Guid? OrganizationUnitId,
    string Status,
    DateTime OccurredAt) : DomainEvent;

public sealed record CommunityEventUpdatedEvent(
    Guid CommunityEventId,
    DateTime StartsAt,
    Guid? OrganizationUnitId,
    string Status,
    DateTime OccurredAt) : DomainEvent;

// Meetings

public sealed record MeetingCreatedEvent(
    Guid MeetingId,
    string Title,
    DateTime StartsAt,
    Guid? OrganizationUnitId,
    string Status,
    DateTime OccurredAt) : DomainEvent;

public sealed record MeetingUpdatedEvent(
    Guid MeetingId,
    DateTime StartsAt,
    Guid? OrganizationUnitId,
    string Status,
    DateTime OccurredAt) : DomainEvent;

public sealed record MeetingRecordedEvent(
    Guid MeetingId,
    Guid? OrganizationUnitId,
    DateTime OccurredAt) : DomainEvent;

// Participation

public sealed record ParticipationRecordedEvent(
    Guid ParticipationId,
    Guid PersonId,
    string TargetType,
    Guid TargetId,
    string Status,
    DateTime OccurredAt) : DomainEvent;
