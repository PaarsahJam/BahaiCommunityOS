using CommunityOS.Authorization.Domain.Aggregates;
using CommunityOS.Authorization.Domain.Enumerations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CommunityOS.Authorization.Infrastructure.Persistence.Configurations;

public sealed class DelegationConfiguration : IEntityTypeConfiguration<Delegation>
{
    public void Configure(EntityTypeBuilder<Delegation> builder)
    {
        builder.ToTable("delegations");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.DelegatorId).HasColumnName("delegator_id").IsRequired();
        builder.Property(x => x.DelegateId).HasColumnName("delegate_id").IsRequired();

        builder.OwnsOne(x => x.Scope, scope =>
        {
            scope.Property(s => s.Type)
                .HasConversion(type => type.Id, id => ScopeType.FromId(id))
                .HasColumnName("scope_type")
                .IsRequired();
            scope.Property(s => s.ScopeId).HasColumnName("scope_id");
            scope.Property(s => s.ResourceType).HasColumnName("scope_resource_type").HasMaxLength(128);
        });

        builder.PrimitiveCollection(x => x.Permissions).HasColumnName("permissions").HasField("_permissions");

        builder.Property(x => x.StartsOn).HasColumnName("starts_on").IsRequired();
        builder.Property(x => x.ExpiresOn).HasColumnName("expires_on").IsRequired();
        builder.Property(x => x.Reason).HasColumnName("reason").HasMaxLength(500);
        builder.Property(x => x.RevokedBy).HasColumnName("revoked_by");
        builder.Property(x => x.RevokedAt).HasColumnName("revoked_at");
        builder.Property(x => x.RevocationReason).HasColumnName("revocation_reason").HasMaxLength(500);

        builder.HasIndex(x => x.DelegateId);
        builder.HasIndex(x => x.DelegatorId);
    }
}
