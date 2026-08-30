using CommunityOS.Authorization.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CommunityOS.Authorization.Tests.Persistence;

/// <summary>
/// Asserts the MassTransit transactional outbox contract in the Authorization EF
/// Core model, built offline from the configuration assemblies (ADR-015 at the
/// Authorization outbox gate). No Docker or live database is required: the
/// relational model is produced solely from
/// <see cref="AuthorizationDbContext.OnModelCreating"/>.
/// </summary>
public sealed class AuthorizationOutboxModelTests
{
    private static AuthorizationDbContext CreateOfflineContext()
    {
        var options = new DbContextOptionsBuilder<AuthorizationDbContext>()
            .UseNpgsql(
                "Host=localhost;Port=5432;Database=communityos_authorization;Username=communityos;Password=communityos",
                npgsql => npgsql.MigrationsAssembly("CommunityOS.Authorization.Infrastructure"))
            .Options;

        return new AuthorizationDbContext(options);
    }

    [Fact]
    public void Context_uses_the_authorization_schema_and_owns_the_mass_transit_outbox()
    {
        using var db = CreateOfflineContext();

        db.Model.GetRelationalModel();

        db.Model.GetDefaultSchema().Should().Be("authorization");

        var outbox = db.Model.GetEntityTypes()
            .Where(e => e.ClrType.FullName?.StartsWith("MassTransit.", StringComparison.Ordinal) == true)
            .ToList();
        outbox.Should().NotBeEmpty();
        outbox.Should().OnlyContain(e => e.GetSchema() == "authorization");
        outbox.Select(e => e.GetTableName()).Should().Contain(
        [
            "InboxState",
            "OutboxState",
            "OutboxMessage"
        ]);
    }
}