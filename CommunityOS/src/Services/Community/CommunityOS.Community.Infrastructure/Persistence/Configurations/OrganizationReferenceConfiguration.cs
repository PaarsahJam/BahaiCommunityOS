using CommunityOS.Community.Domain.Aggregates;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CommunityOS.Community.Infrastructure.Persistence.Configurations;

internal sealed class OrganizationReferenceConfiguration : IEntityTypeConfiguration<OrganizationReference>
{
    public void Configure(EntityTypeBuilder<OrganizationReference> builder)
    {
        builder.ToTable("organization_references");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).ValueGeneratedNever();

        builder.Property(r => r.Name).HasColumnName("name").HasMaxLength(200).IsRequired();
        builder.Property(r => r.OrganizationType).HasColumnName("organization_type").HasMaxLength(100).IsRequired();
        builder.Property(r => r.JurisdictionType).HasColumnName("jurisdiction_type").HasMaxLength(100).IsRequired();
        builder.Property(r => r.JurisdictionScopeId).HasColumnName("jurisdiction_scope_id");
        builder.Property(r => r.LastSeenOn).HasColumnName("last_seen_on").IsRequired();
        builder.Ignore(r => r.DomainEvents);
    }
}
