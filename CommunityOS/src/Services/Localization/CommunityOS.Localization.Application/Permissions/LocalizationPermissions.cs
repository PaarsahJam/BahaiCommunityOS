namespace CommunityOS.Localization.Application.Permissions;

/// <summary>
/// Localization capabilities (localization.*, ratified ADR-029 decision 11).
/// Exactly these five strings exist — no sixth permission was ratified.
/// Exact ordinal membership only; administration never implies read:
/// <c>localization.locale.manage</c> does not carry
/// <c>localization.locale.read</c>, and review does not carry propose.
/// </summary>
public static class LocalizationPermissions
{
    /// <summary>Resource type used in authorization contexts for catalog objects.</summary>
    public const string ResourceType = "localization.catalog";

    public const string LocaleRead = "localization.locale.read";
    public const string LocaleManage = "localization.locale.manage";
    public const string ResourceRead = "localization.resource.read";
    public const string ResourcePropose = "localization.resource.propose";
    public const string ResourceReview = "localization.resource.review";

    /// <summary>All five ratified capabilities.</summary>
    public static readonly IReadOnlyList<string> All =
    [
        LocaleRead, LocaleManage, ResourceRead, ResourcePropose, ResourceReview
    ];
}
