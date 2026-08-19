using CommunityOS.Records.Domain.Aggregates;
using CommunityOS.Records.Domain.Enumerations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CommunityOS.Records.Infrastructure.Persistence.Configurations;

internal sealed class RecordConfiguration : IEntityTypeConfiguration<Record>
{
    public void Configure(EntityTypeBuilder<Record> builder)
    {
        builder.ToTable("records");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).ValueGeneratedNever();

        builder.Property(r => r.Category).HasColumnName("category").HasMaxLength(100).IsRequired();
        builder.Property(r => r.OrganizationUnitId).HasColumnName("organization_unit_id");
        builder.Property(r => r.SubjectType).HasColumnName("subject_type").HasMaxLength(30).IsRequired();
        builder.Property(r => r.SubjectId).HasColumnName("subject_id").IsRequired();
        builder.Property(r => r.CurrentVersionId).HasColumnName("current_version_id");
        builder.Property(r => r.CreatedBy).HasColumnName("created_by").IsRequired();
        builder.Property(r => r.CreatedOn).HasColumnName("created_on").IsRequired();
        builder.Property(r => r.UpdatedBy).HasColumnName("updated_by").IsRequired();
        builder.Property(r => r.UpdatedOn).HasColumnName("updated_on").IsRequired();
        builder.Property(r => r.VerifiedBy).HasColumnName("verified_by");
        builder.Property(r => r.VerifiedOn).HasColumnName("verified_on");

        builder.Property(r => r.Status)
            .HasColumnName("status")
            .HasConversion(v => v.Id, id => RecordStatus.FromId(id))
            .IsRequired();

        builder.OwnsOne(r => r.Classification, c =>
        {
            c.ToTable("record_classification");
            c.WithOwner().HasForeignKey("record_id");
            c.Property(x => x.ClassificationCode).HasColumnName("classification_code").HasMaxLength(50);
            c.Property(x => x.IsSensitive).HasColumnName("is_sensitive").IsRequired();
            c.Property(x => x.RetentionScheduleCode).HasColumnName("retention_schedule_code").HasMaxLength(100);
            c.Property(x => x.RetentionExpiredOn).HasColumnName("retention_expired_on");
            c.Property(x => x.ClassifiedBy).HasColumnName("classified_by");
            c.Property(x => x.ClassifiedOn).HasColumnName("classified_on");
        });

        builder.OwnsMany(r => r.WorkingFields, f =>
        {
            f.ToTable("record_working_fields");
            f.WithOwner().HasForeignKey("record_id");
            f.HasKey("Id");
            f.Property(x => x.Id).ValueGeneratedNever();
            f.Property(x => x.FieldKey).HasColumnName("field_key").HasMaxLength(100).IsRequired();
            f.Property(x => x.FieldValue).HasColumnName("field_value").HasMaxLength(8000).IsRequired();
            f.Property(x => x.IsSensitive).HasColumnName("is_sensitive").IsRequired();
        });

        builder.OwnsMany(r => r.Versions, v =>
        {
            v.ToTable("record_versions");
            v.WithOwner().HasForeignKey("record_id");
            v.HasKey("Id");
            v.Property(x => x.Id).ValueGeneratedNever();
            v.Property(x => x.VersionNumber).HasColumnName("version_number").IsRequired();
            v.Property(x => x.SupersedesVersionNumber).HasColumnName("supersedes_version_number");
            v.Property(x => x.AppliedBy).HasColumnName("applied_by").IsRequired();
            v.Property(x => x.AppliedOn).HasColumnName("applied_on").IsRequired();
            v.Property(x => x.ChangeReason).HasColumnName("change_reason").HasMaxLength(2000);

            v.OwnsMany(x => x.Fields, f =>
            {
                f.ToTable("record_field_values");
                f.WithOwner().HasForeignKey("record_version_id");
                f.HasKey("Id");
                f.Property(x => x.Id).ValueGeneratedNever();
                f.Property(x => x.FieldKey).HasColumnName("field_key").HasMaxLength(100).IsRequired();
                f.Property(x => x.FieldValue).HasColumnName("field_value").HasMaxLength(8000).IsRequired();
                f.Property(x => x.IsSensitive).HasColumnName("is_sensitive").IsRequired();
            });

            v.HasIndex("record_id", "VersionNumber")
                .IsUnique()
                .HasDatabaseName("ix_record_versions_record_version_number");
        });

        builder.OwnsMany(r => r.Scopes, s =>
        {
            s.ToTable("record_scopes");
            s.WithOwner().HasForeignKey("record_id");
            s.HasKey("Id");
            s.Property(x => x.Id).ValueGeneratedNever();
            s.Property(x => x.OrganizationUnitId).HasColumnName("organization_unit_id").IsRequired();

            s.HasIndex("record_id", "OrganizationUnitId")
                .IsUnique()
                .HasDatabaseName("ix_record_scopes_record_organization_unit");
        });

        builder.OwnsMany(r => r.Evidence, e =>
        {
            e.ToTable("record_evidence_references");
            e.WithOwner().HasForeignKey("record_id");
            e.HasKey("Id");
            e.Property(x => x.Id).ValueGeneratedNever();
            e.Property(x => x.DocumentId).HasColumnName("document_id").IsRequired();
            e.Property(x => x.VersionNumber).HasColumnName("version_number").IsRequired();
            e.Property(x => x.ReferenceType).HasColumnName("reference_type").HasMaxLength(50).IsRequired();
            e.Property(x => x.AttachedBy).HasColumnName("attached_by").IsRequired();
            e.Property(x => x.AttachedOn).HasColumnName("attached_on").IsRequired();
            e.Property(x => x.DocumentDeactivatedOn).HasColumnName("document_deactivated_on");
            e.Property(x => x.DocumentRestoredOn).HasColumnName("document_restored_on");

            e.HasIndex("record_id", "DocumentId", "VersionNumber", "ReferenceType")
                .IsUnique()
                .HasDatabaseName("ix_record_evidence_document_version_type");
        });

        builder.OwnsMany(r => r.Holds, h =>
        {
            h.ToTable("record_holds");
            h.WithOwner().HasForeignKey("record_id");
            h.HasKey("Id");
            h.Property(x => x.Id).ValueGeneratedNever();
            h.Property(x => x.HoldType).HasColumnName("hold_type").HasMaxLength(30).IsRequired();
            h.Property(x => x.Reason).HasColumnName("reason").HasMaxLength(2000).IsRequired();
            h.Property(x => x.PlacedBy).HasColumnName("placed_by").IsRequired();
            h.Property(x => x.PlacedOn).HasColumnName("placed_on").IsRequired();
            h.Property(x => x.ReleasedBy).HasColumnName("released_by");
            h.Property(x => x.ReleasedOn).HasColumnName("released_on");

            h.OwnsMany(x => x.DocumentReferences, d =>
            {
                d.ToTable("record_hold_document_references");
                d.WithOwner().HasForeignKey("record_hold_id");
                d.HasKey("Id");
                d.Property(x => x.Id).ValueGeneratedNever();
                d.Property(x => x.DocumentId).HasColumnName("document_id").IsRequired();
                d.Property(x => x.VersionNumber).HasColumnName("version_number");
            });
        });

        builder.HasIndex(r => r.OrganizationUnitId)
            .HasDatabaseName("ix_records_organization_unit_id");
        builder.HasIndex(r => r.CurrentVersionId)
            .HasDatabaseName("ix_records_current_version_id");
        builder.Ignore(r => r.DomainEvents);
    }
}

internal sealed class RecordCategoryConfiguration : IEntityTypeConfiguration<RecordCategory>
{
    public void Configure(EntityTypeBuilder<RecordCategory> builder)
    {
        builder.ToTable("record_categories");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).ValueGeneratedNever();

        builder.Property(c => c.Code).HasColumnName("code").HasMaxLength(100).IsRequired();
        builder.Property(c => c.DisplayName).HasColumnName("display_name").HasMaxLength(200).IsRequired();
        builder.Property(c => c.Description).HasColumnName("description").HasMaxLength(500);
        builder.Property(c => c.IsRetired).HasColumnName("is_retired").IsRequired();
        builder.Property(c => c.CreatedBy).HasColumnName("created_by").IsRequired();
        builder.Property(c => c.CreatedOn).HasColumnName("created_on").IsRequired();
        builder.Property(c => c.UpdatedBy).HasColumnName("updated_by");
        builder.Property(c => c.UpdatedOn).HasColumnName("updated_on");

        builder.HasIndex(c => c.Code).IsUnique().HasDatabaseName("ix_record_categories_code");
        builder.Ignore(c => c.DomainEvents);
    }
}

internal sealed class RetentionScheduleConfiguration : IEntityTypeConfiguration<RetentionSchedule>
{
    public void Configure(EntityTypeBuilder<RetentionSchedule> builder)
    {
        builder.ToTable("retention_schedules");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).ValueGeneratedNever();

        builder.Property(s => s.Code).HasColumnName("code").HasMaxLength(100).IsRequired();
        builder.Property(s => s.DisplayName).HasColumnName("display_name").HasMaxLength(200).IsRequired();
        builder.Property(s => s.Description).HasColumnName("description").HasMaxLength(500);
        builder.Property(s => s.IsRetired).HasColumnName("is_retired").IsRequired();
        builder.Property(s => s.CreatedBy).HasColumnName("created_by").IsRequired();
        builder.Property(s => s.CreatedOn).HasColumnName("created_on").IsRequired();
        builder.Property(s => s.UpdatedBy).HasColumnName("updated_by");
        builder.Property(s => s.UpdatedOn).HasColumnName("updated_on");

        builder.OwnsMany(s => s.Rules, r =>
        {
            r.ToTable("retention_rules");
            r.WithOwner().HasForeignKey("retention_schedule_id");
            r.HasKey("Id");
            r.Property(x => x.Id).ValueGeneratedNever();
            r.Property(x => x.Category).HasColumnName("category").HasMaxLength(100).IsRequired();
            r.Property(x => x.StartTrigger).HasColumnName("start_trigger").HasMaxLength(30).IsRequired();
            r.Property(x => x.Period).HasColumnName("period").HasMaxLength(30).IsRequired();
            r.Property(x => x.Disposition).HasColumnName("disposition").HasMaxLength(30).IsRequired();
            r.Property(x => x.Note).HasColumnName("note").HasMaxLength(500);
            r.Property(x => x.MaximumPeriod).HasColumnName("maximum_period").HasMaxLength(30);
        });

        builder.HasIndex(s => s.Code).IsUnique().HasDatabaseName("ix_retention_schedules_code");
        builder.Ignore(s => s.DomainEvents);
    }
}

internal sealed class RecordsOrganizationUnitReferenceConfiguration : IEntityTypeConfiguration<OrganizationUnitReference>
{
    public void Configure(EntityTypeBuilder<OrganizationUnitReference> builder)
    {
        builder.ToTable("organization_unit_references");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).ValueGeneratedNever();

        builder.Property(r => r.OrganizationUnitId).HasColumnName("organization_unit_id").IsRequired();
        builder.Property(r => r.CreatedOn).HasColumnName("created_on").IsRequired();
        builder.Ignore(r => r.DomainEvents);

        builder.HasIndex(r => r.OrganizationUnitId).IsUnique()
            .HasDatabaseName("ix_records_organization_unit_references_unit_id");
    }
}