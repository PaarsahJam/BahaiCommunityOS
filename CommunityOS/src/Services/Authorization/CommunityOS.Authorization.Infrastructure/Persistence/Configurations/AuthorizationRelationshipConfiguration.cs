using CommunityOS.Authorization.Domain.Aggregates;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CommunityOS.Authorization.Infrastructure.Persistence.Configurations;

public sealed class AuthorizationRelationshipConfiguration : IEntityTypeConfiguration<AuthorizationRelationship>
{
    public void Configure(EntityTypeBuilder<AuthorizationRelationship> builder)
    {
        builder.ToTable("authorization_relationships");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.SubjectId).HasColumnName("subject_id").IsRequired();
        builder.Property(x => x.Relation).HasColumnName("relation").IsRequired().HasMaxLength(100);
        builder.Property(x => x.ObjectType).HasColumnName("object_type").IsRequired().HasMaxLength(128);
        builder.Property(x => x.ObjectId).HasColumnName("object_id").IsRequired();

        builder.PrimitiveCollection(x => x.Permissions).HasColumnName("permissions").HasField("_permissions");

        builder.HasIndex(x => new { x.SubjectId, x.Relation });
        builder.HasIndex(x => new { x.ObjectType, x.ObjectId });
    }
}
