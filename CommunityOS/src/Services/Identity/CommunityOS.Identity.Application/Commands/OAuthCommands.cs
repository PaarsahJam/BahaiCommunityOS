using CommunityOS.Identity.Application.Authentication;
using CommunityOS.Identity.Application.Crypto;
using CommunityOS.Identity.Application.DTOs;
using CommunityOS.Identity.Application.Interfaces;
using CommunityOS.Identity.Domain.Aggregates;
using CommunityOS.Identity.Domain.Entities;
using CommunityOS.Identity.Domain.Enumerations;
using CommunityOS.Identity.Domain.Exceptions;
using CommunityOS.Identity.Domain.Repositories;
using MediatR;

namespace CommunityOS.Identity.Application.Commands;

/// <summary>
/// Interactive OAuth 2.1 authorization request for a native (public) client.
/// Authenticates the user, validates the client/PKCE parameters, and issues a
/// single-use authorization code bound to the supplied code challenge. The
/// caller then exchanges the code (proving possession of the verifier) at the
/// token endpoint.
/// </summary>
public sealed record CreateAuthorizationCodeCommand(
    string Email,
    string Password,
    string ClientId,
    string RedirectUri,
    string CodeChallenge,
    string CodeChallengeMethod,
    string Scope,
    string? MfaCode = null,
    string? State = null,
    string? Nonce = null,
    string? IpAddress = null,
    string? UserAgent = null) : IRequest<AuthorizationCodeDto>;

internal sealed class CreateAuthorizationCodeCommandHandler(
    IAccountAuthenticator authenticator,
    IOAuthClientRepository oauthClients,
    IAuthorizationCodeRepository authorizationCodes,
    IUserAccountRepository userAccounts,
    ISecurityEventRepository securityEvents,
    IMediator mediator) : IRequestHandler<CreateAuthorizationCodeCommand, AuthorizationCodeDto>
{
    private static readonly TimeSpan AuthorizationCodeLifetime = TimeSpan.FromMinutes(10);

    public async Task<AuthorizationCodeDto> Handle(CreateAuthorizationCodeCommand cmd, CancellationToken ct)
    {
        var client = await oauthClients.GetByClientIdAsync(cmd.ClientId, ct)
            ?? throw new InvalidClientException();

        if (!client.Enabled)
            throw new InvalidClientException();

        if (!client.IsGrantTypeAllowed("authorization_code"))
            throw new UnauthorizedGrantException(client.ClientId, "authorization_code");

        if (!client.IsRedirectUriAllowed(cmd.RedirectUri))
            throw new InvalidRedirectUriException(cmd.RedirectUri);

        if (!client.IsScopeAllowed(cmd.Scope))
            throw new InvalidScopeException(cmd.Scope);

        var outcome = await authenticator.AuthenticateAsync(
            new AuthenticateRequest(cmd.Email, cmd.Password, cmd.MfaCode, cmd.IpAddress, cmd.UserAgent), ct);

        if (outcome is MfaRequired)
            throw new MfaRequiredException();

        var account = ((Authenticated)outcome).Account;

        var rawCode = TokenHasher.GenerateToken();
        var authorizationCode = AuthorizationCode.Create(
            account.Id,
            client.Id,
            client.ClientId,
            cmd.RedirectUri,
            cmd.CodeChallenge,
            cmd.CodeChallengeMethod,
            cmd.Scope,
            TokenHasher.Hash(rawCode),
            cmd.Nonce,
            AuthorizationCodeLifetime);

        await authorizationCodes.AddAsync(authorizationCode, ct);

        account.RecordSuccessfulLogin();
        await userAccounts.UpdateAsync(account, ct);

        await securityEvents.AddAsync(SecurityEvent.Create(
            account.Id, "Authorize.CodeIssued", $"client: {client.ClientId}", cmd.IpAddress, cmd.UserAgent), ct);

        foreach (var domainEvent in authorizationCode.DomainEvents)
            await mediator.Publish(domainEvent, ct);
        authorizationCode.ClearDomainEvents();

        return new AuthorizationCodeDto(rawCode, cmd.State, authorizationCode.ExpiresOn);
    }
}

/// <summary>
/// OAuth 2.1 token-endpoint exchange for <c>grant_type=authorization_code</c>.
/// Validates the single-use code, its client/redirect binding, and the PKCE
/// verifier, then issues an access token plus a rotating refresh token.
/// </summary>
public sealed record ExchangeAuthorizationCodeCommand(
    string Code,
    string CodeVerifier,
    string ClientId,
    string RedirectUri) : IRequest<TokenResponseDto>;

internal sealed class ExchangeAuthorizationCodeCommandHandler(
    IAuthorizationCodeRepository authorizationCodes,
    IOAuthClientRepository oauthClients,
    IUserAccountRepository userAccounts,
    ISessionRepository sessions,
    ISecurityEventRepository securityEvents,
    ITokenService tokenService,
    IMediator mediator) : IRequestHandler<ExchangeAuthorizationCodeCommand, TokenResponseDto>
{
    private const int AccessTokenLifetimeSeconds = 900;
    private static readonly TimeSpan RefreshTokenLifetime = TimeSpan.FromDays(30);

    public async Task<TokenResponseDto> Handle(ExchangeAuthorizationCodeCommand cmd, CancellationToken ct)
    {
        var codeHash = TokenHasher.Hash(cmd.Code);
        var code = await authorizationCodes.GetByCodeHashAsync(codeHash, ct)
            ?? throw new InvalidGrantException();

        if (!code.CanBeExchanged)
            throw new InvalidGrantException();

        if (!string.Equals(code.ClientId, cmd.ClientId, StringComparison.Ordinal))
            throw new InvalidGrantException();

        if (!string.Equals(code.RedirectUri, cmd.RedirectUri, StringComparison.Ordinal))
            throw new InvalidGrantException();

        var client = await oauthClients.GetByClientIdAsync(cmd.ClientId, ct)
            ?? throw new InvalidClientException();

        if (!client.Enabled)
            throw new InvalidClientException();

        if (!Pkce.Verify(cmd.CodeVerifier, code.CodeChallenge, code.CodeChallengeMethod))
            throw new InvalidGrantException();

        var account = await userAccounts.GetByIdAsync(code.UserAccountId, ct)
            ?? throw new UserAccountNotFoundException(code.UserAccountId);

        if (account.Status == AccountStatus.Deactivated)
            throw new AccountDeactivatedException(account.Id);

        code.Consume();
        await authorizationCodes.UpdateAsync(code, ct);

        var device = account.RegisterDevice($"OAuth client ({client.ClientId})", "api", null);
        account.RecordSuccessfulLogin();
        await userAccounts.UpdateAsync(account, ct);

        var refreshToken = tokenService.GenerateRefreshToken();
        var session = Session.Create(
            account.Id, device.Id, TokenHasher.Hash(refreshToken), RefreshTokenLifetime, client.ClientId);
        await sessions.AddAsync(session, ct);

        var accessToken = tokenService.GenerateAccessToken(
            account.Id, session.TokenFamilyId, account.Email.Value);
        var wantsOpenId = code.Scope
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Contains("openid", StringComparer.Ordinal);
        var idToken = wantsOpenId
            ? tokenService.GenerateIdToken(
                account.Id, account.Email.Value, account.VerifiedOn is not null, client.ClientId, code.Nonce)
            : null;

        await securityEvents.AddAsync(SecurityEvent.Create(
            account.Id, "Token.Issued", $"client: {client.ClientId}"), ct);

        foreach (var domainEvent in account.DomainEvents)
            await mediator.Publish(domainEvent, ct);
        account.ClearDomainEvents();

        return new TokenResponseDto(
            accessToken, "Bearer", AccessTokenLifetimeSeconds, refreshToken, code.Scope, idToken);
    }
}
