using CommunityOS.SharedKernel.Domain.Primitives;
using CommunityOS.SharedKernel.Guards;

namespace CommunityOS.Identity.Domain.Entities;

public sealed class Role : Entity<Guid>
{
    private readonly List<Permission> _permissions = [];

    public string Name { get; private set; }
    public string? Description { get; private set; }
    public bool IsSystemRole { get; private set; }

    public IReadOnlyList<Permission> Permissions => _permissions.AsReadOnly();

    private Role(Guid id, string name, string? description, bool isSystemRole) : base(id)
    {
        Name = name;
        Description = description;
        IsSystemRole = isSystemRole;
    }

    public static Role Create(string name, string? description = null, bool isSystemRole = false)
    {
        Guard.NotNullOrWhiteSpace(name, nameof(name));
        Guard.MaxLength(name, 100, nameof(name));
        return new Role(Guid.NewGuid(), name.Trim(), description?.Trim(), isSystemRole);
    }

    public void AddPermission(Permission permission)
    {
        Guard.NotNull(permission, nameof(permission));
        if (!_permissions.Contains(permission))
            _permissions.Add(permission);
    }

    public void RemovePermission(Permission permission) =>
        _permissions.Remove(permission);
}
