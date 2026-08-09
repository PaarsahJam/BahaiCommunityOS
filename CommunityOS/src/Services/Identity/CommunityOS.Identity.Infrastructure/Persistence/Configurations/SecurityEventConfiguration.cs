using CommunityOS.Identity.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CommunityOS.Identity.Infrastructure.Persistence.Configurations;

public sealed class SecurityEventConfiguration : IEntityTypeConfiguration<SecurityEvent>
{
    public void Configure(EntityTypeBuilder<SecurityEvent> builder)
    {
        builder.ToTable("security_events");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.UserAccountId).HasColumnName("user_account_id").IsRequired();
        builder.Property(x => x.EventType).HasColumnName("event_type").IsRequired().HasMaxLength(50);
        builder.Property(x => x.Description).HasColumnName("description").HasMaxLength(500);
        builder.Property(x => x.OccurredOn).HasColumnName("occurred_on").IsRequired();
        builder.Property(x => x.IpAddress).HasColumnName("ip_address").HasMaxLength(64);
        builder.Property(x => x.UserAgent).HasColumnName("user_agent").HasMaxLength(512);

        builder.HasIndex(x => new { x.UserAccountId, x.OccurredOn });
    }
}
