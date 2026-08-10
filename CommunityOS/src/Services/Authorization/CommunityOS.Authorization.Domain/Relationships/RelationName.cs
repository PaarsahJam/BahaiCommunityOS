using System.Text.RegularExpressions;

namespace CommunityOS.Authorization.Domain.Relationships;

/// <summary>
/// Validates relationship tuple names used for relationship-based
/// authorization, e.g. <c>belongs_to</c>, <c>serves_on</c>, <c>owns</c>,
/// <c>has_permission</c>, <c>assigned_to</c>.
/// </summary>
public static partial class RelationName
{
    public const int MaxLength = 100;

    [GeneratedRegex(@"^[a-z][a-z0-9_]*$", RegexOptions.CultureInvariant)]
    private static partial Regex ConventionRegex();

    public static bool IsValid(string? relation) =>
        !string.IsNullOrWhiteSpace(relation) &&
        relation.Length <= MaxLength &&
        ConventionRegex().IsMatch(relation);
}
