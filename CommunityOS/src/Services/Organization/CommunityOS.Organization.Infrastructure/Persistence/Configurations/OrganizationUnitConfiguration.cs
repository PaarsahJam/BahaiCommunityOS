using CommunityOS.Organization.Domain.Aggregates;
using CommunityOS.Organization.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CommunityOS.Organization.Infrastructure.Persistence.Configurations;

public sealed class OrganizationUnitConfiguration : IEntityTypeConfiguration<OrganizationUnit>
{
    public void Configure(EntityTypeBuilder<OrganizationUnit> builder)
    {
        builder.ToTable("organization_units");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.OrganizationId).HasColumnName("organization_id").IsRequired();
        builder.Property(x => x.Name).HasColumnName("name").IsRequired().HasMaxLength(200);
        builder.Property(x => x.UnitType).HasColumnName("unit_type").IsRequired().HasMaxLength(100);
        builder.Property(x => x.IsActive).HasColumnName("is_active").IsRequired();
        builder.Property(x => x.CreatedOn).HasColumnName("created_on").IsRequired();
        builder.Property(x => x.Version).HasColumnName("version").IsRequired();

        builder.HasIndex(x => x.OrganizationId);
        builder.HasIndex(x => new { x.OrganizationId, x.Name }).IsUnique();

        builder.OwnsMany(
            x => x.Parents,
            parent =>
            {
                parent.ToTable("organization_unit_parents");
                parent.WithOwner().HasForeignKey("unit_id");
                parent.HasKey(x => x.Id);

                parent.Property(x => x.ParentId).HasColumnName("parent_id");
                parent.HasIndex(x => x.ParentId);

                parent.OwnsOne(x => x.Period, period =>
                {
                    period.Property(p => p.EffectiveFrom).HasColumnName("effective_from").IsRequired();
                    period.Property(p => p.EffectiveUntil).HasColumnName("effective_until");
                });
            });
    }
}