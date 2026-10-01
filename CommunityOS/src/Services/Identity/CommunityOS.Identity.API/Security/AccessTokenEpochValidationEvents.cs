using CommunityOS.Identity.Domain.Repositories;
using CommunityOS.Identity.Infrastructure.Security;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.DependencyInjection;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace CommunityOS.Identity.API.Security;

/// <summary>
/// ADR-036 D1/Q1: account-epoch validation applied by every Identity JWT
/// validation seam (the single <c>JwtBearer</c> handler) after signature,
/// issuer, audience and lifetime validation succeed. Resolves the locally known
/// account from the module's own repository and rejects a token whose
/// <c>sre</c> is older than <c>UserAccount.SessionRevocationEpoch</c>.
/// </summary>
internal static class AccessTokenEpochValidationEvents
{
    internal static Task OnTokenValidatedAsync(TokenValidatedContext context)
    {
        var repository = context.HttpContext.RequestServices
            .GetRequiredService<IUserAccountRepository>();
        return ApplyAsync(context, repository);
    }

    internal static async Task ApplyAsync(
        TokenValidatedContext context, IUserAccountRepository userAccounts)
    {
        var subject = context.Principal?.FindFirst(JwtRegisteredClaimNames.Sub)
            ?? context.Principal?.FindFirst(ClaimTypes.NameIdentifier);
        if (subject is null || !Guid.TryParse(subject.Value, out var accountId))
        {
            context.Fail("Missing or malformed user subject.");
            return;
        }

        var account = await userAccounts.GetByIdAsync(accountId, context.HttpContext.RequestAborted);
        if (account is null)
        {
            context.Fail("Unknown user account.");
            return;
        }

        if (!SessionRevocationEpochValidator.TryReadTokenEpoch(context.Principal, out var tokenEpoch))
        {
            context.Fail("Malformed session-revocation-epoch claim.");
            return;
        }

        if (SessionRevocationEpochValidator.Evaluate(tokenEpoch, account.SessionRevocationEpoch)
            == TokenEpochValidity.Revoked)
        {
            context.Fail("Access token session-revocation epoch is no longer valid.");
        }
    }
}