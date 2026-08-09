using CommunityOS.Identity.Domain.Enumerations;
using CommunityOS.SharedKernel.Domain.Primitives;
using CommunityOS.SharedKernel.Guards;

namespace CommunityOS.Identity.Domain.Aggregates;

/// <summary>
/// A registered OAuth 2.1 / OIDC client that may request tokens from the
/// token endpoint. Public clients (native apps) rely on PKCE instead of a
/// shared secret; confidential clients may also be issued a secret hash.
/// </summary>
public sealed class OAuthClient : AggregateRoot<Guid>
{
    private readonly List<string> _redirectUris = [];
    private readonly List<string> _grantTypes = [];
    private readonly List<string> _allowedScopes = [];

    public string ClientId { get; private set; }
    public string DisplayName { get; private set; }
    public OAuthClientType Type { get; private set; }
    public bool Enabled { get; private set; }
    public DateTime CreatedOn { get; private set; }
    public string? ClientSecretHash { get; private set; }

    public IReadOnlyList<string> RedirectUris => _redirectUris.AsReadOnly();
    public IReadOnlyList<string> AllowedGrantTypes => _grantTypes.AsReadOnly();
    public IReadOnlyList<string> AllowedScopes => _allowedScopes.AsReadOnly();

    private OAuthClient(Guid id, string clientId, string displayName, OAuthClientType type) : base(id)
    {
        ClientId = clientId;
        DisplayName = displayName;
        Type = type;
        Enabled = true;
        CreatedOn = DateTime.UtcNow;
    }

    public static OAuthClient Create(
        string clientId,
        string displayName,
        OAuthClientType type,
        IReadOnlyList<string> redirectUris,
        IReadOnlyList<string> grantTypes,
        IReadOnlyList<string> allowedScopes)
    {
        Guard.NotNullOrWhiteSpace(clientId, nameof(clientId));
        Guard.NotNullOrWhiteSpace(displayName, nameof(displayName));
        Guard.NotNull(type, nameof(type));
        Guard.NotNull(redirectUris, nameof(redirectUris));
        Guard.NotNull(grantTypes, nameof(grantTypes));
        Guard.NotNull(allowedScopes, nameof(allowedScopes));

        var client = new OAuthClient(Guid.NewGuid(), clientId.Trim(), displayName.Trim(), type);

        client._redirectUris.AddRange(
            redirectUris.Where(u => !string.IsNullOrWhiteSpace(u)).Select(u => u.Trim()));
        client._grantTypes.AddRange(
            grantTypes.Where(g => !string.IsNullOrWhiteSpace(g)).Select(g => g.Trim()));
        client._allowedScopes.AddRange(
            allowedScopes.Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => s.Trim()));

        if (client._redirectUris.Count == 0)
            throw new ArgumentException("At least one redirect URI is required.", nameof(redirectUris));
        if (client._grantTypes.Count == 0)
            throw new ArgumentException("At least one grant type is required.", nameof(grantTypes));
        if (client._allowedScopes.Count == 0)
            throw new ArgumentException("At least one scope is required.", nameof(allowedScopes));

        return client;
    }

    public void SetSecret(string clientSecretHash)
    {
        Guard.NotNullOrWhiteSpace(clientSecretHash, nameof(clientSecretHash));
        if (Type != OAuthClientType.Confidential)
            throw new InvalidOperationException("Only confidential clients can hold a client secret.");
        ClientSecretHash = clientSecretHash;
    }

    public bool IsRedirectUriAllowed(string redirectUri) =>
        !string.IsNullOrWhiteSpace(redirectUri) &&
        _redirectUris.Contains(redirectUri, StringComparer.Ordinal);

    public bool IsGrantTypeAllowed(string grantType) =>
        !string.IsNullOrWhiteSpace(grantType) &&
        _grantTypes.Contains(grantType, StringComparer.Ordinal);

    public bool IsScopeAllowed(string requestedScope)
    {
        if (string.IsNullOrWhiteSpace(requestedScope))
            return false;

        var requested = requestedScope.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return requested.Length != 0 &&
               requested.All(s => _allowedScopes.Contains(s, StringComparer.Ordinal));
    }

    public void Enable()
    {
        Enabled = true;
    }

    public void Disable()
    {
        Enabled = false;
    }
}
