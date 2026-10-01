using System.Data.Common;
using System.Reflection;
using CommunityOS.Identity.Application;
using CommunityOS.Identity.Application.Commands;
using CommunityOS.Identity.Application.Interfaces;
using CommunityOS.Identity.Domain.Aggregates;
using CommunityOS.Identity.Domain.Repositories;
using CommunityOS.Identity.Domain.ValueObjects;
using CommunityOS.Identity.Infrastructure.Persistence;
using CommunityOS.Identity.Infrastructure.Repositories;
using CommunityOS.SharedKernel.Domain.Primitives;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Npgsql;
using Xunit;

namespace CommunityOS.Identity.IntegrationTests.Concurrency;

/// <summary>
/// C1 (ADR-036 Section 19, OD-18 — PostgreSQL <c>xmin</c>) focused proofs
/// against the live local PostgreSQL 18.6 instance. Scope is only the C1
/// <c>UserAccount</c> persistence/concurrency boundary; nothing here asserts
/// anything about the open Section 18 questions.
/// <para>
/// <b>Stale-writer invariant under test:</b> no stale whole-row
/// <c>UserAccount</c> write may overwrite a newer committed
/// <c>SessionRevocationEpoch</c>; the stale write must fail with the normal EF
/// concurrency failure, must not be clamped, and must not be retried.
/// </para>
/// </summary>
public sealed class UserAccountXminConcurrencyTests : IAsyncLifetime
{
    private const string ConnectionString =
        "Host=localhost;Port=5432;Database=communityos_identity;Username=communityos;Password=communityos";

    private readonly ServiceProvider _provider;
    private readonly List<Guid> _createdAccountIds = [];

    public UserAccountXminConcurrencyTests()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:IdentityDb"] = ConnectionString,
                ["Jwt:Issuer"] = "test-issuer",
                ["Jwt:Audience"] = "test-audience"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(config);
        services.AddLogging(builder => builder.SetMinimumLevel(LogLevel.Warning));
        services.AddIdentityApplication();
        services.AddDbContext<IdentityDbContext>(opts =>
            opts.UseNpgsql(ConnectionString, npgsql => npgsql
                .MigrationsAssembly(typeof(IdentityDbContext).Assembly.FullName)));
        services.AddScoped<IUserAccountRepository, UserAccountRepository>();
        services.AddScoped<ISessionRepository, SessionRepository>();
        services.AddScoped<ISecurityEventRepository, SecurityEventRepository>();
        services.AddScoped<IUnitOfWork, IdentityUnitOfWork>();

        _provider = services.BuildServiceProvider();
    }

    public async Task InitializeAsync()
    {
        await using var db = CreateContext();
        await db.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        await CleanupAsync();
        await _provider.DisposeAsync();
    }

    private static IdentityDbContext CreateContext(List<string>? sqlLog = null)
    {
        var builder = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseNpgsql(ConnectionString, npgsql => npgsql
                .MigrationsAssembly(typeof(IdentityDbContext).Assembly.FullName));

        if (sqlLog is not null)
            builder.AddInterceptors(new SqlCapturingInterceptor(sqlLog));

        return new IdentityDbContext(builder.Options);
    }

    private sealed class SqlCapturingInterceptor(List<string> log) : DbCommandInterceptor
    {
        public override InterceptionResult<int> NonQueryExecuting(
            DbCommand command, CommandEventData eventData, InterceptionResult<int> result)
        {
            log.Add(command.CommandText);
            return result;
        }

        public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
            DbCommand command, CommandEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            log.Add(command.CommandText);
            return ValueTask.FromResult(result);
        }

        public override InterceptionResult<DbDataReader> ReaderExecuting(
            DbCommand command, CommandEventData eventData,
            InterceptionResult<DbDataReader> result)
        {
            log.Add(command.CommandText);
            return result;
        }

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command, CommandEventData eventData,
            InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        {
            log.Add(command.CommandText);
            return ValueTask.FromResult(result);
        }
    }

    private async Task CleanupAsync()
    {
        if (_createdAccountIds.Count == 0)
            return;

        await using var conn = new NpgsqlConnection(ConnectionString);
        await conn.OpenAsync();
        foreach (var id in _createdAccountIds)
        {
            await using var cmd = new NpgsqlCommand(
                """
                DELETE FROM identity.security_events WHERE user_account_id = @id;
                DELETE FROM identity.sessions WHERE user_account_id = @id;
                DELETE FROM identity.user_accounts WHERE "Id" = @id;
                """, conn);
            cmd.Parameters.AddWithValue("id", id);
            await cmd.ExecuteNonQueryAsync();
        }

        _createdAccountIds.Clear();
    }

    private async Task<Guid> SeedAsync(long epoch = 0)
    {
        await using var db = CreateContext();
        var account = UserAccount.Register(
            Email.Create($"c1-{Guid.NewGuid():N}@example.org"), "password-hash");
        await db.UserAccounts.AddAsync(account);

        for (var i = 0; i < epoch; i++)
            account.AdvanceSessionRevocationEpochOnce();

        await db.SaveChangesAsync();
        _createdAccountIds.Add(account.Id);
        return account.Id;
    }

    private static async Task<long> ReadEpochAsync(Guid id)
    {
        await using var db = CreateContext();
        var reloaded = await db.UserAccounts.AsNoTracking().SingleAsync(a => a.Id == id);
        return reloaded.SessionRevocationEpoch;
    }

    private static async Task InvalidateAsync(IServiceProvider provider, Guid accountId)
    {
        await using var scope = provider.CreateAsyncScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        await sender.Send(new InvalidateAllSessionsCommand(accountId), CancellationToken.None);
    }

    // =====================================================================
    // Scope of the concurrency configuration (OD-18 constraints 6, 7, 8, 9)
    // =====================================================================

    [Fact]
    public void Xmin_concurrency_token_is_configured_on_UserAccount_only()
    {
        using var db = CreateContext();

        var withXmin = db.Model.GetEntityTypes()
            .SelectMany(e => e.GetProperties()
                .Where(p => string.Equals(
                    p.GetColumnName(), "xmin", StringComparison.OrdinalIgnoreCase))
                .Select(p => (Entity: e.ClrType?.Name ?? e.Name, Property: p.Name)))
            .ToList();

        withXmin.Should().HaveCount(1,
            "xmin concurrency protection is scoped to UserAccount and must not leak to any other entity");
        withXmin[0].Entity.Should().Be(nameof(UserAccount));
        withXmin[0].Property.Should().Be("xmin");

        var xmin = db.Model.FindEntityType(typeof(UserAccount))!.FindProperty("xmin")!;
        xmin.ClrType.Should().Be<uint>();
        xmin.IsConcurrencyToken.Should().BeTrue();
        xmin.ValueGenerated.Should().Be(Microsoft.EntityFrameworkCore.Metadata.ValueGenerated.OnAddOrUpdate);
        xmin.GetColumnName().Should().Be("xmin");
        xmin.IsShadowProperty().Should().BeTrue(
            "a shadow property keeps AggregateRoot<TId> and the domain model unmodified");
    }

    [Fact]
    public void AggregateRoot_is_unmodified_and_its_Version_column_mapping_is_preserved()
    {
        using var db = CreateContext();

        // OD-18 constraint 8: AggregateRoot<TId> is not touched.
        var versionProp = typeof(AggregateRoot<Guid>)
            .GetProperty(nameof(AggregateRoot<Guid>.Version))!;
        versionProp.PropertyType.Should().Be<uint>();
        versionProp.GetSetMethod(nonPublic: false).Should().BeNull(
            "the setter remains protected, i.e. the shared-kernel type is unchanged");
        versionProp.GetGetMethod(nonPublic: false).Should().NotBeNull();

        // The pre-existing "Version" bigint column keeps its own mapping and is
        // NOT turned into a concurrency token.
        var uaVersion = db.Model.FindEntityType(typeof(UserAccount))!.FindProperty("Version")!;
        uaVersion.GetColumnName().Should().Be("Version");
        uaVersion.GetColumnType().Should().Be("bigint");
        uaVersion.IsConcurrencyToken.Should().BeFalse();
    }

    [Fact]
    public void Xmin_produces_no_schema_DDL()
    {
        using var db = CreateContext();

        // OD-18 constraint 9: xmin is a PostgreSQL system column, so introducing
        // the concurrency mechanism implies no migration and no schema change.
        var script = db.Database.GenerateCreateScript();
        script.Should().NotContain("xmin",
            "Npgsql excludes system columns from generated DDL");
    }

    // =====================================================================
    // Test A — stale whole-row writer
    // =====================================================================

    [Fact]
    public async Task Stale_whole_row_writer_is_rejected_and_newer_epoch_is_not_overwritten()
    {
        var id = await SeedAsync();

        // Writer A observes epoch N = 0 and keeps its tracked, unlocked entity.
        long observedEpoch;
        await using (var writerA = CreateContext())
        {
            var stale = await writerA.UserAccounts.SingleAsync(a => a.Id == id);
            observedEpoch = stale.SessionRevocationEpoch;
            observedEpoch.Should().Be(0);

            // Writer B advances the epoch twice and commits first (no lock is
            // taken by A, so nothing serialises them).
            await using (var writerB = CreateContext())
            {
                var fresh = await writerB.UserAccounts.SingleAsync(a => a.Id == id);
                fresh.AdvanceSessionRevocationEpochOnce();
                fresh.AdvanceSessionRevocationEpochOnce();
                await writerB.SaveChangesAsync();
            }

            (await ReadEpochAsync(id)).Should().Be(2, "writer B committed two advances");

            // Writer A now advances its own STALE copy once and attempts the
            // whole-row write. Its in-memory epoch (1) is LOWER than the
            // committed epoch (2), so without the concurrency guard this write
            // would persist a decrease.
            stale.AdvanceSessionRevocationEpochOnce();
            stale.SessionRevocationEpoch.Should().Be(observedEpoch + 1);
            writerA.UserAccounts.Update(stale);

            var act = async () => await writerA.SaveChangesAsync();
            await act.Should().ThrowAsync<DbUpdateConcurrencyException>(
                "a stale whole-row write must fail with the normal EF concurrency failure");

            // OD-18 constraint 4: not clamped, not normalized. The committed
            // value survives untouched and is neither lowered to 1 nor raised.
            var afterReject = await writerA.UserAccounts.AsNoTracking()
                .SingleAsync(a => a.Id == id);
            afterReject.SessionRevocationEpoch.Should().Be(2,
                "the stale write must not overwrite, clamp or normalize the newer committed epoch");
        }

        (await ReadEpochAsync(id)).Should().Be(2,
            "the newer committed epoch survives the rejected stale write");
    }

    // =====================================================================
    // Test B — legitimate non-conflicting update
    // =====================================================================

    [Fact]
    public async Task Non_conflicting_update_with_the_current_xmin_succeeds()
    {
        var id = await SeedAsync();

        await using (var writer = CreateContext())
        {
            var account = await writer.UserAccounts.SingleAsync(a => a.Id == id);
            account.AdvanceSessionRevocationEpochOnce();
            await writer.SaveChangesAsync();
        }

        (await ReadEpochAsync(id)).Should().Be(1,
            "a write made with the correct original xmin is accepted normally");
    }

    // =====================================================================
    // Test C — emergency invalidation interaction
    // =====================================================================

    [Fact]
    public async Task Emergency_invalidation_path_participates_and_still_blocks_stale_writers()
    {
        var id = await SeedAsync();

        // The emergency path loads the row via FromSqlInterpolated("... FOR UPDATE").
        // PostgreSQL does not return system columns for a bare SELECT *, so this
        // proves the rowversion value is actually loaded there; if it were not,
        // the emergency UPDATE could not match its own concurrency predicate.
        await InvalidateAsync(_provider, id);

        (await ReadEpochAsync(id)).Should().Be(1,
            "the emergency path advances the epoch exactly once and commits");

        // A second emergency run is also accepted (correct xmin each time).
        await InvalidateAsync(_provider, id);
        (await ReadEpochAsync(id)).Should().Be(2);

        // A non-locking writer observes epoch 2, a competing writer then commits
        // epoch 4, which leaves the first writer genuinely stale.
        await using (var writerA = CreateContext())
        {
            var stale = await writerA.UserAccounts.SingleAsync(a => a.Id == id);
            stale.SessionRevocationEpoch.Should().Be(2);

            await using (var writerB = CreateContext())
            {
                var fresh = await writerB.UserAccounts.SingleAsync(a => a.Id == id);
                fresh.AdvanceSessionRevocationEpochOnce();
                fresh.AdvanceSessionRevocationEpochOnce();
                await writerB.SaveChangesAsync();
            }

            (await ReadEpochAsync(id)).Should().Be(4);

            stale.AdvanceSessionRevocationEpochOnce();
            writerA.UserAccounts.Update(stale);
            var act = async () => await writerA.SaveChangesAsync();
            await act.Should().ThrowAsync<DbUpdateConcurrencyException>(
                "the emergency path does not bypass the xmin protection for later writers");

            (await ReadEpochAsync(id)).Should().Be(4,
                "the stale writer neither overwrites nor clamps the newer committed epoch");
        }
    }

    // =====================================================================
    // Test D — no automatic retry
    // =====================================================================

    [Fact]
    public async Task Concurrency_conflict_is_surfaced_without_any_automatic_retry()
    {
        var id = await SeedAsync();

        var sqlLog = new List<string>();
        await using (var writerA = CreateContext(sqlLog))
        {
            var stale = await writerA.UserAccounts.SingleAsync(a => a.Id == id);
            stale.AdvanceSessionRevocationEpochOnce();
            writerA.UserAccounts.Update(stale);

            // A competing writer commits first, so the save below really conflicts.
            await using (var writerB = CreateContext())
            {
                var fresh = await writerB.UserAccounts.SingleAsync(a => a.Id == id);
                fresh.AdvanceSessionRevocationEpochOnce();
                await writerB.SaveChangesAsync();
            }

            sqlLog.Clear(); // ignore the SELECT issued to load the stale entity

            var act = async () => await writerA.SaveChangesAsync();
            await act.Should().ThrowAsync<DbUpdateConcurrencyException>(
                "the conflict is surfaced to the caller, not swallowed or converted");

            var userAccountUpdates = sqlLog
                .Where(s => s.Contains("UPDATE", StringComparison.OrdinalIgnoreCase)
                            && s.Contains("identity.user_accounts", StringComparison.Ordinal))
                .ToList();

            userAccountUpdates.Should().HaveCount(1,
                "OD-18 constraint 11: exactly one UPDATE attempt, i.e. no retry loop and no retry policy");
        }
    }

    // =====================================================================
    // Concurrency proof — the generated UPDATE is conditional on xmin
    // =====================================================================

    [Fact]
    public async Task Generated_update_is_predicated_on_the_original_xmin()
    {
        var id = await SeedAsync();

        var sqlLog = new List<string>();
        await using (var writer = CreateContext(sqlLog))
        {
            var account = await writer.UserAccounts.SingleAsync(a => a.Id == id);
            account.AdvanceSessionRevocationEpochOnce();
            sqlLog.Clear();

            await writer.SaveChangesAsync();
        }

        var update = sqlLog.Single(s =>
            s.Contains("UPDATE", StringComparison.OrdinalIgnoreCase)
            && s.Contains("identity.user_accounts", StringComparison.Ordinal));

        update.Should().Contain("xmin",
            "the UPDATE carries a concurrency predicate on the PostgreSQL xmin system column");
        update.Should().MatchRegex(@"WHERE[\s\S]*xmin\s*=",
            "the original xmin is supplied as the WHERE predicate of the UPDATE");

        // The newer epoch really did land, proving the predicate matched on the
        // current (non-stale) row.
        (await ReadEpochAsync(id)).Should().Be(1);
    }
}
