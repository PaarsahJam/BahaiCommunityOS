using CommunityOS.Organization.Domain.Aggregates;
using CommunityOS.Organization.Domain.Enumerations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CommunityOS.Organization.Infrastructure.Persistence.Configurations;

public sealed class DelegationFactConfiguration : IEntityTypeConfiguration<DelegationFact>
{
    public void Configure(EntityTypeBuilder<DelegationFact> builder)
    {
        builder.ToTable("delegation_facts");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.DelegatorId).HasColumnName("delegator_id").IsRequired();
        builder.Property(x => x.DelegateId).HasColumnName("delegate_id").IsRequired();
        builder.Property(x => x.OrganizationUnitId).HasColumnName("organization_unit_id").IsRequired();
        builder.Property(x => x.DelegationType).HasColumnName("delegation_type").IsRequired().HasMaxLength(100);
        builder.Property(x => x.Status)
            .HasConversion(status => status.Id, id => DelegationFactStatus.FromId(id))
            .HasColumnName("status")
            .IsRequired();

        builder.OwnsOne(x => x.Period, period =>
        {
            period.Property(p => p.EffectiveFrom).HasColumnName("effective_from").IsRequired();
            period.Property(p => p.EffectiveUntil).HasColumnName("effective_until");
        });

        builder.Property(x => x.GrantedBy).HasColumnName("granted_by").IsRequired();
        builder.Property(x => x.GrantedOn).HasColumnName("granted_on").IsRequired();
        builder.Property(x => x.RevokedBy).HasColumnName("revoked_by");
        builder.Property(x => x.RevokedOn).HasColumnName("revoked_on");
        builder.Property(x => x.Reason).HasColumnName("reason").HasMaxLength(500);
        builder.Property(x => x.Version).HasColumnName("version").IsRequired();

        builder.HasIndex(x => x.DelegatorId);
        builder.HasIndex(x => x.DelegateId);
        builder.HasIndex(x => x.OrganizationUnitId);
    }
}