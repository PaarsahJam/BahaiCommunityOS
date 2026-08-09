using CommunityOS.Identity.Domain.Aggregates;
using CommunityOS.Identity.Domain.Enumerations;
using Microsoft.EntityFrameworkCore;

namespace CommunityOS.Identity.Infrastructure.Persistence;

/// <summary>
/// Seeds development-only OAuth clients so the authorization-code + PKCE
/// flow can be exercised without provisioning a client through an admin API.
/// </summary>
public static class OAuthClientSeeder
{
    public const string FlutterClientId = "communityos-flutter";

    public static async Task SeedDevelopmentClientsAsync(
        IdentityDbContext db, CancellationToken ct = default)
    {
        if (await db.OAuthClients.AnyAsync(x => x.ClientId == FlutterClientId, ct))
            return;

        db.OAuthClients.Add(OAuthClient.Create(
            FlutterClientId,
            "CommunityOS Flutter App",
            OAuthClientType.Public,
            ["communityos://callback", "http://localhost:8080/callback"],
            ["authorization_code", "refresh_token"],
            ["openid", "profile", "email", "offline_access"]));

        await db.SaveChangesAsync(ct);
    }
}
