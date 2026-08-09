using CommunityOS.Identity.Application.Interfaces;
using CommunityOS.Identity.Domain.Aggregates;
using CommunityOS.Identity.Domain.Entities;
using CommunityOS.Identity.Domain.Enumerations;
using CommunityOS.Identity.Domain.Exceptions;
using CommunityOS.Identity.Domain.Repositories;
using CommunityOS.Identity.Domain.ValueObjects;

namespace CommunityOS.Identity.Application.Authentication;

public sealed record AuthenticateRequest(
    string Email,
    string Password,
    string? MfaCode,
    string? IpAddress,
    string? UserAgent);

public abstract record AuthenticationOutcome;

public sealed record Authenticated(UserAccount Account) : AuthenticationOutcome;

public sealed record MfaRequired(Guid UserAccountId, string Email) : AuthenticationOutcome;

/// <summary>
/// Validates interactive credentials (password, optional TOTP MFA) and the
/// account's security state. Shared by the first-party login flow and the
/// OAuth authorization-code flow so both enforce identical rules.
/// </summary>
public interface IAccountAuthenticator
{
    Task<AuthenticationOutcome> AuthenticateAsync(AuthenticateRequest request, CancellationToken ct);
}

internal sealed class AccountAuthenticator(
    IUserAccountRepository userAccounts,
    IPasswordHasher passwordHasher,
    ITotpService totpService,
    ISecurityEventRepository securityEvents) : IAccountAuthenticator
{
    public async Task<AuthenticationOutcome> AuthenticateAsync(AuthenticateRequest request, CancellationToken ct)
    {
        var email = Email.Create(request.Email);
        var account = await userAccounts.GetByEmailAsync(email, ct);

        if (account is null)
        {
            // Hash a throwaway value to keep response timing uniform.
            passwordHasher.Verify(request.Password, passwordHasher.Hash("invalid-placeholder"));
            throw new InvalidCredentialsException();
        }

        if (account.IsLocked && account.LockedUntil is { } lockedUntil)
            throw new AccountLockedException(account.Id, lockedUntil);

        var storedHash = account.GetPasswordHash();
        if (storedHash is null || !passwordHasher.Verify(request.Password, storedHash))
        {
            account.RecordFailedLogin();
            await userAccounts.UpdateAsync(account, ct);
            await securityEvents.AddAsync(SecurityEvent.Create(
                account.Id, "Login.Failed", null, request.IpAddress, request.UserAgent), ct);
            throw new InvalidCredentialsException();
        }

        // MFA challenge: verified method active + no valid code supplied.
        if (account.HasVerifiedMfa && !string.IsNullOrWhiteSpace(request.MfaCode))
        {
            var verified = account.MfaMethods
                .Where(m => m.IsVerified && m.IsActive)
                .Any(m => totpService.Verify(m.Secret, request.MfaCode!));

            if (!verified)
            {
                await securityEvents.AddAsync(SecurityEvent.Create(
                    account.Id, "Mfa.Failed", null, request.IpAddress, request.UserAgent), ct);
                throw new InvalidMfaCodeException();
            }
        }
        else if (account.HasVerifiedMfa)
        {
            await securityEvents.AddAsync(SecurityEvent.Create(
                account.Id, "Mfa.Required", null, request.IpAddress, request.UserAgent), ct);
            return new MfaRequired(account.Id, email.Value);
        }

        if (account.Status == AccountStatus.PendingVerification)
            throw new AccountNotVerifiedException(account.Id);

        if (account.Status == AccountStatus.Deactivated)
            throw new AccountDeactivatedException(account.Id);

        return new Authenticated(account);
    }
}
