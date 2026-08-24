using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace CommunityOS.Localization.API;

internal static class ClaimsPrincipalExtensions
{
    public static Guid SubjectId(this ClaimsPrincipal principal)
    {
        var value = principal.FindFirstValue(JwtRegisteredClaimNames.Sub)
            ?? principal.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new UnauthorizedAccessException("No user subject claim present.");
        return Guid.Parse(value);
    }
}
