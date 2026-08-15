using System.Security.Claims;
using System.IdentityModel.Tokens.Jwt;

namespace CommunityOS.Documents.API.Extensions;

internal static class ClaimsPrincipalExtensions
{
    /// <summary>
    /// Reads the authenticated subject id from the token's subject claim.
    /// </summary>
    internal static Guid GetSubjectId(this ClaimsPrincipal principal)
    {
        var value = principal.FindFirstValue(JwtRegisteredClaimNames.Sub)
            ?? principal.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new UnauthorizedAccessException("No user subject claim present.");

        return Guid.Parse(value);
    }
}