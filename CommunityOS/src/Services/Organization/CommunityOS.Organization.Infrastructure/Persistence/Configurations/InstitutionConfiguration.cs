using CommunityOS.Organization.Domain.Aggregates;
using CommunityOS.Organization.Domain.Enumerations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CommunityOS.Organization.Infrastructure.Persistence.Configurations;

public sealed class InstitutionConfiguration : IEntityTypeConfiguration<Institution>
{
    public void Configure(EntityTypeBuilder<Institution> builder)
    {
        builder.ToTable("institutions");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Name).HasColumnName("name").IsRequired().HasMaxLength(200);
        builder.HasIndex(x => x.Name).IsUnique();

        builder.Property(x => x.InstitutionType).HasColumnName("institution_type").IsRequired().HasMaxLength(100);
        builder.Property(x => x.IsActive).HasColumnName("is_active").IsRequired();

        builder.OwnsOne(x => x.Jurisdiction, jurisdiction =>
        {
            jurisdiction.Property(j => j.Type)
                .HasConversion(type => type.Id, id => JurisdictionType.FromId(id))
                .HasColumnName("jurisdiction_type")
                .IsRequired();
            jurisdiction.Property(j => j.ScopeId).HasColumnName("jurisdiction_scope_id");
        });

        builder.Property(x => x.EstablishedOn).HasColumnName("established_on");
        builder.Property(x => x.CreatedOn).HasColumnName("created_on").IsRequired();
        builder.Property(x => x.Version).HasColumnName("version").IsRequired();
    }
}