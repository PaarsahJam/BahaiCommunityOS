using CommunityOS.Documents.Domain.Aggregates;
using CommunityOS.Documents.Domain.Enumerations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CommunityOS.Documents.Infrastructure.Persistence.Configurations;

internal sealed class DocumentConfiguration : IEntityTypeConfiguration<Document>
{
    public void Configure(EntityTypeBuilder<Document> builder)
    {
        builder.ToTable("documents");
        builder.HasKey(d => d.Id);
        builder.Property(d => d.Id).ValueGeneratedNever();

        builder.Property(d => d.Title).HasColumnName("title").HasMaxLength(300).IsRequired();
        builder.Property(d => d.Description).HasColumnName("description").HasMaxLength(2000);
        builder.Property(d => d.OrganizationUnitId).HasColumnName("organization_unit_id");
        builder.Property(d => d.OwnerType).HasColumnName("owner_type").HasMaxLength(30);
        builder.Property(d => d.OwnerId).HasColumnName("owner_id");
        builder.Property(d => d.CurrentVersionId).HasColumnName("current_version_id");
        builder.Property(d => d.CreatedBy).HasColumnName("created_by").IsRequired();
        builder.Property(d => d.CreatedOn).HasColumnName("created_on").IsRequired();
        builder.Property(d => d.UpdatedBy).HasColumnName("updated_by").IsRequired();
        builder.Property(d => d.UpdatedOn).HasColumnName("updated_on").IsRequired();

        builder.Property(d => d.Status)
            .HasColumnName("status")
            .HasConversion(v => v.Id, id => DocumentStatus.FromId(id))
            .IsRequired();

        builder.OwnsOne(d => d.Classification, c =>
        {
            c.ToTable("document_classification");
            c.WithOwner().HasForeignKey("document_id");
            c.Property(x => x.ClassificationCode).HasColumnName("classification_code").HasMaxLength(50);
            c.Property(x => x.IsSensitive).HasColumnName("is_sensitive").IsRequired();
            c.Property(x => x.RetentionCategory).HasColumnName("retention_category").HasMaxLength(100);
            c.Property(x => x.LegalHoldReference).HasColumnName("legal_hold_reference").HasMaxLength(100);
            c.Property(x => x.AdministrativeHoldReference).HasColumnName("administrative_hold_reference").HasMaxLength(100);
            c.Property(x => x.ClassifiedBy).HasColumnName("classified_by");
            c.Property(x => x.ClassifiedOn).HasColumnName("classified_on");
        });

        builder.OwnsMany(d => d.Versions, v =>
        {
            v.ToTable("document_versions");
            v.WithOwner().HasForeignKey("document_id");
            v.HasKey("Id");
            v.Property(x => x.Id).ValueGeneratedNever();
            v.Property(x => x.VersionNumber).HasColumnName("version_number").IsRequired();
            v.Property(x => x.ContentHash).HasColumnName("content_hash").HasMaxLength(64).IsRequired();
            v.Property(x => x.ObjectKey).HasColumnName("object_key").HasMaxLength(200).IsRequired();
            v.Property(x => x.MimeType).HasColumnName("mime_type").HasMaxLength(200).IsRequired();
            v.Property(x => x.SizeBytes).HasColumnName("size_bytes").IsRequired();
            v.Property(x => x.FileName).HasColumnName("file_name").HasMaxLength(255).IsRequired();
            v.Property(x => x.Source).HasColumnName("source").HasMaxLength(30).IsRequired();
            v.Property(x => x.UploadedBy).HasColumnName("uploaded_by").IsRequired();
            v.Property(x => x.UploadedOn).HasColumnName("uploaded_on").IsRequired();

            v.Property(x => x.ScanStatus)
                .HasColumnName("scan_status")
                .HasConversion(x => x.Id, id => ScanStatus.FromId(id))
                .IsRequired();

            // Immutability and ordering: one version per number per document.
            v.HasIndex("document_id", "VersionNumber")
                .IsUnique()
                .HasDatabaseName("ix_document_versions_document_version_number");
        });

        builder.OwnsMany(d => d.Scopes, s =>
        {
            s.ToTable("document_scopes");
            s.WithOwner().HasForeignKey("document_id");
            s.HasKey("Id");
            s.Property(x => x.Id).ValueGeneratedNever();
            s.Property(x => x.OrganizationUnitId).HasColumnName("organization_unit_id").IsRequired();

            // A unit can appear once in a document's scope set.
            s.HasIndex("document_id", "OrganizationUnitId")
                .IsUnique()
                .HasDatabaseName("ix_document_scopes_document_organization_unit");
        });

        builder.OwnsMany(d => d.References, r =>
        {
            r.ToTable("document_references");
            r.WithOwner().HasForeignKey("document_id");
            r.HasKey("Id");
            r.Property(x => x.Id).ValueGeneratedNever();
            r.Property(x => x.SourceContext).HasColumnName("source_context").HasMaxLength(100).IsRequired();
            r.Property(x => x.SourceEntityId).HasColumnName("source_entity_id").IsRequired();
            r.Property(x => x.ReferenceType).HasColumnName("reference_type").HasMaxLength(50).IsRequired();
            r.Property(x => x.CreatedBy).HasColumnName("created_by").IsRequired();
            r.Property(x => x.CreatedOn).HasColumnName("created_on").IsRequired();

            r.HasIndex("document_id", "SourceContext", "SourceEntityId", "ReferenceType")
                .IsUnique()
                .HasDatabaseName("ix_document_references_context_entity_type");
        });

        builder.HasIndex(d => d.OrganizationUnitId);
        builder.Ignore(d => d.DomainEvents);
    }
}

internal sealed class OrganizationUnitReferenceConfiguration : IEntityTypeConfiguration<OrganizationUnitReference>
{
    public void Configure(EntityTypeBuilder<OrganizationUnitReference> builder)
    {
        builder.ToTable("organization_unit_references");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).ValueGeneratedNever();

        builder.Property(r => r.OrganizationId).HasColumnName("organization_id").IsRequired();
        builder.Property(r => r.Name).HasColumnName("name").HasMaxLength(200).IsRequired();
        builder.Property(r => r.UnitType).HasColumnName("unit_type").HasMaxLength(100).IsRequired();
        builder.Property(r => r.ParentId).HasColumnName("parent_id");
        builder.Property(r => r.LastSeenOn).HasColumnName("last_seen_on").IsRequired();
        builder.Ignore(r => r.DomainEvents);

        builder.HasIndex(r => r.OrganizationId).HasDatabaseName("ix_organization_unit_references_organization_id");
    }
}