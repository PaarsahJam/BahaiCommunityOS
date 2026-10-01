using CommunityOS.Authorization.Application.Authorization;
using CommunityOS.Localization.Application;
using CommunityOS.Localization.Domain;
using CommunityOS.Localization.Domain.Events;
using CommunityOS.Localization.Domain.Exceptions;
using CommunityOS.Localization.Infrastructure;
using CommunityOS.Localization.Infrastructure.Persistence;
using FluentAssertions;
using MassTransit;
using MassTransit.EntityFrameworkCoreIntegration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Testcontainers.PostgreSql;
using Xunit;

namespace CommunityOS.Localization.IntegrationTests.Persistence;

/// <summary>
/// Persistence proofs against a real PostgreSQL instance with the production
/// bus-outbox wiring (ADR-015, ADR-029 decisions 14/16):
///
/// L-01 — the migration creates the localization schema with the ratified
/// seed: exactly one locale (en) active and default; catalog_state at 0;
/// fa/ar are candidates only and are never seeded.
///
/// L-02 — approval bumps the global catalog version and lands the
/// LocalizationCatalogChanged outbox row in the SAME save as the mutation;
/// a rolled-back transaction leaves neither behind; payloads carry codes,
/// versions and timestamps only.
///
/// NOTE: Docker is unavailable in the current environment, so this suite is
/// compile-only. It is executed in CI where a container runtime exists.
/// </summary>
public sealed class LocalizationPersistenceTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("communityos_localization")
        .WithUsername("communityos")
        .WithPassword("communityos")
        .Build();

    private ServiceProvider? _provider;

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
        var connectionString = _postgres.GetConnectionString();

        await using var bootstrap = new LocalizationDbContext(
            new DbContextOptionsBuilder<LocalizationDbContext>()
                .UseNpgsql(connectionString, npgsql => npgsql
                    .MigrationsAssembly(typeof(LocalizationDbContext).Assembly.FullName))
                .Options);
        await bootstrap.Database.MigrateAsync();

        var services = new ServiceCollection();
        services.AddDbContext<LocalizationDbContext>(options =>
            options.UseNpgsql(connectionString));
        services.AddMassTransit(bus =>
        {
            // Production wiring (EventBusServiceExtensions.ConfigureOutbox) on
            // the in-memory transport. The long query delay keeps the delivery
            // service idle so assertions observe persisted rows pre-delivery.
            bus.AddEntityFrameworkOutbox<LocalizationDbContext>(outbox =>
            {
                outbox.UsePostgres();
                outbox.QueryDelay = TimeSpan.FromMinutes(10);
                outbox.UseBusOutbox();
            });
            bus.UsingInMemory((_, _) => { });
        });

        _provider = services.BuildServiceProvider();

        // The bus is deliberately not started: with UseBusOutbox, publishes
        // are captured into the scoped DbContext before any transport
        // involvement, so no broker is needed to prove durable persistence.
    }

    public async Task DisposeAsync()
    {
        if (_provider is not null)
        {
            await _provider.DisposeAsync();
        }

        await _postgres.DisposeAsync();
    }

    private LocalizationDbContext CreateContext() =>
        _provider!.CreateScope().ServiceProvider.GetRequiredService<LocalizationDbContext>();

    private static LocalizationJournal CreateJournal(LocalizationDbContext db) =>
        new(db, NullLogger<LocalizationJournal>.Instance);

    private static async Task<List<string>> OutboxContractNamesAsync(LocalizationDbContext db)
    {
        var bodies = await db.Set<OutboxMessage>().Select(m => m.Body).ToListAsync();
        return bodies
            .Where(body => body.Contains("urn:message:CommunityOS.Contracts.Localization:", StringComparison.Ordinal))
            .Select(body =>
            {
                const string marker = "urn:message:CommunityOS.Contracts.Localization:";
                var start = body.IndexOf(marker, StringComparison.Ordinal) + marker.Length;
                return body[start..].Split('"', ',')[0];
            })
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToList();
    }

    [Fact]
    public async Task Migration_seeds_exactly_the_default_locale_and_zero_version()
    {
        await using var db = CreateContext();

        var locales = await db.Locales.AsNoTracking().ToListAsync();
        locales.Should().ContainSingle("the ratified seed policy seeds exactly one locale");
        var en = locales[0];
        en.Code.Should().Be("en");
        en.Status.Should().Be(LocaleStatus.Active);
        en.IsDefault.Should().BeTrue();

        (await db.CatalogState.AsNoTracking().SingleAsync()).Version.Should().Be(0L);

        // fa/ar are activation candidates only — never seeded.
        db.Locales.Any(l => l.Code == "fa" || l.Code == "ar").Should().BeFalse();
    }

    [Fact]
    public async Task Approval_bumps_version_and_captures_outbox_atomically()
    {
        var actor = Guid.NewGuid();
        var now = DateTime.UtcNow;

        await using var db = CreateContext();
        var journal = CreateJournal(db);
        await using var scope = _provider!.CreateAsyncScope();
        var publishEndpoint = scope.ServiceProvider.GetRequiredService<IPublishEndpoint>();

        var ns = ResourceNamespace.Create("ui", null, actor, now);
        await journal.SaveNamespaceAsync(ns, CancellationToken.None);

        var entry = ResourceEntry.Create(ns.Id, "greeting", now);
        entry.ProposeDraft("fa", "سلام", ContentProvenance.ForHuman(), actor, now);
        entry.SubmitForReview(entry.Revisions[0].Id, now);

        long publishedVersion = -1;

        // Mirrors ApproveRevisionHandler exactly: mutate → bump the global
        // version + publish the domain event (buffered into this context by
        // the bus outbox) → ONE atomic save.
        await journal.PublishCatalogChangeAsync(
            entry,
            async (version, ct) =>
            {
                publishedVersion = version;
                await publishEndpoint.Publish(new CatalogChangedDomainEvent(
                    "resources", ns.Name, "fa", version), ct);
            },
            CancellationToken.None);

        publishedVersion.Should().Be(1L);
        db.ChangeTracker.Clear();

        var types = await OutboxContractNamesAsync(db);
        types.Should().Equal(["LocalizationCatalogChanged"]);

        var body = (await db.Set<OutboxMessage>().Select(m => m.Body).SingleAsync());
        body.Should().NotContain("سلام",
            "catalog events carry codes and versions only — never values");
        body.Should().Contain("\"resources\"");

        // The committed version is observable from a fresh context.
        await using var verify = CreateContext();
        (await verify.CatalogState.AsNoTracking().SingleAsync()).Version.Should().Be(publishedVersion);
        verify.Entries.Any(e => e.Key == "greeting").Should().BeTrue();
    }

    [Fact]
    public async Task A_rolled_back_publish_leaves_neither_mutation_nor_outbox_row()
    {
        var actor = Guid.NewGuid();
        var now = DateTime.UtcNow;

        await using var db = CreateContext();
        var ns = ResourceNamespace.Create("rollback", null, actor, now);
        await CreateJournal(db).SaveNamespaceAsync(ns, CancellationToken.None);
        db.ChangeTracker.Clear();

        var entry = ResourceEntry.Create(ns.Id, "never-published", now);
        entry.ProposeDraft("en", "value", ContentProvenance.ForHuman(), actor, now);
        entry.SubmitForReview(entry.Revisions[0].Id, now);

        var baselineOutbox = await db.Set<OutboxMessage>().CountAsync();

        // The event callback throws AFTER the inner flush has buffered the
        // mutation + version bump: the journal must roll everything back.
        Func<Task> act = () => CreateJournal(db).PublishCatalogChangeAsync(
            entry,
            (_, _) => throw new InvalidOperationException("simulated failure after buffer"),
            CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();

        db.ChangeTracker.Clear();
        (await db.Set<OutboxMessage>().CountAsync()).Should().Be(baselineOutbox,
            "a failed business transaction discards the buffered event");
        db.Entries.Any(e => e.Key == "never-published")
            .Should().BeFalse("no mutation survives the rollback");
        (await db.CatalogState.AsNoTracking().SingleAsync()).Version.Should().Be(0L,
            "the version bump rolls back with the transaction");
    }

    [Fact]
    public async Task Concurrent_approval_of_the_same_aggregate_surfaces_as_conflict()
    {
        var actor = Guid.NewGuid();
        var now = DateTime.UtcNow;

        ResourceEntry seeded;
        await using (var setup = CreateContext())
        {
            var ns = ResourceNamespace.Create("conflict", null, actor, now);
            await CreateJournal(setup).SaveNamespaceAsync(ns, CancellationToken.None);
            seeded = ResourceEntry.Create(ns.Id, "hot.key", now);
            seeded.ProposeDraft("en", "one", ContentProvenance.ForHuman(), actor, now);
            seeded.SubmitForReview(seeded.Revisions[0].Id, now);
            await CreateJournal(setup).SaveEntryAsync(seeded, CancellationToken.None);
        }

        await using var first = CreateContext();
        await using var second = CreateContext();
        var firstEntry = await first.Entries.Include(e => e.Revisions).SingleAsync(e => e.Id == seeded.Id);
        var secondEntry = await second.Entries.Include(e => e.Revisions).SingleAsync(e => e.Id == seeded.Id);

        firstEntry.Approve(firstEntry.Revisions[0].Id, actor, now);
        secondEntry.Approve(secondEntry.Revisions[0].Id, actor, now);
        await CreateJournal(first).SaveEntryAsync(firstEntry, CancellationToken.None);

        // The xmin concurrency token makes the stale save fail; ratified
        // mapping turns that into a conflict, never a silent overwrite.
        Func<Task> act = () => CreateJournal(second).SaveEntryAsync(secondEntry, CancellationToken.None);
        await act.Should().ThrowAsync<DbUpdateConcurrencyException>();
    }

    [Fact]
    public void Reserved_source_contexts_are_rejected_before_any_storage()
    {
        var act = () => EntityTranslation.Create(
            "library", "document", Guid.NewGuid(), "display_name", "en", DateTime.UtcNow);
        act.Should().Throw<LocalizationConflictException>(
            "the Knowledge/Library boundary holds in the localization store too");

        var actKnowledge = () => EntityTranslation.Create(
            "knowledge", "article", Guid.NewGuid(), "title", "en", DateTime.UtcNow);
        actKnowledge.Should().Throw<LocalizationConflictException>();
    }

    [Fact]
    public async Task Entity_translation_upsert_and_query_flow_end_to_end()
    {
        var actor = Guid.NewGuid();
        var entityId = Guid.NewGuid();
        var guard = new AuthorizationGuard(new AllowAllEvaluator());

        await using var db = CreateContext();
        var journal = CreateJournal(db);
        var reader = new LocalizationReader(db);

        var created = await new UpsertEntityTranslationsHandler(reader, journal, guard)
            .Handle(new UpsertEntityTranslationsCommand(actor,
            [
                new UpsertEntityTranslationItem(
                    "community", "person", entityId, "display_name", "fa-IR", "علی")
            ]), CancellationToken.None);
        created.Created.Should().Be(1);

        // Same tuple (normalized culture): a NEW draft revision on the SAME
        // aggregate; history keeps the first value verbatim.
        var updated = await new UpsertEntityTranslationsHandler(reader, journal, guard)
            .Handle(new UpsertEntityTranslationsCommand(actor,
            [
                new UpsertEntityTranslationItem(
                    "community", "person", entityId, "display_name", "fa-ir", "علی‌رضا")
            ]), CancellationToken.None);
        updated.Created.Should().Be(0);
        updated.Updated.Should().Be(1);

        db.ChangeTracker.Clear();
        var found = await reader.FindEntityTranslationAsync(
            "community", "person", entityId, "display_name", "fa", CancellationToken.None);
        found!.Revisions.Should().HaveCount(2);
        found.Revisions[0].Value.Should().Be("علی",
            "the previously proposed value stays verbatim in history");
    }

    [Fact]
    public async Task State_filtered_walks_return_complete_pages_from_the_database()
    {
        var actor = Guid.NewGuid();
        var now = DateTime.UtcNow;

        await using var db = CreateContext();
        var journal = CreateJournal(db);
        var reader = new LocalizationReader(db);

        var ns = ResourceNamespace.Create("walk", null, actor, now);
        await journal.SaveNamespaceAsync(ns, CancellationToken.None);

        // Interleave states so a take-before-filter page window would silently
        // drop matching rows beyond it (16G finding LOC-03).
        foreach (var key in new[]
                 {
                     "a-approved", "b-draft", "c-approved",
                     "d-draft", "e-approved", "f-draft"
                 })
        {
            var entry = ResourceEntry.Create(ns.Id, key, now);
            entry.ProposeDraft("en", $"value-{key}", ContentProvenance.ForHuman(), actor, now);
            if (key.EndsWith("approved", StringComparison.Ordinal))
            {
                entry.SubmitForReview(entry.Revisions[0].Id, now);
                entry.Approve(entry.Revisions[0].Id, actor, now);
            }

            await journal.SaveEntryAsync(entry, CancellationToken.None);
        }

        db.ChangeTracker.Clear();

        // Walk the approved view with a window smaller than the match set.
        var walked = new List<string>();
        (string NamespaceName, string Key, Guid Id)? cursor = null;
        while (true)
        {
            var page = await reader.WalkEntriesAsync(
                ns.Id, "approved", null, cursor, 2, CancellationToken.None);
            if (page.Count == 0)
            {
                break;
            }

            walked.AddRange(page.Select(r => r.Key));
            if (page.Count < 2)
            {
                break;
            }

            var last = page[^1];
            cursor = (last.NamespaceName, last.Key, last.Id);
        }

        walked.Should().Equal("a-approved", "c-approved", "e-approved");
    }

    private sealed class AllowAllEvaluator
        : CommunityOS.Authorization.Application.Interfaces.IAuthorizationEvaluator
    {
        public Task<AuthorizationDecision> EvaluateAsync(
            AuthorizationRequest request, CancellationToken ct = default) =>
            Task.FromResult(AuthorizationDecision.Allow(
                Guid.NewGuid().ToString("N"), [Guid.NewGuid().ToString("N")], DateTime.UtcNow));

        public Task<IReadOnlyList<AuthorizationDecision>> EvaluateBatchAsync(
            IReadOnlyList<AuthorizationRequest> requests, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<AuthorizationDecision>>([]);
    }
}
