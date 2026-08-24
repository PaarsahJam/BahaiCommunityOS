using CommunityOS.Localization.Domain;
using MassTransit;
using Microsoft.EntityFrameworkCore;

namespace CommunityOS.Localization.Infrastructure.Persistence;

/// <summary>
/// EF Core context for the Localization service (schema <c>localization</c>,
/// database <c>communityos_localization</c>). Born with the transactional
/// outbox: it models the MassTransit outbox entities so the ratified
/// <c>LocalizationCatalogChanged</c>-forwarding publishes commit atomically
/// with catalog changes (ADR-015, ADR-029 decisions 14/16). The inbox entity
/// is modeled but inert — Localization has zero consumers at the ratified
/// gate.
/// </summary>
public sealed class LocalizationDbContext(DbContextOptions<LocalizationDbContext> options)
    : DbContext(options)
{
    public DbSet<Locale> Locales => Set<Locale>();

    public DbSet<ResourceNamespace> Namespaces => Set<ResourceNamespace>();

    public DbSet<ResourceEntry> Entries => Set<ResourceEntry>();

    public DbSet<EntityTranslation> EntityTranslations => Set<EntityTranslation>();

    public DbSet<TranslationSuggestion> Suggestions => Set<TranslationSuggestion>();

    public DbSet<LocalizationCatalogState> CatalogState => Set<LocalizationCatalogState>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("localization");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(LocalizationDbContext).Assembly);

        // Transactional outbox entities (MassTransit EF Core outbox).
        modelBuilder.AddInboxStateEntity();
        modelBuilder.AddOutboxMessageEntity();
        modelBuilder.AddOutboxStateEntity();

        base.OnModelCreating(modelBuilder);
    }
}

/// <summary>
/// Singleton row holding the monotonic global catalog version used in bundle
/// ETags and <c>LocalizationCatalogChanged</c> payloads (ADR-029 decisions
/// 14/17). Bumped inside the same transaction as every publishing mutation.
/// </summary>
public sealed class LocalizationCatalogState
{
    public Guid Id { get; set; }

    public long Version { get; set; }

    public DateTime UpdatedOn { get; set; }
}
