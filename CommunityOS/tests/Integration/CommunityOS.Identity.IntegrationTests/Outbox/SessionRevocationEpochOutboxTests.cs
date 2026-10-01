using CommunityOS.Identity.Application;
using CommunityOS.Identity.Application.Commands;
using CommunityOS.Identity.Application.Crypto;
using CommunityOS.Identity.Application.Interfaces;
using CommunityOS.Identity.Domain.Aggregates;
using CommunityOS.Identity.Domain.Events;
using CommunityOS.Identity.Domain.Repositories;
using CommunityOS.Identity.Domain.ValueObjects;
using CommunityOS.Identity.Infrastructure.Integration;
using CommunityOS.Identity.Infrastructure.Persistence;
using CommunityOS.Identity.Infrastructure.Repositories;
using FluentAssertions;
using MassTransit;
using MassTransit.EntityFrameworkCoreIntegration;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Npgsql;
using System.Text.Json;
using Xunit;

namespace CommunityOS.Identity.IntegrationTests.Outbox;

/// <summary>
/// ADR-036 Q4: the emergency invalidation must advance the session-revocation
/// epoch, revoke the account's active refresh families, and publish
/// <c>SessionRevocationEpochAdvanced</c> through the MassTransit transactional
/// outbox as ONE atomic unit on the live PostgreSQL 18.6 instance. A rollback
/// leaves no epoch change, no revocation, and no orphaned outbox row. Once per
/// decision, the event carries the resulting epoch.
/// <para>
/// Bus wiring follows the repo's established outbox-persistence pattern
/// (Localization/Correspondence integration suites): the bus is never started,
/// because with <c>UseBusOutbox</c> the publish is captured into the scoped
/// DbContext before any transport involvement — no broker is required to prove
/// durable, atomic persistence. This suite does not exercise broker delivery
/// and does not weaken the production <c>WaitUntilStarted</c> setting.
/// </para>
/// </summary>
public sealed class SessionRevocationEpochOutboxTests : IAsyncLifetime
{
    private const string ConnectionString =
        "Host=localhost;Port=5432;Database=communityos_identity;Username=communityos;Password=communityos";

    private const string EpochEventUrn =
        "urn:message:CommunityOS.Contracts.Identity:SessionRevocationEpochAdvanced";

    private readonly ServiceProvider _provider;
    private readonly List<Guid> _createdAccountIds = [];

    public SessionRevocationEpochOutboxTests()
    {
        _provider = BuildProvider();
    }

    private static ServiceProvider BuildProvider(Action<IServiceCollection>? addExtraRegistrations = null)
    {
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.SetMinimumLevel(LogLevel.Warning));
        services.AddIdentityApplication();
        services.AddDbContext<IdentityDbContext>(opts =>
            opts.UseNpgsql(ConnectionString, npgsql => npgsql
                .MigrationsAssembly(typeof(IdentityDbContext).Assembly.FullName)));
        services.AddScoped<IUserAccountRepository, UserAccountRepository>();
        services.AddScoped<ISessionRepository, SessionRepository>();
        services.AddScoped<ISecurityEventRepository, SecurityEventRepository>();
        services.AddScoped<IUnitOfWork, IdentityUnitOfWork>();

        // ADR-036 Q4: the real domain-event -> integration-event forwarder, so
        // the outbox captures the epoch decision exactly as production does.
        services.AddScoped(typeof(INotificationHandler<>), typeof(IdentityIntegrationEventPublisher<>));

        services.AddMassTransit(bus =>
        {
            // Production wiring (EventBusServiceExtensions.ConfigureOutbox) on
            // the live IdentityDbContext. The long query delay keeps the
            // delivery service idle so assertions observe persisted rows.
            bus.AddEntityFrameworkOutbox<IdentityDbContext>(outbox =>
            {
                outbox.UsePostgres();
                outbox.QueryDelay = TimeSpan.FromMinutes(10);
                outbox.UseBusOutbox();
            });
            bus.UsingInMemory((_, _) => { });
        });

        addExtraRegistrations?.Invoke(services);

        return services.BuildServiceProvider();
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

        // Only this suite produces SessionRevocationEpochAdvanced outbox rows;
        // remove them so the shared live database stays clean.
        await using var db = CreateContext();
        await db.Set<OutboxMessage>()
            .Where(m => m.Body.Contains(EpochEventUrn))
            .ExecuteDeleteAsync();

        _createdAccountIds.Clear();
    }

    // --- Seeding -------------------------------------------------------------

    private async Task<UserAccount> SeedAsync()
    {
        await using var scope = _provider.CreateAsyncScope();
        var accounts = scope.ServiceProvider.GetRequiredService<IUserAccountRepository>();

        var account = UserAccount.Register(
            Email.Create($"q4o-{Guid.NewGuid():N}@example.org"), "password-hash");
        await accounts.AddAsync(account);
        _createdAccountIds.Add(account.Id);
        return account;
    }

    private static (Session Session, string Token) MakeSession(Guid userAccountId, long epoch)
    {
        var token = $"RT-{Guid.NewGuid():N}";
        var session = Session.Create(
            userAccountId, Guid.NewGuid(), TokenHasher.Hash(token), TimeSpan.FromDays(30),
            sessionRevocationEpochAtIssue: epoch);
        return (session, token);
    }

    private async Task AddSessionAsync(Session session)
    {
        await using var scope = _provider.CreateAsyncScope();
        var sessions = scope.ServiceProvider.GetRequiredService<ISessionRepository>();
        await sessions.AddAsync(session);
    }

    private static async Task InvalidateAsync(IServiceProvider provider, Guid accountId)
    {
        await using var scope = provider.CreateAsyncScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        await sender.Send(new InvalidateAllSessionsCommand(accountId), CancellationToken.None);
    }

    // --- Outbox readers --------------------------------------------------------

    private static async Task<int> EpochOutboxCountAsync(IdentityDbContext db) =>
        await db.Set<OutboxMessage>()
            .CountAsync(m => m.Body.Contains(EpochEventUrn));

    private static async Task<List<(Guid UserAccountId, long Epoch, DateTime OccurredOn)>>
        ReadEpochMessagesAsync(IdentityDbContext db)
    {
        var rows = await db.Set<OutboxMessage>()
            .Where(m => m.Body.Contains(EpochEventUrn))
            .OrderBy(m => m.SentTime)
            .ToListAsync();

        return rows.Select(ReadEpochMessage).ToList();
    }

    private static (Guid UserAccountId, long Epoch, DateTime OccurredOn) ReadEpochMessage(OutboxMessage row)
    {
        using var doc = JsonDocument.Parse(row.Body);
        var root = doc.RootElement;

        var messageTypes = root.EnumerateObject()
            .First(p => string.Equals(p.Name, "messageType", StringComparison.OrdinalIgnoreCase))
            .Value.EnumerateArray().Select(v => v.GetString()).ToList();
        messageTypes.Should().Contain(EpochEventUrn,
            "the outbox row is the SessionRevocationEpochAdvanced contract");

        var message = root.EnumerateObject()
            .First(p => string.Equals(p.Name, "message", StringComparison.OrdinalIgnoreCase))
            .Value;
        Guid UserAccountId() => message.EnumerateObject()
            .First(p => string.Equals(p.Name, "userAccountId", StringComparison.OrdinalIgnoreCase))
            .Value.GetGuid();
        long Epoch() => message.EnumerateObject()
            .First(p => string.Equals(p.Name, "sessionRevocationEpoch", StringComparison.OrdinalIgnoreCase))
            .Value.GetInt64();
        DateTime OccurredOn() => message.EnumerateObject()
            .First(p => string.Equals(p.Name, "occurredOn", StringComparison.OrdinalIgnoreCase))
            .Value.GetDateTime();

        return (UserAccountId(), Epoch(), OccurredOn());
    }

    private static async Task<IReadOnlyList<Session>> LoadFamilyAsync(IServiceProvider provider, Guid familyId)
    {
        await using var scope = provider.CreateAsyncScope();
        var sessions = scope.ServiceProvider.GetRequiredService<ISessionRepository>();
        return await sessions.GetByFamilyAsync(familyId);
    }

    // --- Tests -----------------------------------------------------------------

    [Fact]
    public async Task Emergency_invalidation_advances_epoch_and_persists_the_event_to_the_outbox_atomically()
    {
        var account = await SeedAsync();
        var (first, _) = MakeSession(account.Id, epoch: 0);
        await AddSessionAsync(first);

        await InvalidateAsync(_provider, account.Id);

        // Exactly one SessionRevocationEpochAdvanced row, carrying the account
        // and the resulting epoch — the outbox row is committed atomically with
        // the epoch advance.
        await using (var read = CreateContext())
        {
            var messages = await ReadEpochMessagesAsync(read);
            messages.Should().ContainSingle();
            messages[0].UserAccountId.Should().Be(account.Id);
            messages[0].Epoch.Should().Be(1);
            messages[0].OccurredOn.Should().NotBe(default);
        }

        // No OTHER identity event is emitted by the invalidation.
        await using (var read = CreateContext())
        {
            var allIdentityRows = await read.Set<OutboxMessage>()
                .CountAsync(m => m.Body.Contains("urn:message:CommunityOS.Contracts.Identity:"));
            allIdentityRows.Should().Be(1,
                "the emergency publishes exactly the epoch event and nothing else");
        }

        // The committed state: epoch advanced once, every active family revoked.
        await using (var verify = CreateContext())
        {
            var reloaded = await verify.UserAccounts.AsNoTracking().SingleAsync(a => a.Id == account.Id);
            reloaded.SessionRevocationEpoch.Should().Be(1);

            var family = await verify.Sessions.AsNoTracking()
                .Where(s => s.TokenFamilyId == first.TokenFamilyId
                            && s.UserAccountId == account.Id)
                .ToListAsync();
            family.Should().ContainSingle().Which.IsRevoked.Should().BeTrue();
        }
    }

    [Fact]
    public async Task Two_decisions_publish_each_new_epoch_exactly_once()
    {
        var account = await SeedAsync();
        var (first, _) = MakeSession(account.Id, epoch: 0);
        await AddSessionAsync(first);

        await InvalidateAsync(_provider, account.Id);
        await InvalidateAsync(_provider, account.Id);

        await using var read = CreateContext();
        var messages = await ReadEpochMessagesAsync(read);
        messages.Should().HaveCount(2,
            "each emergency decision produces exactly one event");
        messages.Should().OnlyContain(m => m.UserAccountId == account.Id);
        messages.Select(m => m.Epoch).Should().Equal([1L, 2L],
            "the event represents the resulting epoch exactly once per decision");

        var reloaded = await read.UserAccounts.AsNoTracking().SingleAsync(a => a.Id == account.Id);
        reloaded.SessionRevocationEpoch.Should().Be(2);
    }

    [Fact]
    public async Task A_rolled_back_invalidation_leaves_no_epoch_change_no_revocation_and_no_outbox_event()
    {
        // A handler registered AFTER the publisher fails after the event has
        // been captured, forcing the emergency transaction to roll back.
        using var rollbackProvider = BuildProvider(services =>
            services.AddScoped<INotificationHandler<SessionRevocationEpochAdvancedEvent>,
                ThrowingEpochHandler>());

        var account = await SeedAsync();
        var (first, _) = MakeSession(account.Id, epoch: 0);
        await AddSessionAsync(first);

        await using var before = CreateContext();
        var baselineOutbox = await EpochOutboxCountAsync(before);

        var send = async () => await InvalidateAsync(rollbackProvider, account.Id);
        await send.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("simulated post-publish failure",
                "the failure comes from the post-capture handler, proving the event was buffered before the rollback");

        await using (var read = CreateContext())
        {
            // None of the four must have been committed independently.
            (await EpochOutboxCountAsync(read)).Should().Be(baselineOutbox,
                "the buffered outbox row is rolled back with the transaction");
            (await read.UserAccounts.AsNoTracking().SingleAsync(a => a.Id == account.Id))
                .SessionRevocationEpoch.Should().Be(0,
                "the epoch advance rolls back");
        }

        var family = await LoadFamilyAsync(_provider, first.TokenFamilyId);
        family.Should().ContainSingle().Which.IsRevoked.Should().BeFalse(
            "the family revocation rolls back");
    }

    private sealed class ThrowingEpochHandler : INotificationHandler<SessionRevocationEpochAdvancedEvent>
    {
        public Task Handle(SessionRevocationEpochAdvancedEvent notification, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("simulated post-publish failure");
    }
}