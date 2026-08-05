using CommunityOS.Identity.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CommunityOS.Identity.Infrastructure.Persistence.Configurations;

internal sealed class RoleConfiguration : IEntityTypeConfiguration<Role>
{
    public void Configure(EntityTypeBuilder<Role> builder)
    {
        builder.ToTable("roles");

        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).ValueGeneratedNever();
        builder.Property(r => r.Name).HasMaxLength(100).IsRequired();
        builder.Property(r => r.Description).HasMaxLength(500);
        builder.Property(r => r.IsSystemRole).IsRequired();

        builder.HasMany(r => r.Permissions)
            .WithMany()
            .UsingEntity(j => j.ToTable("role_permissions"));

        builder.Ignore(r => r.DomainEvents);
    }
}

internal sealed class PermissionConfiguration : IEntityTypeConfiguration<Permission>
{
    public void Configure(EntityTypeBuilder<Permission> builder)
    {
        builder.ToTable("permissions");

        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).ValueGeneratedNever();
        builder.Property(p => p.Code).HasMaxLength(100).IsRequired();
        builder.HasIndex(p => p.Code).IsUnique();
        builder.Property(p => p.Description).HasMaxLength(500).IsRequired();

        builder.Ignore(p => p.DomainEvents);
    }
}
