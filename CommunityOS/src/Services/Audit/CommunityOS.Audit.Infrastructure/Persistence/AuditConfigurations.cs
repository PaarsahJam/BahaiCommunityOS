using CommunityOS.Audit.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CommunityOS.Audit.Infrastructure.Persistence;

public sealed class AuditEntryConfiguration : IEntityTypeConfiguration<AuditEntry>
{
    public void Configure(EntityTypeBuilder<AuditEntry> builder)
    {
        builder.ToTable("audit_entries");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.OccurredOn).HasColumnName("occurred_on");
        builder.HasIndex(x => x.OccurredOn).HasDatabaseName("ix_audit_entries_occurred_on");
        builder.Property(x => x.IngestedOn).HasColumnName("ingested_on");
        builder.Property(x => x.SourceService).HasColumnName("source_service").HasMaxLength(50).IsRequired();
        builder.Property(x => x.SourceEventType).HasColumnName("source_event_type").HasMaxLength(100).IsRequired();
        builder.Property(x => x.Action).HasColumnName("action").HasMaxLength(100).IsRequired();

        builder.Property(x => x.SourceEventHash).HasColumnName("source_event_hash").HasMaxLength(64).IsRequired();
        builder.HasIndex(x => x.SourceEventHash).IsUnique().HasDatabaseName("ux_audit_entries_source_event_hash");

        builder.Property(x => x.Outcome).HasColumnName("outcome").HasMaxLength(100);

        builder.Property(x => x.ResourceType).HasColumnName("resource_type").HasMaxLength(50).IsRequired();
        builder.Property(x => x.ResourceId).HasColumnName("resource_id");
        builder.HasIndex(x => new { x.ResourceType, x.ResourceId })
            .HasDatabaseName("ix_audit_entries_resource");

        builder.Property(x => x.SecondaryResourceId).HasColumnName("secondary_resource_id");

        builder.Property(x => x.SubjectId).HasColumnName("subject_id");
        builder.HasIndex(x => x.SubjectId).HasDatabaseName("ix_audit_entries_subject_id");

        builder.Property(x => x.ActorId).HasColumnName("actor_id");
        builder.HasIndex(x => x.ActorId).HasDatabaseName("ix_audit_entries_actor_id");

        builder.Property(x => x.OrganizationUnitId).HasColumnName("organization_unit_id");
        builder.HasIndex(x => x.OrganizationUnitId).HasDatabaseName("ix_audit_entries_organization_unit_id");

        builder.Property(x => x.Sensitivity)
            .HasColumnName("sensitivity")
            .HasMaxLength(20)
            .IsRequired()
            .HasConversion(
                value => value == AuditSensitivity.Sensitive ? "sensitive" : "normal",
                value => value == "sensitive" ? AuditSensitivity.Sensitive : AuditSensitivity.Normal);
        builder.HasIndex(x => x.Sensitivity).HasDatabaseName("ix_audit_entries_sensitivity");

        builder.Property(x => x.MetadataJson).HasColumnName("metadata").HasColumnType("jsonb");
        builder.Property(x => x.CorrelationId).HasColumnName("correlation_id");
        builder.Property(x => x.CausationId).HasColumnName("causation_id");

        builder.Property(x => x.RetentionClass).HasColumnName("retention_class").HasMaxLength(50).IsRequired();
        builder.Property(x => x.RetentionExpiresOn).HasColumnName("retention_expires_on");
        builder.HasIndex(x => new { x.RetentionClass, x.RetentionExpiresOn })
            .HasDatabaseName("ix_audit_entries_retention_expiry")
            .HasFilter("retention_expires_on IS NOT NULL");
    }
}

public sealed class AuditEntryHoldConfiguration : IEntityTypeConfiguration<AuditEntryHold>
{
    public void Configure(EntityTypeBuilder<AuditEntryHold> builder)
    {
        builder.ToTable("audit_entry_holds");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.EntryId).HasColumnName("entry_id").IsRequired();
        builder.Property(x => x.HoldType).HasColumnName("hold_type").HasMaxLength(20).IsRequired();
        builder.Property(x => x.PlacedBy).HasColumnName("placed_by").IsRequired();
        builder.Property(x => x.PlacedOn).HasColumnName("placed_on").IsRequired();
        builder.Property(x => x.ReasonCode).HasColumnName("reason_code").HasMaxLength(50).IsRequired();
        builder.Property(x => x.ReleasedBy).HasColumnName("released_by");
        builder.Property(x => x.ReleasedOn).HasColumnName("released_on");

        builder.HasOne<AuditEntry>()
            .WithMany()
            .HasForeignKey(x => x.EntryId)
            .OnDelete(DeleteBehavior.Cascade);

        // Active-hold lookups and the purge expiry predicate both filter on
        // released_on IS NULL; the partial index covers exactly that shape.
        builder.HasIndex(x => x.EntryId)
            .HasDatabaseName("ix_audit_entry_holds_active")
            .HasFilter("released_on IS NULL");
    }
}

public sealed class OrganizationUnitReferenceConfiguration : IEntityTypeConfiguration<OrganizationUnitReference>
{
    public void Configure(EntityTypeBuilder<OrganizationUnitReference> builder)
    {
        builder.ToTable("organization_unit_references");
        builder.HasKey(x => x.OrganizationUnitId);

        builder.Property(x => x.OrganizationUnitId).HasColumnName("organization_unit_id");
        builder.Property(x => x.ParentOrganizationUnitId).HasColumnName("parent_organization_unit_id");
        builder.Property(x => x.LastUpdatedOn).HasColumnName("last_updated_on");
    }
}
