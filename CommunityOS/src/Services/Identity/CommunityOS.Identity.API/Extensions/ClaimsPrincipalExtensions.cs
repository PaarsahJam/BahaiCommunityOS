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
}
