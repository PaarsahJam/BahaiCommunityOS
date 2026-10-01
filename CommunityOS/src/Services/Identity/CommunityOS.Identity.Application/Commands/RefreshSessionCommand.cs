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
    IUnitOfWork unitOfWork,
    ILogger<RefreshSessionCommandHandler> logger) : IRequestHandler<RefreshSessionCommand, TokenDto>
{
    private static readonly TimeSpan RefreshTokenLifetime = TimeSpan.FromDays(30);

    public async Task<TokenDto> Handle(RefreshSessionCommand cmd, CancellationToken ct)
    {
        var tokenHash = TokenHasher.Hash(cmd.RefreshToken);

        // ADR-036 D3: one explicit transaction as the single persistence
        // boundary for the whole refresh decision — account lock, post-lock
        // re-reads, epoch decision, reuse detection, rotation and reuse-family
        // revocation all commit together (or roll back together).
        await unitOfWork.BeginTransactionAsync(ct);
        try
        {
            var session = await sessions.GetByRefreshTokenHashAsync(tokenHash, ct)
                ?? throw new InvalidRefreshTokenException();

            // Account-row lock (identity.user_accounts ... FOR UPDATE) — the
            // serialization point of every refresh and emergency invalidation
            // for this account. The epoch read below is the ONLY authoritative
            // value for the accept/reject decision.
            var account = await userAccounts.GetByIdForUpdateAsync(session.UserAccountId, ct)
                ?? throw new UserAccountNotFoundException(session.UserAccountId);

            // Post-lock re-read: if another refresh or an emergency
            // invalidation committed while we were waiting for the lock, its
            // effects (reuse flag, revocation, family state) must be observed
            // here rather than from the tracked pre-lock snapshot.
            await sessions.ReloadAsync(session, ct);

            // A rotated session used again signals token theft: revoke every
            // session in the family. Persisted inside this transaction before
            // the request fails.
            if (session.RefreshTokenUsed)
            {
                await RevokeFamilyAsync(session, ct);
                await securityEvents.AddAsync(SecurityEvent.Create(
                    session.UserAccountId, "RefreshToken.ReuseDetected", null, cmd.IpAddress, null), ct);
                logger.RefreshTokenReuseDetected(session.UserAccountId);
                await unitOfWork.CommitAsync(ct);
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

            if (account.Status == Domain.Enumerations.AccountStatus.Deactivated)
                throw new AccountDeactivatedException(account.Id);

            // ADR-036 D3: the refresh family is stale if it is bound to an
            // epoch older than the account's POST-lock epoch — i.e. an
            // emergency invalidation committed while this refresh waited for
            // the lock. The pre-lock epoch is never used for this decision.
            if (session.IsStaleRelativeTo(account.SessionRevocationEpoch))
                throw new InvalidRefreshTokenException();

            var newRefreshToken = tokenService.GenerateRefreshToken();
            var rotated = session.Rotate(
                TokenHasher.Hash(newRefreshToken), RefreshTokenLifetime, account.SessionRevocationEpoch);

            await sessions.UpdateAsync(session, ct);
            await sessions.AddAsync(rotated, ct);

            // Single transactional persistence boundary: final flush + commit.
            await unitOfWork.CommitAsync(ct);

            var accessToken = tokenService.GenerateAccessToken(
                account.Id, rotated.TokenFamilyId, account.Email.Value, account.SessionRevocationEpoch);
            return new TokenDto(accessToken, newRefreshToken, DateTime.UtcNow.AddMinutes(15));
        }
        catch
        {
            try
            {
                await unitOfWork.RollbackAsync(ct);
            }
            catch
            {
                // Best-effort rollback; never mask the operation error.
            }

            throw;
        }
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