namespace CommunityOS.AI.Application.Permissions;

/// <summary>
/// AI Platform capabilities (ai.*, ratified ADR-030 decision 10).
/// Exactly these two strings exist — no third permission was ratified.
/// Exact ordinal membership only; manage does NOT imply invoke.
/// </summary>
public static class AiPermissions
{
    /// <summary>Invoke AI assistance. Granted to all authenticated human roles except Volunteer/Guest.</summary>
    public const string AssistInvoke = "ai.assist.invoke";

    /// <summary>Administer AI platform providers/models/templates/usage. Granted to Global/National administrators only.</summary>
    public const string PlatformManage = "ai.platform.manage";

    /// <summary>All ratified AI permissions.</summary>
    public static readonly IReadOnlyList<string> All = [AssistInvoke, PlatformManage];
}
