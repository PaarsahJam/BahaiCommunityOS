namespace CommunityOS.Organization.Application.Permissions;

/// <summary>
/// Central registry of well-known organization permission names
/// (<c>organization.entity.action</c>). These capabilities are enforced by
/// the Organization application layer through the Authorization guard.
/// </summary>
public static class OrganizationPermissions
{
    public const string OrgCreate = "organization.org.create";
    public const string OrgRead = "organization.org.read";
    public const string OrgUpdate = "organization.org.update";
    public const string OrgDissolve = "organization.org.dissolve";

    public const string UnitCreate = "organization.unit.create";
    public const string UnitRead = "organization.unit.read";
    public const string UnitUpdate = "organization.unit.update";
    public const string UnitReparent = "organization.unit.reparent";
    public const string UnitDeactivate = "organization.unit.deactivate";

    public const string AppointmentAssign = "organization.appointment.assign";
    public const string AppointmentRead = "organization.appointment.read";
    public const string AppointmentEnd = "organization.appointment.end";

    /// <summary>
    /// Required (in addition to <see cref="AppointmentRead"/>) to query
    /// appointments by person across organization units. Person-scoped
    /// appointment lookups are inherently cross-cutting and therefore need an
    /// explicit grant rather than the default unit-scoped read.
    /// </summary>
    public const string AppointmentReadPerson = "organization.appointment.read.person";

    /// <summary>
    /// Required to view the appointment <c>Reason</c> field. The reason may
    /// contain sensitive context and is masked for callers without this grant.
    /// </summary>
    public const string AppointmentReadReason = "organization.appointment.reason.read";

    public const string CommitteeCreate = "organization.committee.create";
    public const string CommitteeRead = "organization.committee.read";
    public const string CommitteeUpdate = "organization.committee.update";
    public const string CommitteeDeactivate = "organization.committee.deactivate";
    public const string CommitteeMemberAdd = "organization.committee.member.add";
    public const string CommitteeMemberRemove = "organization.committee.member.remove";

    public const string InstitutionCreate = "organization.institution.create";
    public const string InstitutionRead = "organization.institution.read";
    public const string InstitutionUpdate = "organization.institution.update";

    public const string DelegationGrant = "organization.delegation.grant";
    public const string DelegationRead = "organization.delegation.read";
    public const string DelegationRevoke = "organization.delegation.revoke";

    /// <summary>
    /// Required (in addition to <see cref="DelegationRead"/>) to query
    /// delegation facts by delegator or delegate. Person-scoped delegation
    /// lookups are inherently cross-cutting and therefore need an explicit
    /// grant rather than the default unit-scoped read.
    /// </summary>
    public const string DelegationReadPerson = "organization.delegation.read.person";

    /// <summary>
    /// Required to view the delegation fact <c>Reason</c> field. The reason may
    /// contain sensitive context and is masked for callers without this grant.
    /// </summary>
    public const string DelegationReadReason = "organization.delegation.reason.read";
}
