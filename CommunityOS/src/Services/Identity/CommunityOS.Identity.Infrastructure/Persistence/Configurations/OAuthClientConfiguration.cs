using CommunityOS.Identity.Domain.Aggregates;
using CommunityOS.Identity.Domain.Enumerations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CommunityOS.Identity.Infrastructure.Persistence.Configurations;

public sealed class OAuthClientConfiguration : IEntityTypeConfiguration<OAuthClient>
{
    public void Configure(EntityTypeBuilder<OAuthClient> builder)
    {
        builder.ToTable("oauth_clients");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.ClientId).HasColumnName("client_id").IsRequired().HasMaxLength(100);
        builder.HasIndex(x => x.ClientId).IsUnique();

        builder.Property(x => x.DisplayName).HasColumnName("display_name").IsRequired().HasMaxLength(200);
        builder.Property(x => x.Type)
            .HasConversion(
                type => type.Id,
                id => OAuthClientType.FromId(id))
            .HasColumnName("type_id")
            .IsRequired();
        builder.Property(x => x.Enabled).HasColumnName("enabled").IsRequired();
        builder.Property(x => x.CreatedOn).HasColumnName("created_on").IsRequired();
        builder.Property(x => x.ClientSecretHash).HasColumnName("client_secret_hash").HasMaxLength(256);

        builder.PrimitiveCollection(x => x.RedirectUris).HasField("_redirectUris");
        builder.PrimitiveCollection(x => x.AllowedGrantTypes).HasField("_grantTypes");
        builder.PrimitiveCollection(x => x.AllowedScopes).HasField("_allowedScopes");
    }
}
