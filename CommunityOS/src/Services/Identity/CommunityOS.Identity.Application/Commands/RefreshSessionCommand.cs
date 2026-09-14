using CommunityOS.Identity.Application.Crypto;
using CommunityOS.Identity.Application.DTOs;
using CommunityOS.Identity.Application.Interfaces;
using CommunityOS.Identity.Application.Logging;
using CommunityOS.Identity.Domain.Entities;
using CommunityOS.Identity.Domain.Exceptions;
using CommunityOS.Identity.Domain.Repositories;
using MediatR;
using Microsoft.Extensions.Logging;

namespace CommunityOS.Identity.Application.Commands;

public sealed record RefreshSessionCommand(
    string RefreshToken,
    string? IpAddress = null,
    string? ClientId = null) : IRequest<TokenDto>;

internal sealed class RefreshSessionCommandHandler(
    ISessionRepository sessions,
    IUserAccountRepository userAccounts,
    ITokenService tokenService,
    ISecurityEventRepository securityEvents,
    ILogger<RefreshSessionCommandHandler> logger) : IRequestHandler<RefreshSessionCommand, TokenDto>
{
    private static readonly TimeSpan RefreshTokenLifetime = TimeSpan.FromDays(30);

    public async Task<TokenDto> Handle(RefreshSessionCommand cmd, CancellationToken ct)
    {
        var tokenHash = TokenHasher.Hash(cmd.RefreshToken);
        var session = await sessions.GetByRefreshTokenHashAsync(tokenHash, ct)
            ?? throw new InvalidRefreshTokenException();

        // A rotated session that is used again signals token theft:
        // revoke every session in the family.
        if (session.RefreshTokenUsed)
        {
            await RevokeFamilyAsync(session, ct);
            await securityEvents.AddAsync(SecurityEvent.Create(
                session.UserAccountId, "RefreshToken.ReuseDetected", null, cmd.IpAddress, null), ct);
            logger.RefreshTokenReuseDetected(session.UserAccountId);
            throw new RefreshTokenReuseDetectedException();
        }

        // Sessions issued via the OAuth token endpoint are bound to a client;
        // they may only be rotated by that same client.
        if (session.ClientId is not null &&
            (cmd.ClientId is null ||
             !string.Equals(session.ClientId, cmd.ClientId, StringComparison.Ordinal)))
            throw new InvalidGrantException();

        if (!session.IsActive)
            throw new InvalidRefreshTokenException();

        var account = await userAccounts.GetByIdAsync(session.UserAccountId, ct)
            ?? throw new UserAccountNotFoundException(session.UserAccountId);

        if (account.Status == Domain.Enumerations.AccountStatus.Deactivated)
            throw new AccountDeactivatedException(account.Id);

        var newRefreshToken = tokenService.GenerateRefreshToken();
        var rotated = session.Rotate(TokenHasher.Hash(newRefreshToken), RefreshTokenLifetime);

        await sessions.UpdateAsync(session, ct);
        await sessions.AddAsync(rotated, ct);

        var accessToken = tokenService.GenerateAccessToken(
            account.Id, rotated.TokenFamilyId, account.Email.Value);
        return new TokenDto(accessToken, newRefreshToken, DateTime.UtcNow.AddMinutes(15));
    }

    private async Task RevokeFamilyAsync(Domain.Aggregates.Session session, CancellationToken ct)
    {
        var family = await sessions.GetByFamilyAsync(session.TokenFamilyId, ct);
        foreach (var s in family.Where(s => !s.IsRevoked))
        {
            s.Revoke("Refresh token reuse detected.");
            await sessions.UpdateAsync(s, ct);
        }
    }
}
