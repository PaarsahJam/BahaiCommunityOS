namespace CommunityOS.Authorization.HttpClient.Integration;

/// <summary>
/// Configuration for the HTTP-based <see cref="HttpAuthorizationEvaluator"/>.
/// Each consuming service binds its own instance from the
/// <c>AuthorizationService</c> configuration section and sets its own
/// <see cref="ClientId"/> default (ADR-018/019).
/// </summary>
public sealed class AuthorizationHttpEvaluatorOptions
{
    public const string SectionName = "AuthorizationService";

    /// <summary>Base URL of the Authorization service API.</summary>
    public string BaseUrl { get; set; } = string.Empty;

    /// <summary>
    /// Bearer access token the service presents to the Authorization service.
    /// In development this is a configured platform service token.
    /// </summary>
    public string AccessToken { get; set; } = string.Empty;

    /// <summary>
    /// Client identifier sent as the <c>X-Client-Id</c> header.
    /// Each consuming service sets its own default.
    /// </summary>
    public string ClientId { get; set; } = string.Empty;
}
