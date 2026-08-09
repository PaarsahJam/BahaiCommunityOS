using CommunityOS.SharedKernel.Domain.Primitives;

namespace CommunityOS.Identity.Domain.Enumerations;

/// <summary>
/// Kind of OAuth 2.1 client. Public clients (native/mobile apps) cannot
/// hold a client secret and therefore MUST use PKCE; confidential clients
/// (server-side services) can additionally authenticate with a secret.
/// </summary>
public sealed class OAuthClientType : Enumeration<int>
{
    public static readonly OAuthClientType Public = new(1, "Public");
    public static readonly OAuthClientType Confidential = new(2, "Confidential");

    private OAuthClientType(int id, string name) : base(id, name) { }

    public static IEnumerable<OAuthClientType> All => [Public, Confidential];

    public static OAuthClientType FromId(int id) =>
        All.FirstOrDefault(t => t.Id == id)
        ?? throw new ArgumentException($"Unknown OAuthClientType id: {id}");
}
