using CommunityOS.Identity.Domain.Aggregates;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CommunityOS.Identity.Infrastructure.Persistence.Configurations;

public sealed class AuthorizationCodeConfiguration : IEntityTypeConfiguration<AuthorizationCode>
{
    public void Configure(EntityTypeBuilder<AuthorizationCode> builder)
    {
        builder.ToTable("authorization_codes");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.CodeHash).HasColumnName("code_hash").IsRequired().HasMaxLength(64);
        builder.HasIndex(x => x.CodeHash).IsUnique();

        builder.Property(x => x.UserAccountId).HasColumnName("user_account_id").IsRequired();
        builder.Property(x => x.OAuthClientId).HasColumnName("oauth_client_id").IsRequired();
        builder.Property(x => x.ClientId).HasColumnName("client_id").IsRequired().HasMaxLength(100);
        builder.Property(x => x.RedirectUri).HasColumnName("redirect_uri").IsRequired().HasMaxLength(2048);
        builder.Property(x => x.CodeChallenge).HasColumnName("code_challenge").IsRequired().HasMaxLength(256);
        builder.Property(x => x.CodeChallengeMethod).HasColumnName("code_challenge_method").IsRequired().HasMaxLength(10);
        builder.Property(x => x.Scope).HasColumnName("scope").IsRequired().HasMaxLength(500);
        builder.Property(x => x.Nonce).HasColumnName("nonce").HasMaxLength(500);
        builder.Property(x => x.IssuedOn).HasColumnName("issued_on").IsRequired();
        builder.Property(x => x.ExpiresOn).HasColumnName("expires_on").IsRequired();
        builder.Property(x => x.ConsumedOn).HasColumnName("consumed_on");

        builder.HasIndex(x => x.UserAccountId);
        builder.HasIndex(x => x.OAuthClientId);
    }
}
