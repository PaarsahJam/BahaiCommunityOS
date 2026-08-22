using CommunityOS.Search.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CommunityOS.Search.Infrastructure.Persistence;

public sealed class SearchDocumentConfiguration : IEntityTypeConfiguration<SearchDocument>
{
    public void Configure(EntityTypeBuilder<SearchDocument> builder)
    {
        builder.ToTable("search_documents"); builder.HasKey(x => x.Id);
        builder.Property(x => x.SourceType).HasColumnName("source_type").HasMaxLength(50).IsRequired();
        builder.Property(x => x.SourceId).HasColumnName("source_id").IsRequired();
        builder.Property(x => x.OrganizationUnitId).HasColumnName("organization_unit_id");
        builder.Property(x => x.Status).HasMaxLength(50).IsRequired();
        builder.Property(x => x.IsSensitive).HasColumnName("is_sensitive");
        builder.Property(x => x.DisplayTitle).HasColumnName("display_title").HasMaxLength(500).IsRequired();
        builder.Property(x => x.TypeCode).HasColumnName("type_code").HasMaxLength(100).IsRequired();
        builder.Property(x => x.IndexedOn).HasColumnName("indexed_on"); builder.Property(x => x.CreatedOn).HasColumnName("created_on");
        builder.Property(x => x.SearchVector).HasColumnName("search_vector").HasColumnType("tsvector");
        builder.HasIndex(x => new { x.SourceType, x.SourceId }).IsUnique();
        builder.HasIndex(x => x.OrganizationUnitId);
        builder.HasIndex(x => x.SearchVector).HasMethod("gin");
        builder.OwnsMany(x => x.AdditionalScopes, scope => { scope.ToTable("search_document_scopes"); scope.WithOwner().HasForeignKey(x => x.SearchDocumentId); scope.HasKey(x => new { x.SearchDocumentId, x.OrganizationUnitId }); scope.Property(x => x.OrganizationUnitId).HasColumnName("organization_unit_id"); });
    }
}

public sealed class OrganizationUnitReferenceConfiguration : IEntityTypeConfiguration<OrganizationUnitReference>
{
    public void Configure(EntityTypeBuilder<OrganizationUnitReference> builder)
    { builder.ToTable("organization_unit_references"); builder.HasKey(x => x.OrganizationUnitId); builder.Property(x => x.OrganizationUnitId).HasColumnName("organization_unit_id"); builder.Property(x => x.ParentOrganizationUnitId).HasColumnName("parent_organization_unit_id"); builder.Property(x => x.LastUpdatedOn).HasColumnName("last_updated_on"); }
}
public sealed class SearchIndexLogConfiguration : IEntityTypeConfiguration<SearchIndexLog>
{
    public void Configure(EntityTypeBuilder<SearchIndexLog> builder)
    { builder.ToTable("search_index_log"); builder.HasKey(x => x.SourceType); builder.Property(x => x.SourceType).HasColumnName("source_type").HasMaxLength(50); builder.Property(x => x.LastEventOccurredOn).HasColumnName("last_event_occurred_on"); builder.Property(x => x.IndexedCount).HasColumnName("indexed_count"); }
}
