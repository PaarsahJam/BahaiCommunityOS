using CommunityOS.Identity.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CommunityOS.Identity.Infrastructure.Persistence.Configurations;

public sealed class RecoveryRequestConfiguration : IEntityTypeConfiguration<RecoveryRequest>
{
    public void Configure(EntityTypeBuilder<RecoveryRequest> builder)
    {
        builder.ToTable("recovery_requests");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.UserAccountId).HasColumnName("user_account_id").IsRequired();
        builder.Property(x => x.TokenHash).HasColumnName("token_hash").IsRequired().HasMaxLength(64);
        builder.Property(x => x.Purpose).HasColumnName("purpose").IsRequired().HasMaxLength(50);
        builder.Property(x => x.RequestedOn).HasColumnName("requested_on").IsRequired();
        builder.Property(x => x.ExpiresOn).HasColumnName("expires_on").IsRequired();
        builder.Property(x => x.ConsumedOn).HasColumnName("consumed_on");
        builder.Property(x => x.Attempts).HasColumnName("attempts").IsRequired();

        builder.HasIndex(x => x.TokenHash).IsUnique();
        builder.HasIndex(x => new { x.UserAccountId, x.Purpose, x.ExpiresOn });
    }
}
