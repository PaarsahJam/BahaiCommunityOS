using CommunityOS.Community.Application.DTOs;
using CommunityOS.Community.Domain.Aggregates;
using CommunityOS.Community.Domain.Entities;

namespace CommunityOS.Community.Application;

/// <summary>
/// Manual mapping extensions between domain aggregates and DTOs. Mapping is
/// explicit so privacy-sensitive fields are only copied into the DTO when the
/// caller has the corresponding permission.
/// </summary>
public static class CommunityLifeMappingExtensions
{
    public static PersonDto ToDto(this Person person) => new(
        person.Id,
        person.PreferredName,
        person.FormalName,
        person.PreferredLanguage,
        person.Status,
        person.IdentityAccountId is not null,
        person.CreatedOn);

    /// <summary>
    /// Maps a person to the detail DTO, conditionally including contact
    /// methods and sensitive fields based on the caller's grants.
    /// </summary>
    public static PersonDetailDto ToDetailDto(
        this Person person,
        bool includeContact,
        bool includeSensitive) => new(
        person.Id,
        person.PreferredName,
        person.FormalName,
        includeSensitive ? person.DateOfBirth : null,
        person.PreferredLanguage,
        person.Status,
        includeSensitive ? person.IdentityAccountId : null,
        person.Privacy.ProfileVisibility.Name,
        person.Privacy.ContactVisibility.Name,
        person.Privacy.DateOfBirthVisibility.Name,
        includeContact ? person.ContactMethods.Select(ToDto).ToList() : [],
        person.CreatedOn);

    public static ContactMethodDto ToDto(this ContactMethod contactMethod) => new(
        contactMethod.Id,
        contactMethod.Type.Name,
        contactMethod.Value,
        contactMethod.IsPreferred,
        contactMethod.Visibility.Name);

    public static HouseholdDto ToDto(this Household household) => new(
        household.Id,
        household.Name,
        household.Address?.Line1,
        household.Address?.Line2,
        household.Address?.City,
        household.Address?.Region,
        household.Address?.PostalCode,
        household.Address?.Country,
        household.Members.Select(ToDto).ToList());

    public static HouseholdMemberDto ToDto(this HouseholdMember member) => new(
        member.Id,
        member.PersonId,
        member.Role.Name,
        member.Period.EffectiveFrom,
        member.Period.EffectiveUntil);

    public static FamilyRelationshipDto ToDto(this FamilyRelationship relationship) => new(
        relationship.Id,
        relationship.PersonIdA,
        relationship.PersonIdB,
        relationship.RelationshipType.Name,
        relationship.Period.EffectiveFrom,
        relationship.Period.EffectiveUntil);

    public static MembershipDto ToDto(this Membership membership) => new(
        membership.Id,
        membership.PersonId,
        membership.Status.Name,
        membership.Period.EffectiveFrom,
        membership.Period.EffectiveUntil,
        membership.WithdrawnOn,
        membership.PeriodHistory.Select(h => new MembershipPeriodDto(
            h.Status.Name,
            h.Period.EffectiveFrom,
            h.Period.EffectiveUntil)).ToList());

    public static ActivityDto ToDto(this Activity activity) => new(
        activity.Id,
        activity.Title,
        activity.Description,
        activity.Category,
        activity.OrganizerPersonId,
        activity.OrganizationUnitId,
        activity.Location,
        activity.IsOnline,
        activity.OnlineUrl,
        activity.Schedule.StartsAt,
        activity.Schedule.EndsAt,
        activity.Visibility.Name,
        activity.Status.Name,
        activity.Capacity,
        activity.CreatedOn);

    public static CommunityEventDto ToDto(this CommunityEvent communityEvent) => new(
        communityEvent.Id,
        communityEvent.Title,
        communityEvent.Description,
        communityEvent.StartsAt,
        communityEvent.EndsAt,
        communityEvent.TimeZone,
        communityEvent.Location,
        communityEvent.IsOnline,
        communityEvent.OnlineUrl,
        communityEvent.OrganizerPersonId,
        communityEvent.OrganizationUnitId,
        communityEvent.Status.Name,
        communityEvent.Visibility.Name,
        communityEvent.RegistrationOpen,
        communityEvent.Capacity,
        communityEvent.CreatedOn);

    public static MeetingDto ToDto(this Meeting meeting) => new(
        meeting.Id,
        meeting.Title,
        meeting.Description,
        meeting.StartsAt,
        meeting.EndsAt,
        meeting.TimeZone,
        meeting.Location,
        meeting.OrganizerPersonId,
        meeting.OrganizationUnitId,
        meeting.Status.Name,
        meeting.Visibility.Name,
        meeting.Minutes,
        meeting.Participants.Select(ToDto).ToList(),
        meeting.AgendaItems.Select(ToDto).ToList(),
        meeting.Actions.Select(ToDto).ToList(),
        meeting.CreatedOn);

    public static MeetingParticipantDto ToDto(this MeetingParticipant participant) => new(
        participant.Id,
        participant.PersonId,
        participant.Role,
        participant.Attendance.Name);

    public static MeetingAgendaItemDto ToDto(this MeetingAgendaItem item) => new(
        item.Id,
        item.Title,
        item.Description,
        item.Order,
        item.IsCompleted);

    public static MeetingActionDto ToDto(this MeetingAction action) => new(
        action.Id,
        action.Description,
        action.AssigneePersonId,
        action.DueDate,
        action.IsCompleted,
        action.CompletedOn);

    public static ParticipationDto ToDto(this Participation participation) => new(
        participation.Id,
        participation.PersonId,
        participation.TargetType.Name,
        participation.TargetId,
        participation.Role,
        participation.Status.Name,
        participation.Period.EffectiveFrom,
        participation.Period.EffectiveUntil,
        participation.RecordedOn);
}
