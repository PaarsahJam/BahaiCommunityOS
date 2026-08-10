using CommunityOS.Community.Domain.Aggregates;
using CommunityOS.Community.Domain.Entities;
using CommunityOS.Community.Domain.ValueObjects;
using CommunityAggregate = CommunityOS.Community.Domain.Aggregates.Community;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CommunityOS.Community.Infrastructure.Persistence.Configurations;

internal sealed class CommunityConfiguration : IEntityTypeConfiguration<CommunityAggregate>
{
    public void Configure(EntityTypeBuilder<CommunityAggregate> builder)
    {
        builder.ToTable("communities");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).ValueGeneratedNever();

        builder.OwnsOne(c => c.Name, n =>
            n.Property(x => x.Value).HasColumnName("name").HasMaxLength(200).IsRequired());

        builder.OwnsOne(c => c.Area, a =>
        {
            a.Property(x => x.Country).HasColumnName("country").HasMaxLength(100).IsRequired();
            a.Property(x => x.Region).HasColumnName("region").HasMaxLength(100);
            a.Property(x => x.City).HasColumnName("city").HasMaxLength(100);
            a.Property(x => x.Latitude).HasColumnName("latitude");
            a.Property(x => x.Longitude).HasColumnName("longitude");
        });

        builder.Property(c => c.Level)
            .HasColumnName("hierarchy_level")
            .HasConversion(l => l.Id, id => HierarchyLevel.FromId(id))
            .IsRequired();

        builder.Property(c => c.ParentId).HasColumnName("parent_id");
        builder.Property(c => c.IsActive).HasColumnName("is_active").IsRequired();
        builder.Property(c => c.Version).HasColumnName("version").IsConcurrencyToken();

        builder.HasMany(c => c.LocalUnits)
            .WithOne()
            .HasForeignKey("community_id")
            .OnDelete(DeleteBehavior.Cascade);

        builder.Ignore(c => c.DomainEvents);
    }
}

internal sealed class LocalUnitConfiguration : IEntityTypeConfiguration<LocalUnit>
{
    public void Configure(EntityTypeBuilder<LocalUnit> builder)
    {
        builder.ToTable("local_units");
        builder.HasKey(u => u.Id);
        builder.Property(u => u.Id).ValueGeneratedNever();

        builder.OwnsOne(u => u.Name, n =>
            n.Property(x => x.Value).HasColumnName("name").HasMaxLength(200).IsRequired());

        builder.OwnsOne(u => u.Area, a =>
        {
            a.Property(x => x.Country).HasColumnName("country").HasMaxLength(100).IsRequired();
            a.Property(x => x.Region).HasColumnName("region").HasMaxLength(100);
            a.Property(x => x.City).HasColumnName("city").HasMaxLength(100);
            a.Property(x => x.Latitude).HasColumnName("latitude");
            a.Property(x => x.Longitude).HasColumnName("longitude");
        });

        builder.Property(u => u.ClusterId).HasColumnName("cluster_id").IsRequired();
        builder.Property(u => u.IsActive).HasColumnName("is_active").IsRequired();
        builder.Ignore(u => u.DomainEvents);
    }
}

internal sealed class ClusterConfiguration : IEntityTypeConfiguration<Cluster>
{
    public void Configure(EntityTypeBuilder<Cluster> builder)
    {
        builder.ToTable("clusters");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).ValueGeneratedNever();

        builder.OwnsOne(c => c.Name, n =>
            n.Property(x => x.Value).HasColumnName("name").HasMaxLength(200).IsRequired());

        builder.OwnsOne(c => c.Area, a =>
        {
            a.Property(x => x.Country).HasColumnName("country").HasMaxLength(100).IsRequired();
            a.Property(x => x.Region).HasColumnName("region").HasMaxLength(100);
            a.Property(x => x.City).HasColumnName("city").HasMaxLength(100);
            a.Property(x => x.Latitude).HasColumnName("latitude");
            a.Property(x => x.Longitude).HasColumnName("longitude");
        });

        builder.Property(c => c.RegionId).HasColumnName("region_id").IsRequired();
        builder.Ignore(c => c.DomainEvents);
    }
}
