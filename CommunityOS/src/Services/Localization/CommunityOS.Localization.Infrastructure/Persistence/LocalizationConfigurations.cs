using CommunityOS.Localization.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CommunityOS.Localization.Infrastructure.Persistence;

public sealed class LocaleConfiguration : IEntityTypeConfiguration<Locale>
{
    public void Configure(EntityTypeBuilder<Locale> builder)
    {
        builder.ToTable("locales");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.Code).HasColumnName("code").HasMaxLength(35).IsRequired();
        builder.Property(x => x.DisplayName).HasColumnName("display_name").HasMaxLength(200);
        builder.Property(x => x.Status)
            .HasColumnName("status").HasMaxLength(20).IsRequired()
            .HasConversion(
                value => value.ToString().ToLowerInvariant(),
                value => System.Enum.Parse<LocaleStatus>(value, ignoreCase: true));
        builder.Property(x => x.IsDefault).HasColumnName("is_default").IsRequired();
        builder.Property(x => x.CreatedBy).HasColumnName("created_by").IsRequired();
        builder.Property(x => x.CreatedOn).HasColumnName("created_on").IsRequired();
        builder.Property(x => x.UpdatedOn).HasColumnName("updated_on").IsRequired();

        builder.HasIndex(x => x.Code).IsUnique().HasDatabaseName("ux_locales_code");

        // Ratified seed policy (ADR-029 decision 3): only the default locale
        // 'en' exists and is active at implementation time; fa/ar remain
        // activation candidates and are deliberately NOT seeded.
        builder.HasData(Locale.CreateDefaultSeed(
            SeedIds.DefaultLocaleId, "en", "English",
            SeedIds.SystemActorId, SeedIds.SeedTimestamp));
    }
}

public sealed class ResourceNamespaceConfiguration : IEntityTypeConfiguration<ResourceNamespace>
{
    public void Configure(EntityTypeBuilder<ResourceNamespace> builder)
    {
        builder.ToTable("namespaces");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.Name).HasColumnName("name").HasMaxLength(100).IsRequired();
        builder.Property(x => x.Description).HasColumnName("description").HasMaxLength(500);
        builder.Property(x => x.CreatedBy).HasColumnName("created_by").IsRequired();
        builder.Property(x => x.CreatedOn).HasColumnName("created_on").IsRequired();

        builder.HasIndex(x => x.Name).IsUnique().HasDatabaseName("ux_namespaces_name");
    }
}

public sealed class ResourceEntryConfiguration : IEntityTypeConfiguration<ResourceEntry>
{
    public void Configure(EntityTypeBuilder<ResourceEntry> builder)
    {
        builder.ToTable("resource_entries");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.NamespaceId).HasColumnName("namespace_id").IsRequired();
        builder.Property(x => x.Key).HasColumnName("key").HasMaxLength(200).IsRequired();
        builder.Property(x => x.IsDeprecated).HasColumnName("is_deprecated").IsRequired();
        builder.Property(x => x.DeprecatedOn).HasColumnName("deprecated_on");
        builder.Property(x => x.Revision).HasColumnName("revision").IsConcurrencyToken();
        builder.Property(x => x.CreatedOn).HasColumnName("created_on").IsRequired();
        builder.Property(x => x.UpdatedOn).HasColumnName("updated_on").IsRequired();

        // Approved keys are unique per namespace and immutable (ADR-029
        // decision 4); the constraint is the storage-level backstop.
        builder.HasIndex(x => new { x.NamespaceId, x.Key })
            .IsUnique()
            .HasDatabaseName("ux_resource_entries_namespace_key");

        builder.Navigation(x => x.Revisions).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

public sealed class ResourceRevisionConfiguration : IEntityTypeConfiguration<ResourceRevision>
{
    public void Configure(EntityTypeBuilder<ResourceRevision> builder)
    {
        builder.ToTable("resource_revisions");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.EntryId).HasColumnName("entry_id").IsRequired();
        builder.Property(x => x.CultureCode).HasColumnName("culture_code").HasMaxLength(35).IsRequired();
        builder.Property(x => x.Value).HasColumnName("value").HasMaxLength(2000).IsRequired();
        builder.Property(x => x.State)
            .HasColumnName("state").HasMaxLength(20).IsRequired()
            .HasConversion(
                value => value.ToString().ToLowerInvariant(),
                value => System.Enum.Parse<ReviewState>(value, ignoreCase: true));
        builder.Property(x => x.Provenance).HasColumnName("provenance").HasMaxLength(100).IsRequired();
        builder.Property(x => x.ProposedBy).HasColumnName("proposed_by").IsRequired();
        builder.Property(x => x.ProposedOn).HasColumnName("proposed_on").IsRequired();
        builder.Property(x => x.ReviewedBy).HasColumnName("reviewed_by");
        builder.Property(x => x.ReviewedOn).HasColumnName("reviewed_on");

        builder.HasOne<ResourceEntry>()
            .WithMany(e => e.Revisions)
            .HasForeignKey(x => x.EntryId)
            .OnDelete(DeleteBehavior.Cascade);

        // At most one approved revision per (entry, culture): a new approval
        // supersedes the previous one in the same save (ADR-029 decision 4).
        builder.HasIndex(x => new { x.EntryId, x.CultureCode, x.State })
            .IsUnique()
            .HasDatabaseName("ux_resource_revisions_entry_culture_approved")
            .HasFilter("state = 'approved'");

        builder.HasIndex(x => new { x.State, x.ProposedOn })
            .HasDatabaseName("ix_resource_revisions_state_proposed_on");
    }
}

public sealed class EntityTranslationConfiguration : IEntityTypeConfiguration<EntityTranslation>
{
    public void Configure(EntityTypeBuilder<EntityTranslation> builder)
    {
        builder.ToTable("entity_translations");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.SourceContext).HasColumnName("source_context").HasMaxLength(50).IsRequired();
        builder.Property(x => x.EntityType).HasColumnName("entity_type").HasMaxLength(100).IsRequired();
        builder.Property(x => x.EntityId).HasColumnName("entity_id").IsRequired();
        builder.Property(x => x.Field).HasColumnName("field").HasMaxLength(100).IsRequired();
        builder.Property(x => x.CultureCode).HasColumnName("culture_code").HasMaxLength(35).IsRequired();
        builder.Property(x => x.IsDeprecated).HasColumnName("is_deprecated").IsRequired();
        builder.Property(x => x.DeprecatedOn).HasColumnName("deprecated_on");
        builder.Property(x => x.Revision).HasColumnName("revision").IsConcurrencyToken();
        builder.Property(x => x.CreatedOn).HasColumnName("created_on").IsRequired();
        builder.Property(x => x.UpdatedOn).HasColumnName("updated_on").IsRequired();

        // The by-reference identity tuple (ADR-029 decision 7).
        builder.HasIndex(x => new
            {
                x.SourceContext, x.EntityType, x.EntityId, x.Field, x.CultureCode
            })
            .IsUnique()
            .HasDatabaseName("ux_entity_translations_tuple");

        builder.Navigation(x => x.Revisions).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

public sealed class EntityTranslationRevisionConfiguration : IEntityTypeConfiguration<EntityTranslationRevision>
{
    public void Configure(EntityTypeBuilder<EntityTranslationRevision> builder)
    {
        builder.ToTable("entity_translation_revisions");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.TranslationId).HasColumnName("translation_id").IsRequired();
        builder.Property(x => x.CultureCode).HasColumnName("culture_code").HasMaxLength(35).IsRequired();
        builder.Property(x => x.Value).HasColumnName("value").HasMaxLength(2000).IsRequired();
        builder.Property(x => x.State)
            .HasColumnName("state").HasMaxLength(20).IsRequired()
            .HasConversion(
                value => value.ToString().ToLowerInvariant(),
                value => System.Enum.Parse<ReviewState>(value, ignoreCase: true));
        builder.Property(x => x.Provenance).HasColumnName("provenance").HasMaxLength(100).IsRequired();
        builder.Property(x => x.ProposedBy).HasColumnName("proposed_by").IsRequired();
        builder.Property(x => x.ProposedOn).HasColumnName("proposed_on").IsRequired();
        builder.Property(x => x.ReviewedBy).HasColumnName("reviewed_by");
        builder.Property(x => x.ReviewedOn).HasColumnName("reviewed_on");

        builder.HasOne<EntityTranslation>()
            .WithMany(t => t.Revisions)
            .HasForeignKey(x => x.TranslationId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => new { x.TranslationId, x.State })
            .IsUnique()
            .HasDatabaseName("ux_entity_translation_revisions_translation_state_approved")
            .HasFilter("state = 'approved'");
    }
}

public sealed class TranslationSuggestionConfiguration : IEntityTypeConfiguration<TranslationSuggestion>
{
    public void Configure(EntityTypeBuilder<TranslationSuggestion> builder)
    {
        builder.ToTable("translation_suggestions");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.TargetKind).HasColumnName("target_kind").HasMaxLength(30).IsRequired();
        builder.Property(x => x.TargetId).HasColumnName("target_id").IsRequired();
        builder.Property(x => x.TargetCultureCode).HasColumnName("target_culture_code").HasMaxLength(35).IsRequired();
        builder.Property(x => x.SuggestedValue).HasColumnName("suggested_value").HasMaxLength(2000).IsRequired();
        builder.Property(x => x.Provenance).HasColumnName("provenance").HasMaxLength(100).IsRequired();
        builder.Property(x => x.Status)
            .HasColumnName("status").HasMaxLength(30).IsRequired()
            .HasConversion(
                value => value.ToString().ToLowerInvariant(),
                value => System.Enum.Parse<SuggestionStatus>(value, ignoreCase: true));
        builder.Property(x => x.DecidedBy).HasColumnName("decided_by");
        builder.Property(x => x.DecidedOn).HasColumnName("decided_on");
        builder.Property(x => x.CreatedOn).HasColumnName("created_on").IsRequired();

        builder.HasIndex(x => new { x.Status, x.CreatedOn })
            .HasDatabaseName("ix_translation_suggestions_status_created_on");
    }
}

public sealed class LocalizationCatalogStateConfiguration : IEntityTypeConfiguration<LocalizationCatalogState>
{
    public void Configure(EntityTypeBuilder<LocalizationCatalogState> builder)
    {
        builder.ToTable("catalog_state");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.Version).HasColumnName("version").IsRequired();
        builder.Property(x => x.UpdatedOn).HasColumnName("updated_on").IsRequired();

        builder.HasData(new LocalizationCatalogState
        {
            Id = SeedIds.CatalogStateRowId,
            Version = 0L,
            UpdatedOn = SeedIds.SeedTimestamp
        });
    }
}

internal static class SeedIds
{
    /// <summary>Deterministic ids for model-seeded rows.</summary>
    public static readonly Guid DefaultLocaleId =
        new("3f2a9c1e-6b7d-4c8e-9a0f-1d2e3f4a5b6c");

    public static readonly Guid CatalogStateRowId =
        new("a1b2c3d4-e5f6-47a8-9b0c-1d2e3f4a5b6d");

    /// <summary>Synthetic actor for model-seeded rows (no real subject).</summary>
    public static readonly Guid SystemActorId =
        new("00000000-0000-0000-0000-000000000001");

    public static readonly DateTime SeedTimestamp =
        new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
}
