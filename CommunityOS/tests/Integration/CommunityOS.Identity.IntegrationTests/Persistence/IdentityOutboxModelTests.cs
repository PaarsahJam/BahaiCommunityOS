using CommunityOS.Identity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CommunityOS.Identity.IntegrationTests.Persistence;

/// <summary>
/// Asserts the MassTransit transactional outbox contract in the Identity EF
/// Core model, built offline from the configuration assemblies (ADR-015 at the
/// Identity producer outbox gate). No Docker or live database is required: the
/// relational model is produced solely from
/// <see cref="IdentityDbContext.OnModelCreating"/>.
/// </summary>
public sealed class IdentityOutboxModelTests
{
    private static IdentityDbContext CreateOfflineContext()
    {
        var options = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseNpgsql(
                "Host=localhost;Port=5432;Database=communityos_identity;Username=communityos;Password=communityos",
                npgsql => npgsql.MigrationsAssembly("CommunityOS.Identity.Infrastructure"))
            .Options;

        return new IdentityDbContext(options);
    }

    [Fact]
    public void Context_uses_the_identity_schema_and_owns_the_mass_transit_outbox()
    {
        using var db = CreateOfflineContext();

        db.Model.GetRelationalModel();

        db.Model.GetDefaultSchema().Should().Be("identity");

        var outbox = db.Model.GetEntityTypes()
            .Where(e => e.ClrType.FullName?.StartsWith("MassTransit.", StringComparison.Ordinal) == true)
            .ToList();
        outbox.Should().NotBeEmpty();
        outbox.Should().OnlyContain(e => e.GetSchema() == "identity");
        outbox.Select(e => e.GetTableName()).Should().Contain(
        [
            "InboxState",
            "OutboxState",
            "OutboxMessage"
        ]);
    }
}
