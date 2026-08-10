using CommunityOS.Authorization.Domain.Aggregates;
using CommunityOS.Authorization.Domain.Enumerations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CommunityOS.Authorization.Infrastructure.Persistence.Configurations;

public sealed class RoleAssignmentConfiguration : IEntityTypeConfiguration<RoleAssignment>
{
    public void Configure(EntityTypeBuilder<RoleAssignment> builder)
    {
        builder.ToTable("role_assignments");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.SubjectId).HasColumnName("subject_id").IsRequired();
        builder.Property(x => x.RoleId).HasColumnName("role_id").IsRequired();
        builder.Property(x => x.RoleCode).HasColumnName("role_code").IsRequired().HasMaxLength(100);

        builder.OwnsOne(x => x.Scope, scope =>
        {
            scope.Property(s => s.Type)
                .HasConversion(type => type.Id, id => ScopeType.FromId(id))
                .HasColumnName("scope_type")
                .IsRequired();
            scope.Property(s => s.ScopeId).HasColumnName("scope_id");
            scope.Property(s => s.ResourceType).HasColumnName("scope_resource_type").HasMaxLength(128);
        });

        builder.Property(x => x.GrantedBy).HasColumnName("granted_by").IsRequired();
        builder.Property(x => x.GrantedAt).HasColumnName("granted_at").IsRequired();
        builder.Property(x => x.EffectiveFrom).HasColumnName("effective_from").IsRequired();
        builder.Property(x => x.EffectiveUntil).HasColumnName("effective_until");
        builder.Property(x => x.Reason).HasColumnName("reason").HasMaxLength(500);
        builder.Property(x => x.RevokedBy).HasColumnName("revoked_by");
        builder.Property(x => x.RevokedAt).HasColumnName("revoked_at");
        builder.Property(x => x.RevocationReason).HasColumnName("revocation_reason").HasMaxLength(500);

        builder.HasIndex(x => x.SubjectId);
        builder.HasIndex(x => new { x.RoleId, x.SubjectId });
    }
}
