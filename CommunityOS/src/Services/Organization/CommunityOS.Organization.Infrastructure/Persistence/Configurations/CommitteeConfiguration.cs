using CommunityOS.Organization.Domain.Aggregates;
using CommunityOS.Organization.Domain.Enumerations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CommunityOS.Organization.Infrastructure.Persistence.Configurations;

public sealed class CommitteeConfiguration : IEntityTypeConfiguration<Committee>
{
    public void Configure(EntityTypeBuilder<Committee> builder)
    {
        builder.ToTable("committees");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Name).HasColumnName("name").IsRequired().HasMaxLength(200);
        builder.Property(x => x.CommitteeType).HasColumnName("committee_type").IsRequired().HasMaxLength(100);
        builder.Property(x => x.OrganizationId).HasColumnName("organization_id").IsRequired();
        builder.Property(x => x.OrganizationUnitId).HasColumnName("organization_unit_id");
        builder.Property(x => x.IsActive).HasColumnName("is_active").IsRequired();
        builder.Property(x => x.CreatedOn).HasColumnName("created_on").IsRequired();
        builder.Property(x => x.Version).HasColumnName("version").IsRequired();

        builder.OwnsOne(x => x.Jurisdiction, jurisdiction =>
        {
            jurisdiction.Property(j => j.Type)
                .HasConversion(type => type.Id, id => JurisdictionType.FromId(id))
                .HasColumnName("jurisdiction_type")
                .IsRequired();
            jurisdiction.Property(j => j.ScopeId).HasColumnName("jurisdiction_scope_id");
        });

        builder.HasIndex(x => x.OrganizationId);
        builder.HasIndex(x => x.OrganizationUnitId);

        builder.OwnsMany(
            x => x.Members,
            member =>
            {
                member.ToTable("committee_members");
                member.WithOwner().HasForeignKey("committee_id");
                member.HasKey(x => x.Id);
                member.Property(x => x.Id).ValueGeneratedNever();

                member.Property(x => x.PersonId).HasColumnName("person_id").IsRequired();
                member.Property(x => x.RoleCode).HasColumnName("role_code").IsRequired().HasMaxLength(100);

                member.OwnsOne(x => x.Period, period =>
                {
                    period.Property(p => p.EffectiveFrom).HasColumnName("effective_from").IsRequired();
                    period.Property(p => p.EffectiveUntil).HasColumnName("effective_until");
                });

                member.HasIndex(x => x.PersonId);
            });
    }
}