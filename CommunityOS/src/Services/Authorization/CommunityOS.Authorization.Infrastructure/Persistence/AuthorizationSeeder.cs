using CommunityOS.Authorization.Domain.Aggregates;
using CommunityOS.Authorization.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace CommunityOS.Authorization.Infrastructure.Persistence;

/// <summary>
/// Seeds development-only role catalog entries and an optional bootstrap
/// global administrator. Roles are deployment configuration: business logic
/// must never hard-code decisions around a specific role code. The bootstrap
/// assignment is only created when
/// <c>Authorization:BootstrapGlobalAdminSubjectId</c> is explicitly set in
/// configuration and is itself a normal, auditable role assignment.
/// </summary>
public static class AuthorizationSeeder
{
    private static readonly IReadOnlyList<(string Code, string DisplayName, string[] Permissions)> RoleCatalog =
    [
        ("GlobalAdministrator", "Global Administrator",
        [
            "authz.check",
            "authz.role.list", "authz.role.create", "authz.role.update", "authz.role.assign", "authz.role.revoke",
            "authz.relationship.write", "authz.relationship.read",
            "authz.delegation.grant", "authz.delegation.revoke",
            "authz.breakglass.approve", "authz.breakglass.revoke", "authz.breakglass.list",

            "records.record.create", "records.record.read", "records.record.read.sensitive",
            "records.record.update", "records.record.submit", "records.record.verify",
            "records.record.correct", "records.record.archive", "records.record.deactivate",
            "records.record.restore", "records.record.classify", "records.record.scope.manage",
            "records.record.evidence.manage", "records.retention.manage", "records.hold.manage",
            "records.category.manage", "records.record.admin",

            "workflow.task.read", "workflow.task.read.sensitive",
            "workflow.task.create", "workflow.task.assign", "workflow.task.start",
            "workflow.task.complete", "workflow.task.cancel", "workflow.task.escalate",
            "workflow.definition.read", "workflow.definition.manage", "workflow.task.admin",

            "notifications.notification.read", "notifications.notification.read.sensitive",
            "notifications.notification.create", "notifications.notification.send",
            "notifications.notification.admin", "notifications.type.manage",
            "notifications.template.read", "notifications.preference.manage",
            "search.result.read", "search.result.read.sensitive", "search.index.manage",
            "audit.entry.read", "audit.entry.read.sensitive", "audit.entry.export", "audit.entry.admin",

            "correspondence.letter.read", "correspondence.letter.read.sensitive",
            "correspondence.letter.create", "correspondence.letter.update",
            "correspondence.letter.submit", "correspondence.letter.cancel",
            "correspondence.letter.export", "correspondence.letter.admin",
            "correspondence.template.read", "correspondence.template.manage"
        ]),
        ("PlatformService", "Platform Service Principal",
        [
            "authz.check", "authz.relationship.write", "authz.relationship.read"
        ]),
        ("NationalAdministrator", "National Administrator",
        [
            "authz.check",
            "authz.role.list", "authz.role.assign", "authz.role.revoke",
            "authz.relationship.write", "authz.relationship.read",
            "authz.delegation.grant", "authz.delegation.revoke",
            "authz.breakglass.approve", "authz.breakglass.revoke", "authz.breakglass.list",

            "records.record.create", "records.record.read", "records.record.read.sensitive",
            "records.record.update", "records.record.submit", "records.record.verify",
            "records.record.correct", "records.record.archive", "records.record.deactivate",
            "records.record.restore", "records.record.classify", "records.record.scope.manage",
            "records.record.evidence.manage", "records.retention.manage", "records.hold.manage",
            "records.category.manage", "records.record.admin",

            "workflow.task.read", "workflow.task.read.sensitive",
            "workflow.task.create", "workflow.task.assign", "workflow.task.start",
            "workflow.task.complete", "workflow.task.cancel", "workflow.task.escalate",
            "workflow.definition.read", "workflow.definition.manage", "workflow.task.admin",

            "notifications.notification.read", "notifications.notification.read.sensitive",
            "notifications.notification.create", "notifications.notification.send",
            "notifications.notification.admin", "notifications.type.manage",
            "notifications.template.read", "notifications.preference.manage",
            "search.result.read", "search.result.read.sensitive", "search.index.manage",
            "audit.entry.read", "audit.entry.read.sensitive", "audit.entry.export", "audit.entry.admin",

            "correspondence.letter.read", "correspondence.letter.read.sensitive",
            "correspondence.letter.create", "correspondence.letter.update",
            "correspondence.letter.submit", "correspondence.letter.cancel",
            "correspondence.letter.export", "correspondence.letter.admin",
            "correspondence.template.read", "correspondence.template.manage"
        ]),
        ("LocalAdministrator", "Local Administrator",
        [
            "authz.role.list", "authz.role.assign", "authz.role.revoke",
            "authz.relationship.read",
            "authz.delegation.grant",
            "authz.breakglass.approve",

            "correspondence.letter.read", "correspondence.letter.create",
            "correspondence.letter.update", "correspondence.letter.submit",
            "correspondence.letter.cancel", "correspondence.template.read"
        ]),
        ("CommitteeMember", "Committee Member",
        [
            "authz.relationship.read"
        ]),
        ("Volunteer", "Volunteer", []),
        ("Member", "Member",
        [
            "notifications.notification.read", "notifications.preference.manage"
        ]),
        ("Guest", "Guest", [])
    ];

    public static async Task SeedDevelopmentRolesAsync(
        AuthorizationDbContext db,
        IConfiguration config,
        CancellationToken ct = default)
    {
        foreach (var (code, displayName, permissions) in RoleCatalog)
        {
            if (await db.Roles.AnyAsync(x => x.Code == code, ct))
                continue;

            db.Roles.Add(Role.Create(code, displayName, null, permissions, isSystem: true));
        }

        await db.SaveChangesAsync(ct);

        await SeedBootstrapGlobalAdministratorAsync(db, config, ct);
    }

    private static async Task SeedBootstrapGlobalAdministratorAsync(
        AuthorizationDbContext db,
        IConfiguration config,
        CancellationToken ct)
    {
        var bootstrap = config["Authorization:BootstrapGlobalAdminSubjectId"];
        if (!Guid.TryParse(bootstrap, out var subjectId))
            return;

        var role = await db.Roles.FirstOrDefaultAsync(x => x.Code == "GlobalAdministrator", ct);
        if (role is null)
            return;

        var alreadyAssigned = await db.RoleAssignments.AnyAsync(
            x => x.SubjectId == subjectId && x.RoleId == role.Id && !x.IsRevoked, ct);
        if (alreadyAssigned)
            return;

        var now = DateTime.UtcNow;
        db.RoleAssignments.Add(RoleAssignment.Create(
            subjectId,
            role.Id,
            role.Code,
            AuthorizationScope.Global(),
            grantedBy: subjectId,
            grantedAt: now,
            effectiveFrom: null,
            effectiveUntil: null,
            reason: "Bootstrap global administrator (see Authorization:BootstrapGlobalAdminSubjectId)."));

        await db.SaveChangesAsync(ct);
    }
}
