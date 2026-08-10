using CommunityOS.Authorization.Domain.Events;
using CommunityOS.Authorization.Domain.Exceptions;
using CommunityOS.Authorization.Domain.Permissions;
using CommunityOS.SharedKernel.Domain.Primitives;
using CommunityOS.SharedKernel.Guards;

namespace CommunityOS.Authorization.Domain.Aggregates;

/// <summary>
/// A configurable collection of permissions grouped under a stable code.
/// Roles are the RBAC building block of CommunityOS; they are deployment
/// configurable and business decisions must never be hard-coded around a
/// specific role name.
/// </summary>
public sealed class Role : AggregateRoot<Guid>
{
    private readonly List<string> _permissions = [];

    public string Code { get; private set; }
    public string DisplayName { get; private set; }
    public string? Description { get; private set; }
    public bool Enabled { get; private set; }
    public bool IsSystem { get; private set; }
    public DateTime CreatedOn { get; private set; }

    public IReadOnlyList<string> Permissions => _permissions.AsReadOnly();

    private Role(Guid id, string code, string displayName, string? description, bool isSystem) : base(id)
    {
        Code = code;
        DisplayName = displayName;
        Description = description;
        Enabled = true;
        IsSystem = isSystem;
        CreatedOn = DateTime.UtcNow;
    }

    public static Role Create(
        string code,
        string displayName,
        string? description,
        IReadOnlyList<string> permissions,
        bool isSystem = false)
    {
        Guard.NotNullOrWhiteSpace(code, nameof(code));
        Guard.NotNullOrWhiteSpace(displayName, nameof(displayName));
        Guard.MaxLength(code, 100, nameof(code));
        Guard.MaxLength(displayName, 200, nameof(displayName));
        if (description is not null) Guard.MaxLength(description, 1000, nameof(description));
        Guard.NotNull(permissions, nameof(permissions));
        ValidatePermissions(permissions);

        var role = new Role(Guid.NewGuid(), code.Trim(), displayName.Trim(), description?.Trim(), isSystem);
        role._permissions.AddRange(
            permissions.Select(PermissionName.Normalize).Distinct(StringComparer.Ordinal));

        role.RaiseDomainEvent(new RoleCreatedEvent(role.Id, role.Code));
        return role;
    }

    public void UpdateDetails(string displayName, string? description)
    {
        Guard.NotNullOrWhiteSpace(displayName, nameof(displayName));
        Guard.MaxLength(displayName, 200, nameof(displayName));
        if (description is not null) Guard.MaxLength(description, 1000, nameof(description));
        DisplayName = displayName.Trim();
        Description = description?.Trim();
    }

    public void UpdatePermissions(IReadOnlyList<string> permissions)
    {
        Guard.NotNull(permissions, nameof(permissions));
        ValidatePermissions(permissions);
        _permissions.Clear();
        _permissions.AddRange(
            permissions.Select(PermissionName.Normalize).Distinct(StringComparer.Ordinal));
        RaiseDomainEvent(new RolePermissionsChangedEvent(Id, Code));
    }

    public void Enable() => Enabled = true;

    public void Disable() => Enabled = false;

    public bool HasPermission(string permission) =>
        _permissions.Contains(permission, StringComparer.Ordinal);

    private static void ValidatePermissions(IReadOnlyList<string> permissions)
    {
        var invalid = permissions.FirstOrDefault(p => !PermissionName.IsValid(p));
        if (invalid is not null)
            throw new InvalidPermissionException(invalid);
    }
}
