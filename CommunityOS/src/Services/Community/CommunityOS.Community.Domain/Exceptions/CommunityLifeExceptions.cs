namespace CommunityOS.Community.Domain.Exceptions;

// Person

public sealed class PersonNotFoundException(Guid personId)
    : Exception($"Person '{personId}' was not found.");

public sealed class PersonAlreadyLinkedException(Guid personId, Guid accountId)
    : Exception($"Person '{personId}' is already linked to Identity account '{accountId}'.");

public sealed class PersonNotLinkedException(Guid personId)
    : Exception($"Person '{personId}' is not linked to an Identity account.");

public sealed class PersonNotLinkedToAccountException(Guid identityAccountId)
    : Exception($"No person is linked to Identity account '{identityAccountId}'.");

public sealed class PersonAlreadyDeactivatedException(Guid personId)
    : Exception($"Person '{personId}' is already deactivated.");

public sealed class PersonNotDeactivatedException(Guid personId)
    : Exception($"Person '{personId}' is not deactivated.");

public sealed class ContactMethodNotFoundException(Guid personId, Guid contactMethodId)
    : Exception($"Contact method '{contactMethodId}' was not found on person '{personId}'.");

public sealed class DuplicateContactMethodException(string value)
    : Exception($"A contact method with value '{value}' already exists.");

public sealed class MultiplePreferredContactException()
    : Exception("Only one contact method may be marked as preferred.");

public sealed class InvalidContactMethodException(string type, string value)
    : Exception($"Contact method value '{value}' is not a valid {type}.");

// Household

public sealed class HouseholdNotFoundException(Guid householdId)
    : Exception($"Household '{householdId}' was not found.");

public sealed class DuplicateHouseholdMemberException(Guid householdId, Guid personId)
    : Exception($"Person '{personId}' is already a member of household '{householdId}'.");

public sealed class HouseholdMemberNotFoundException(Guid householdId, Guid personId)
    : Exception($"Person '{personId}' is not a member of household '{householdId}'.");

// Family relationships

public sealed class FamilyRelationshipNotFoundException(Guid relationshipId)
    : Exception($"Family relationship '{relationshipId}' was not found.");

public sealed class SelfFamilyRelationshipException(Guid personId)
    : Exception($"A family relationship cannot link a person to themselves (person '{personId}').");

public sealed class DuplicateFamilyRelationshipException(Guid personIdA, Guid personIdB, string relationshipType)
    : Exception($"An active {relationshipType} relationship between '{personIdA}' and '{personIdB}' already exists.");

public sealed class FamilyRelationshipAlreadyEndedException(Guid relationshipId)
    : Exception($"Family relationship '{relationshipId}' is already ended.");

// Membership

public sealed class MembershipNotFoundException(Guid membershipId)
    : Exception($"Membership '{membershipId}' was not found.");

public sealed class DuplicateMembershipException(Guid personId)
    : Exception($"Person '{personId}' already has a community membership.");

public sealed class MembershipStatusUnchangedException(Guid membershipId, string status)
    : Exception($"Membership '{membershipId}' is already in status '{status}'.");

// Activities

public sealed class ActivityNotFoundException(Guid activityId)
    : Exception($"Activity '{activityId}' was not found.");

public sealed class ActivityAlreadyCancelledException(Guid activityId)
    : Exception($"Activity '{activityId}' is already cancelled.");

// Events

public sealed class CommunityEventNotFoundException(Guid eventId)
    : Exception($"Community event '{eventId}' was not found.");

public sealed class CommunityEventAlreadyCancelledException(Guid eventId)
    : Exception($"Community event '{eventId}' is already cancelled.");

// Meetings

public sealed class MeetingNotFoundException(Guid meetingId)
    : Exception($"Meeting '{meetingId}' was not found.");

public sealed class MeetingAlreadyCancelledException(Guid meetingId)
    : Exception($"Meeting '{meetingId}' is already cancelled.");

public sealed class DuplicateMeetingParticipantException(Guid meetingId, Guid personId)
    : Exception($"Person '{personId}' is already a participant of meeting '{meetingId}'.");

public sealed class MeetingParticipantNotFoundException(Guid meetingId, Guid personId)
    : Exception($"Person '{personId}' is not a participant of meeting '{meetingId}'.");

public sealed class MeetingAgendaItemNotFoundException(Guid meetingId, Guid agendaItemId)
    : Exception($"Agenda item '{agendaItemId}' was not found on meeting '{meetingId}'.");

public sealed class MeetingActionNotFoundException(Guid meetingId, Guid actionId)
    : Exception($"Action item '{actionId}' was not found on meeting '{meetingId}'.");

// Participation

public sealed class ParticipationNotFoundException(Guid participationId)
    : Exception($"Participation '{participationId}' was not found.");

public sealed class DuplicateParticipationException(Guid personId, string targetType, Guid targetId)
    : Exception($"Person '{personId}' already has a participation record for {targetType} '{targetId}'.");

public sealed class ParticipationAlreadyCancelledException(Guid participationId)
    : Exception($"Participation '{participationId}' is already cancelled.");

// Shared value-object rules

public sealed class InvalidEffectivePeriodException()
    : Exception("The effective period is invalid: the end must be after the start.");

public sealed class InvalidTimeRangeException()
    : Exception("The time range is invalid: the end must be after the start.");
