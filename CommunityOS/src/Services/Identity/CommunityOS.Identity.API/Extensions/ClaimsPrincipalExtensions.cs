using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace CommunityOS.Identity.API.Extensions;

internal static class ClaimsPrincipalExtensions
{
    /// <summary>
    /// Reads the authenticated user account id from the token's subject claim.
    /// </summary>
    internal static Guid GetUserAccountId(this ClaimsPrincipal principal)
    {
        var value = principal.FindFirstValue(JwtRegisteredClaimNames.Sub)
            ?? principal.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new UnauthorizedAccessException("No user subject claim present.");

        return Guid.Parse(value);
    }

    /// <summary>
    /// Reads the logical-session identifier from the token's signed <c>sid</c>
    /// claim (the issuing session's token-family id). Returns <see langword="null"/>
    /// for absent or malformed claims so callers can only fall back to
    /// "no current session correlation" — never throw.
    /// </summary>
    internal static Guid? GetSessionFamilyId(this ClaimsPrincipal principal)
    {
        var value = principal.FindFirstValue(JwtRegisteredClaimNames.Sid);
        if (value is null) return null;
        return Guid.TryParse(value, out var parsed) ? parsed : null;
    }
}
