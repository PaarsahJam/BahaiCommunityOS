using CommunityOS.Identity.Domain.Aggregates;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CommunityOS.Identity.Infrastructure.Persistence.Configurations;

public sealed class SessionConfiguration : IEntityTypeConfiguration<Session>
{
    public void Configure(EntityTypeBuilder<Session> builder)
    {
        builder.ToTable("sessions");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.UserAccountId).HasColumnName("user_account_id").IsRequired();
        builder.Property(x => x.DeviceId).HasColumnName("device_id").IsRequired();
        builder.Property(x => x.TokenFamilyId).HasColumnName("token_family_id").IsRequired();
        builder.Property(x => x.ClientId).HasColumnName("client_id").HasMaxLength(100);
        builder.Property(x => x.RefreshTokenHash).HasColumnName("refresh_token_hash").IsRequired().HasMaxLength(64);
        builder.Property(x => x.CreatedOn).HasColumnName("created_on").IsRequired();
        builder.Property(x => x.ExpiresOn).HasColumnName("expires_on").IsRequired();
        builder.Property(x => x.LastUsedOn).HasColumnName("last_used_on").IsRequired();
        builder.Property(x => x.RevokedOn).HasColumnName("revoked_on");
        builder.Property(x => x.RevocationReason).HasColumnName("revocation_reason").HasMaxLength(200);
        builder.Property(x => x.RefreshTokenUsed).HasColumnName("refresh_token_used").IsRequired();

        builder.HasIndex(x => x.RefreshTokenHash).IsUnique();
        builder.HasIndex(x => x.TokenFamilyId);
        builder.HasIndex(x => new { x.UserAccountId, x.RevokedOn });
    }
}
