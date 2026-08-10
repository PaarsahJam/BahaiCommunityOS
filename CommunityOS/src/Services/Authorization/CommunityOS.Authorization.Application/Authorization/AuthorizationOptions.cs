namespace CommunityOS.Authorization.Application.Authorization;

/// <summary>
/// Operational limits for the Authorization service. These are configuration,
/// not business authorization decisions.
/// </summary>
public sealed class AuthorizationOptions
{
    public const string SectionName = "Authorization";

    public int MaxBreakGlassDurationMinutes { get; set; } = 60;

    public int MaxBreakGlassPermissions { get; set; } = 10;

    public int MaxDelegationDurationDays { get; set; } = 30;

    public int MaxDelegatedPermissions { get; set; } = 25;
}
