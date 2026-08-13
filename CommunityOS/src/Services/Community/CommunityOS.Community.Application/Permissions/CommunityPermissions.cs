namespace CommunityOS.Community.Application.Permissions;

/// <summary>
/// Central registry of well-known community permission names
/// (<c>community.entity.action</c>). These capabilities are enforced by the
/// Community application layer through the Authorization guard. Sensitive
/// data classes (contact details, date of birth, family relationships,
/// identity links) carry their own permissions so reading a profile never
/// implies reading all personal data.
/// </summary>
public static class CommunityPermissions
{
    // Person
    public const string PersonCreate = "community.person.create";
    public const string PersonRead = "community.person.read";
    public const string PersonUpdate = "community.person.update";
    public const string PersonDeactivate = "community.person.deactivate";
    public const string PersonContactRead = "community.person.contact.read";
    public const string PersonSensitiveRead = "community.person.sensitive.read";
    public const string PersonIdentityLink = "community.person.identity.link";

    // Household
    public const string HouseholdCreate = "community.household.create";
    public const string HouseholdRead = "community.household.read";
    public const string HouseholdUpdate = "community.household.update";

    // Family relationships
    public const string FamilyCreate = "community.family.create";
    public const string FamilyRead = "community.family.read";
    public const string FamilyUpdate = "community.family.update";

    // Membership
    public const string MembershipCreate = "community.membership.create";
    public const string MembershipRead = "community.membership.read";
    public const string MembershipUpdate = "community.membership.update";

    // Activities
    public const string ActivityCreate = "community.activity.create";
    public const string ActivityRead = "community.activity.read";
    public const string ActivityUpdate = "community.activity.update";

    // Events
    public const string EventCreate = "community.event.create";
    public const string EventRead = "community.event.read";
    public const string EventUpdate = "community.event.update";

    // Meetings
    public const string MeetingCreate = "community.meeting.create";
    public const string MeetingRead = "community.meeting.read";
    public const string MeetingUpdate = "community.meeting.update";
    public const string MeetingParticipantManage = "community.meeting.participant.manage";
    public const string MeetingRecord = "community.meeting.record";

    // Participation
    public const string ParticipationCreate = "community.participation.create";
    public const string ParticipationRead = "community.participation.read";
    public const string ParticipationUpdate = "community.participation.update";

    // Calendar read model
    public const string CalendarRead = "community.calendar.read";

    // Deprecated Community-owned hierarchy (premature implementation pending
    // relocation to the Organization service — ADR-016). Guarded here only to
    // keep the endpoints authorization-correct under ADR-019.
    public const string CommunityHierarchyRead = "community.hierarchy.read";
    public const string CommunityHierarchyCreate = "community.hierarchy.create";
    public const string CommunityHierarchyUpdate = "community.hierarchy.update";
    public const string CommunityHierarchyDeactivate = "community.hierarchy.deactivate";
}
