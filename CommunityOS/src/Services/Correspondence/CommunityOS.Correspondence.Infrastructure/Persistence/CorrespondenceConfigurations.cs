using CommunityOS.Correspondence.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CommunityOS.Correspondence.Infrastructure.Persistence;

public sealed class LetterConfiguration : IEntityTypeConfiguration<Letter>
{
    public void Configure(EntityTypeBuilder<Letter> builder)
    {
        builder.ToTable("letters");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.OrganizationUnitId).HasColumnName("organization_unit_id").IsRequired();
        builder.Property(x => x.CategoryCode).HasColumnName("category_code").HasMaxLength(50).IsRequired();
        builder.Property(x => x.Subject).HasColumnName("subject").HasMaxLength(200).IsRequired();
        builder.Property(x => x.Body).HasColumnName("body").IsRequired();
        builder.Property(x => x.Sensitivity)
            .HasColumnName("sensitivity").HasMaxLength(20).IsRequired()
            .HasConversion(
                value => value == LetterSensitivity.Sensitive ? "sensitive" : "normal",
                value => value == "sensitive" ? LetterSensitivity.Sensitive : LetterSensitivity.Normal);
        builder.Property(x => x.Status)
            .HasColumnName("status").HasMaxLength(30).IsRequired()
            .HasConversion(
                value => value.ToString().ToLowerInvariant(),
                value => System.Enum.Parse<LetterStatus>(value, true));
        builder.Property(x => x.Revision).HasColumnName("revision").IsConcurrencyToken();

        builder.Property(x => x.LetterYear).HasColumnName("letter_year");
        builder.Property(x => x.LetterSequence).HasColumnName("letter_sequence");
        builder.Property(x => x.TemplateId).HasColumnName("template_id");
        builder.Property(x => x.TemplateCode).HasColumnName("template_code").HasMaxLength(50);
        builder.Property(x => x.RelatedLetterId).HasColumnName("related_letter_id");
        builder.Property(x => x.SubjectPersonId).HasColumnName("subject_person_id");

        builder.Property(x => x.CreatedBy).HasColumnName("created_by").IsRequired();
        builder.Property(x => x.CreatedOn).HasColumnName("created_on").IsRequired();
        builder.Property(x => x.UpdatedOn).HasColumnName("updated_on").IsRequired();

        builder.Property(x => x.SubmittedBy).HasColumnName("submitted_by");
        builder.Property(x => x.SubmittedOn).HasColumnName("submitted_on");
        builder.Property(x => x.MaterializedOn).HasColumnName("materialized_on");
        builder.Property(x => x.DispatchedOn).HasColumnName("dispatched_on");
        builder.Property(x => x.DeliveredOn).HasColumnName("delivered_on");
        builder.Property(x => x.DeliveryFailureReasonCode).HasColumnName("delivery_failure_reason_code").HasMaxLength(50);
        builder.Property(x => x.CancelledBy).HasColumnName("cancelled_by");
        builder.Property(x => x.CancelledOn).HasColumnName("cancelled_on");
        builder.Property(x => x.CancellationReasonCode).HasColumnName("cancellation_reason_code").HasMaxLength(50);

        builder.Property(x => x.RetentionClass).HasColumnName("retention_class").HasMaxLength(50).IsRequired();
        builder.Property(x => x.RetentionExpiresOn).HasColumnName("retention_expires_on");

        // Per-unit yearly reference uniqueness (ADR-028 decision 4).
        builder.HasIndex(x => new { x.OrganizationUnitId, x.LetterYear, x.LetterSequence })
            .IsUnique()
            .HasDatabaseName("ux_letters_unit_year_sequence")
            .HasFilter("letter_year IS NOT NULL AND letter_sequence IS NOT NULL");

        builder.HasIndex(x => x.OrganizationUnitId).HasDatabaseName("ix_letters_organization_unit_id");
        builder.HasIndex(x => new { x.Status, x.SubmittedOn }).HasDatabaseName("ix_letters_status_submitted_on");
        builder.HasIndex(x => x.CreatedOn).HasDatabaseName("ix_letters_created_on");
        builder.HasIndex(x => new { x.RetentionClass, x.RetentionExpiresOn })
            .HasDatabaseName("ix_letters_retention_expiry")
            .HasFilter("retention_expires_on IS NOT NULL");

        // Recipients/document links/attachments are aggregate children managed
        // through aggregate methods. Lifecycle history is deliberately NOT a
        // mapped navigation: the journal drains the aggregate's pending rows
        // into letter_status_history explicitly so the table keeps NO foreign
        // key to letters and purge tombstones survive the batch deletion
        // (ADR-028 decision 13).
        builder.Navigation(x => x.Recipients).UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.Navigation(x => x.DocumentLinks).UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.Navigation(x => x.Attachments).UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.Ignore(x => x.ReferenceNumber);
    }
}
public sealed class LetterRecipientConfiguration : IEntityTypeConfiguration<LetterRecipient>
{
    public void Configure(EntityTypeBuilder<LetterRecipient> builder)
    {
        builder.ToTable("letter_recipients");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.LetterId).HasColumnName("letter_id").IsRequired();
        builder.Property(x => x.Kind)
            .HasColumnName("kind").HasMaxLength(20).IsRequired()
            .HasConversion(
                value => value.ToString().ToLowerInvariant(),
                value => System.Enum.Parse<RecipientKind>(value, true));
        builder.Property(x => x.PersonId).HasColumnName("person_id");
        builder.Property(x => x.UnitId).HasColumnName("unit_id");
        builder.Property(x => x.DisplayLine).HasColumnName("display_line").HasMaxLength(500);
        builder.Property(x => x.AddedOn).HasColumnName("added_on").IsRequired();

        builder.HasOne<Letter>()
            .WithMany(l => l.Recipients)
            .HasForeignKey(x => x.LetterId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => x.PersonId).HasDatabaseName("ix_letter_recipients_person_id");
        builder.HasIndex(x => x.UnitId).HasDatabaseName("ix_letter_recipients_unit_id");
    }
}

public sealed class LetterStatusHistoryConfiguration : IEntityTypeConfiguration<LetterStatusHistory>
{
    public void Configure(EntityTypeBuilder<LetterStatusHistory> builder)
    {
        builder.ToTable("letter_status_history");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.LetterId).HasColumnName("letter_id");
        builder.Property(x => x.FromStatus)
            .HasColumnName("from_status").HasMaxLength(30).IsRequired()
            .HasConversion<string>();
        builder.Property(x => x.ToStatus)
            .HasColumnName("to_status").HasMaxLength(30).IsRequired()
            .HasConversion<string>();
        builder.Property(x => x.Cause)
            .HasColumnName("cause").HasMaxLength(20).IsRequired()
            .HasConversion<string>();
        builder.Property(x => x.ActorId).HasColumnName("actor_id");
        builder.Property(x => x.ReasonCode).HasColumnName("reason_code").HasMaxLength(50);
        builder.Property(x => x.OccurredOn).HasColumnName("occurred_on").IsRequired();

        // Deliberately NO foreign key to letters: history outlives purged
        // letters so purge tombstones (cause = PurgeMarker) persist after the
        // batch deletion (ADR-028 decision 13). There is no ordinary
        // application path that rewrites or deletes these rows; the table is
        // additionally protected by the purge-guard trigger.
        builder.HasIndex(x => x.LetterId).HasDatabaseName("ix_letter_status_history_letter_id");
        builder.HasIndex(x => x.OccurredOn).HasDatabaseName("ix_letter_status_history_occurred_on");
    }
}

public sealed class LetterDocumentLinkConfiguration : IEntityTypeConfiguration<LetterDocumentLink>
{
    public void Configure(EntityTypeBuilder<LetterDocumentLink> builder)
    {
        builder.ToTable("letter_documents");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.LetterId).HasColumnName("letter_id").IsRequired();
        builder.Property(x => x.DocumentId).HasColumnName("document_id").IsRequired();
        builder.Property(x => x.VersionNumber).HasColumnName("version_number").IsRequired();
        builder.Property(x => x.ContentHash).HasColumnName("content_hash").HasMaxLength(64).IsRequired();
        builder.Property(x => x.MaterializedOn).HasColumnName("materialized_on").IsRequired();

        builder.HasOne<Letter>()
            .WithMany(l => l.DocumentLinks)
            .HasForeignKey(x => x.LetterId)
            .OnDelete(DeleteBehavior.Cascade);

        // One materialization link per document (idempotent correlation).
        builder.HasIndex(x => x.DocumentId).IsUnique().HasDatabaseName("ux_letter_documents_document_id");
    }
}

public sealed class LetterAttachmentConfiguration : IEntityTypeConfiguration<LetterAttachment>
{
    public void Configure(EntityTypeBuilder<LetterAttachment> builder)
    {
        builder.ToTable("letter_attachments");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.LetterId).HasColumnName("letter_id").IsRequired();
        builder.Property(x => x.DocumentId).HasColumnName("document_id").IsRequired();
        builder.Property(x => x.ReferenceType).HasColumnName("reference_type").HasMaxLength(50).IsRequired();
        builder.Property(x => x.AddedBy).HasColumnName("added_by").IsRequired();
        builder.Property(x => x.AddedOn).HasColumnName("added_on").IsRequired();

        builder.HasOne<Letter>()
            .WithMany(l => l.Attachments)
            .HasForeignKey(x => x.LetterId)
            .OnDelete(DeleteBehavior.Cascade);

        // One attachment reference per document per letter.
        builder.HasIndex(x => new { x.LetterId, x.DocumentId })
            .IsUnique()
            .HasDatabaseName("ux_letter_attachments_letter_document");
    }
}

public sealed class LetterDeliveryRecordConfiguration : IEntityTypeConfiguration<LetterDeliveryRecord>
{
    public void Configure(EntityTypeBuilder<LetterDeliveryRecord> builder)
    {
        builder.ToTable("letter_delivery_records");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.LetterId).HasColumnName("letter_id").IsRequired();
        builder.Property(x => x.MethodCode).HasColumnName("method_code").HasMaxLength(50).IsRequired();
        builder.Property(x => x.Outcome)
            .HasColumnName("outcome").HasMaxLength(20).IsRequired()
            .HasConversion<string>();
        builder.Property(x => x.ReasonCode).HasColumnName("reason_code").HasMaxLength(50);
        builder.Property(x => x.ActorId).HasColumnName("actor_id").IsRequired();
        builder.Property(x => x.OccurredOn).HasColumnName("occurred_on").IsRequired();

        builder.HasOne<Letter>()
            .WithMany(l => l.DeliveryRecordsInternal)
            .HasForeignKey(x => x.LetterId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => x.LetterId).HasDatabaseName("ix_letter_delivery_records_letter_id");
    }
}

public sealed class TemplateConfiguration : IEntityTypeConfiguration<Template>
{
    public void Configure(EntityTypeBuilder<Template> builder)
    {
        builder.ToTable("templates");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.Code).HasColumnName("code").HasMaxLength(50).IsRequired();
        builder.HasIndex(x => x.Code).IsUnique().HasDatabaseName("ux_templates_code");
        builder.Property(x => x.Title).HasColumnName("title").HasMaxLength(200).IsRequired();
        builder.Property(x => x.SubjectTemplate).HasColumnName("subject_template").HasMaxLength(200).IsRequired();
        builder.Property(x => x.BodyTemplate).HasColumnName("body_template").IsRequired();
        builder.Property(x => x.CategoryCode).HasColumnName("category_code").HasMaxLength(50).IsRequired();
        builder.Property(x => x.IsActive).HasColumnName("is_active").IsRequired();
        builder.Property(x => x.CreatedBy).HasColumnName("created_by").IsRequired();
        builder.Property(x => x.CreatedOn).HasColumnName("created_on").IsRequired();
        builder.Property(x => x.UpdatedOn).HasColumnName("updated_on").IsRequired();
    }
}

public sealed class LetterHoldConfiguration : IEntityTypeConfiguration<LetterHold>
{
    public void Configure(EntityTypeBuilder<LetterHold> builder)
    {
        builder.ToTable("letter_holds");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.LetterId).HasColumnName("letter_id").IsRequired();
        builder.Property(x => x.HoldType).HasColumnName("hold_type").HasMaxLength(20).IsRequired();
        builder.Property(x => x.PlacedBy).HasColumnName("placed_by").IsRequired();
        builder.Property(x => x.PlacedOn).HasColumnName("placed_on").IsRequired();
        builder.Property(x => x.ReasonCode).HasColumnName("reason_code").HasMaxLength(50).IsRequired();
        builder.Property(x => x.ReleasedBy).HasColumnName("released_by");
        builder.Property(x => x.ReleasedOn).HasColumnName("released_on");

        builder.HasOne<Letter>()
            .WithMany()
            .HasForeignKey(x => x.LetterId)
            .OnDelete(DeleteBehavior.Cascade);

        // Active-hold lookups and the purge expiry predicate both filter on
        // released_on IS NULL; the partial index covers exactly that shape.
        builder.HasIndex(x => x.LetterId)
            .HasDatabaseName("ix_letter_holds_active")
            .HasFilter("released_on IS NULL");
    }
}

public sealed class ExportActivityConfiguration : IEntityTypeConfiguration<ExportActivity>
{
    public void Configure(EntityTypeBuilder<ExportActivity> builder)
    {
        builder.ToTable("export_activity");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.RequestedBy).HasColumnName("requested_by").IsRequired();
        builder.Property(x => x.Format).HasColumnName("format").HasMaxLength(10).IsRequired();
        builder.Property(x => x.FilterSummary).HasColumnName("filter_summary").HasMaxLength(200).IsRequired();
        builder.Property(x => x.RowCount).HasColumnName("row_count").IsRequired();
        builder.Property(x => x.IncludedSensitive).HasColumnName("included_sensitive").IsRequired();
        builder.Property(x => x.RequestedOn).HasColumnName("requested_on").IsRequired();

        builder.HasIndex(x => x.RequestedOn).HasDatabaseName("ix_export_activity_requested_on");
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
