using CommunityOS.Community.Domain.Aggregates;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CommunityOS.Community.Infrastructure.Persistence.Configurations;

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
