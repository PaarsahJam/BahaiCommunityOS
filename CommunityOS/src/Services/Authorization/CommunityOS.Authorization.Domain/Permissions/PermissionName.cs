using System.Text.RegularExpressions;

namespace CommunityOS.Authorization.Domain.Permissions;

/// <summary>
/// Validates the CommunityOS permission naming convention. Permissions name a
/// meaningful protected capability (<c>domain.entity.action</c>), never a UI
/// affordance. Examples: <c>records.record.read</c>, <c>authz.role.assign</c>.
/// </summary>
public static partial class PermissionName
{
    public const int MaxLength = 128;

    [GeneratedRegex(@"^[a-z][a-z0-9]*(\.[a-z][a-z0-9]*)+$", RegexOptions.CultureInvariant)]
    private static partial Regex ConventionRegex();

    public static bool IsValid(string? permission) =>
        !string.IsNullOrWhiteSpace(permission) &&
        permission.Length <= MaxLength &&
        ConventionRegex().IsMatch(permission);

    public static string Normalize(string permission)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(permission);
        return permission.Trim();
    }
}
