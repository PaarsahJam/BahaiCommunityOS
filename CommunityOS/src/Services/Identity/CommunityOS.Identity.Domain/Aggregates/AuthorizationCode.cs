using CommunityOS.Identity.Domain.Events;
using CommunityOS.SharedKernel.Domain.Primitives;
using CommunityOS.SharedKernel.Guards;

namespace CommunityOS.Identity.Domain.Aggregates;

/// <summary>
/// A single-use, short-lived authorization code issued after interactive
/// authentication. The code is bound to the client, redirect URI and the
/// PKCE code challenge supplied during the authorization request, so the
/// token endpoint can prove possession of the matching verifier. Only the
/// SHA-256 hash of the raw code is ever persisted.
/// </summary>
public sealed class AuthorizationCode : AggregateRoot<Guid>
{
    public const string ChallengeMethodS256 = "S256";
    public const string ChallengeMethodPlain = "plain";

    public string CodeHash { get; private set; }
    public Guid UserAccountId { get; private set; }
    public Guid OAuthClientId { get; private set; }
    public string ClientId { get; private set; }
    public string RedirectUri { get; private set; }
    public string CodeChallenge { get; private set; }
    public string CodeChallengeMethod { get; private set; }
    public string Scope { get; private set; }
    public string? Nonce { get; private set; }
    public DateTime IssuedOn { get; private set; }
    public DateTime ExpiresOn { get; private set; }
    public DateTime? ConsumedOn { get; private set; }

    private AuthorizationCode(
        Guid id,
        Guid userAccountId,
        Guid oAuthClientId,
        string clientId,
        string redirectUri,
        string codeChallenge,
        string codeChallengeMethod,
        string scope,
        string? nonce,
        string codeHash,
        DateTime expiresOn) : base(id)
    {
        UserAccountId = userAccountId;
        OAuthClientId = oAuthClientId;
        ClientId = clientId;
        RedirectUri = redirectUri;
        CodeChallenge = codeChallenge;
        CodeChallengeMethod = codeChallengeMethod;
        Scope = scope;
        Nonce = nonce;
        CodeHash = codeHash;
        IssuedOn = DateTime.UtcNow;
        ExpiresOn = expiresOn;
    }

    public static AuthorizationCode Create(
        Guid userAccountId,
        Guid oauthClientId,
        string clientId,
        string redirectUri,
        string codeChallenge,
        string codeChallengeMethod,
        string scope,
        string codeHash,
        string? nonce,
        TimeSpan lifetime)
    {
        Guard.NotDefault(userAccountId, nameof(userAccountId));
        Guard.NotDefault(oauthClientId, nameof(oauthClientId));
        Guard.NotNullOrWhiteSpace(clientId, nameof(clientId));
        Guard.NotNullOrWhiteSpace(redirectUri, nameof(redirectUri));
        Guard.NotNullOrWhiteSpace(codeChallenge, nameof(codeChallenge));
        Guard.NotNullOrWhiteSpace(codeHash, nameof(codeHash));
        Guard.NotNullOrWhiteSpace(scope, nameof(scope));
        Guard.Positive((int)lifetime.TotalSeconds, nameof(lifetime));

        if (codeChallengeMethod is not (ChallengeMethodS256 or ChallengeMethodPlain))
            throw new ArgumentException(
                "code_challenge_method must be 'S256' or 'plain'.", nameof(codeChallengeMethod));

        var code = new AuthorizationCode(
            Guid.NewGuid(),
            userAccountId,
            oauthClientId,
            clientId.Trim(),
            redirectUri.Trim(),
            codeChallenge,
            codeChallengeMethod,
            scope.Trim(),
            string.IsNullOrWhiteSpace(nonce) ? null : nonce.Trim(),
            codeHash,
            DateTime.UtcNow.Add(lifetime));

        code.RaiseDomainEvent(new AuthorizationCodeIssuedEvent(userAccountId, clientId.Trim()));
        return code;
    }

    public bool IsExpired => DateTime.UtcNow >= ExpiresOn;
    public bool IsConsumed => ConsumedOn is not null;
    public bool CanBeExchanged => !IsExpired && !IsConsumed;

    public void Consume()
    {
        if (!CanBeExchanged)
            throw new InvalidOperationException("The authorization code is expired or has already been consumed.");
        ConsumedOn = DateTime.UtcNow;
    }
}
