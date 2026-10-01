using CommunityOS.Identity.Domain.Aggregates;
using CommunityOS.Identity.Domain.ValueObjects;
using CommunityOS.Identity.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Xunit;

namespace CommunityOS.Identity.IntegrationTests.Persistence;

/// <summary>
/// ADR-036 D1/Q2 persistence proofs against the live local PostgreSQL 18.6
/// instance (see docker/docker-compose.yml and IdentityDbContextFactory): the
/// migration applies successfully, <c>identity.user_accounts</c> gains
/// <c>session_revocation_epoch</c> BIGINT NOT NULL DEFAULT 0, existing and new
/// accounts observe epoch 0, an account can advance the epoch exactly once, the
/// changed value persists, and a fresh <see cref="IdentityDbContext"/> reloads
/// the same account observing the persisted value.
/// </summary>
public sealed class SessionRevocationEpochPersistenceTests : IAsyncLifetime
{
    private const string ConnectionString =
        "Host=localhost;Port=5432;Database=communityos_identity;Username=communityos;Password=communityos";

    private readonly List<Guid> _createdAccountIds = [];

    public async Task InitializeAsync()
    {
        // Idempotent: verifies the full migration chain (including the Q2
        // AddSessionRevocationEpoch migration) applies cleanly against the live
        // PostgreSQL 18.6 instance.
        await using var db = CreateContext();
        await db.Database.MigrateAsync();
    }

    public Task DisposeAsync() => CleanupAsync();

    private static IdentityDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseNpgsql(ConnectionString, npgsql => npgsql
                .MigrationsAssembly(typeof(IdentityDbContext).Assembly.FullName))
            .Options;
        return new IdentityDbContext(options);
    }

    private async Task CleanupAsync()
    {
        if (_createdAccountIds.Count == 0)
        {
            return;
        }

        await using var db = CreateContext();
        foreach (var id in _createdAccountIds)
        {
            var account = await db.UserAccounts.IgnoreQueryFilters()
                .FirstOrDefaultAsync(a => a.Id == id);
            if (account is not null)
            {
                db.UserAccounts.Remove(account);
            }
        }

        await db.SaveChangesAsync();
        _createdAccountIds.Clear();
    }

    private static async Task<(string DataType, bool IsNullable, string? Default)>
        GetEpochColumnDefinitionAsync()
    {
        await using var conn = new NpgsqlConnection(ConnectionString);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand(
            """
            SELECT data_type, is_nullable, column_default
            FROM information_schema.columns
            WHERE table_schema = 'identity'
              AND table_name = 'user_accounts'
              AND column_name = 'session_revocation_epoch'
            """, conn);
        await using var reader = await cmd.ExecuteReaderAsync();
        await reader.ReadAsync();
        return (
            reader.GetString(0),
            reader.GetString(1) == "YES",
            reader.IsDBNull(2) ? null : reader.GetString(2));
    }

    [Fact]
    public async Task Migration_applies_and_epoch_column_matches_the_contract()
    {
        await using var db = CreateContext();

        var applied = await db.Database.GetAppliedMigrationsAsync();
        applied.Should().Contain(
            "20260923073201_AddSessionRevocationEpoch",
            "the Q2 migration must be recorded in the live database");

        var (dataType, isNullable, columnDefault) = await GetEpochColumnDefinitionAsync();

        dataType.Should().Be("bigint");
        isNullable.Should().BeFalse();
        columnDefault.Should().StartWith("0", "DEFAULT 0 backfills existing rows");
    }

    [Fact]
    public async Task New_account_begins_at_epoch_zero()
    {
        Guid accountId;
        await using (var setup = CreateContext())
        {
            var account = UserAccount.Register(
                Email.Create($"epoch-{Guid.NewGuid():N}@example.org"), "password-hash");
            accountId = account.Id;
            _createdAccountIds.Add(accountId);
            setup.UserAccounts.Add(account);
            await setup.SaveChangesAsync();
        }

        await using var db = CreateContext();
        var reloaded = await db.UserAccounts.AsNoTracking().SingleAsync(a => a.Id == accountId);

        reloaded.SessionRevocationEpoch.Should().Be(0,
            "the initial value of a freshly registered account is 0");
    }

    [Fact]
    public async Task Account_can_advance_epoch_and_the_change_persists()
    {
        Guid accountId;
        await using (var setup = CreateContext())
        {
            var account = UserAccount.Register(
                Email.Create($"epoch-{Guid.NewGuid():N}@example.org"), "password-hash");
            accountId = account.Id;
            _createdAccountIds.Add(accountId);
            setup.UserAccounts.Add(account);
            await setup.SaveChangesAsync();
        }

        await using (var writer = CreateContext())
        {
            var loaded = await writer.UserAccounts.SingleAsync(a => a.Id == accountId);
            loaded.SessionRevocationEpoch.Should().Be(0);

            loaded.AdvanceSessionRevocationEpochOnce();
            await writer.SaveChangesAsync();
        }

        // A fresh DbContext must observe the persisted value.
        await using (var verify = CreateContext())
        {
            var reloaded = await verify.UserAccounts.AsNoTracking().SingleAsync(a => a.Id == accountId);
            reloaded.SessionRevocationEpoch.Should().Be(1,
                "the advanced epoch survives SaveChanges and a fresh context reload");
        }
    }
}