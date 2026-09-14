using CommunityOS.Identity.Application.Authentication;
using CommunityOS.Identity.Application.Crypto;
using CommunityOS.Identity.Application.DTOs;
using CommunityOS.Identity.Application.Interfaces;
using CommunityOS.Identity.Application.Logging;
using CommunityOS.Identity.Domain.Aggregates;
using CommunityOS.Identity.Domain.Entities;
using CommunityOS.Identity.Domain.Repositories;
using MediatR;
using Microsoft.Extensions.Logging;

namespace CommunityOS.Identity.Application.Commands;

public sealed record LoginCommand(
    string Email,
    string Password,
    string? MfaCode = null,
    string? DeviceName = null,
    string? Platform = null,
    string? UserAgent = null,
    string? IpAddress = null) : IRequest<LoginResponseDto>;

internal sealed class LoginCommandHandler(
    IUserAccountRepository userAccounts,
    ISessionRepository sessions,
    ISecurityEventRepository securityEvents,
    IAccountAuthenticator authenticator,
    ITokenService tokenService,
    ILogger<LoginCommandHandler> logger) : IRequestHandler<LoginCommand, LoginResponseDto>
{
    private static readonly TimeSpan RefreshTokenLifetime = TimeSpan.FromDays(30);

    public async Task<LoginResponseDto> Handle(LoginCommand cmd, CancellationToken ct)
    {
        var outcome = await authenticator.AuthenticateAsync(
            new AuthenticateRequest(cmd.Email, cmd.Password, cmd.MfaCode, cmd.IpAddress, cmd.UserAgent), ct);

        if (outcome is MfaRequired mfa)
            return new LoginResponseDto(mfa.UserAccountId, mfa.Email, RequiresMfa: true, Tokens: null);

        var account = ((Authenticated)outcome).Account;

        var tokens = await IssueTokensAsync(account, cmd, ct);

        account.RecordSuccessfulLogin();
        await userAccounts.UpdateAsync(account, ct);

        await securityEvents.AddAsync(SecurityEvent.Create(
            account.Id, "Login.Succeeded", null, cmd.IpAddress, cmd.UserAgent), ct);

        logger.SignInSucceeded(account.Id);
        return new LoginResponseDto(account.Id, account.Email.Value, RequiresMfa: false, tokens);
    }

    private async Task<TokenDto> IssueTokensAsync(
        UserAccount account, LoginCommand cmd, CancellationToken ct)
    {
        var device = account.Devices.FirstOrDefault(d => d.Name == cmd.DeviceName && d.IsTrusted)
            ?? account.RegisterDevice(
                string.IsNullOrWhiteSpace(cmd.DeviceName)
                    ? "Default device"
                    : cmd.DeviceName,
                cmd.Platform,
                cmd.UserAgent);
        await userAccounts.UpdateAsync(account, ct);

        var refreshToken = tokenService.GenerateRefreshToken();
        var session = Session.Create(
            account.Id, device.Id, TokenHasher.Hash(refreshToken), RefreshTokenLifetime);
        await sessions.AddAsync(session, ct);

        var accessToken = tokenService.GenerateAccessToken(
            account.Id, session.TokenFamilyId, account.Email.Value);
        return new TokenDto(accessToken, refreshToken, DateTime.UtcNow.AddMinutes(15));
    }
}
