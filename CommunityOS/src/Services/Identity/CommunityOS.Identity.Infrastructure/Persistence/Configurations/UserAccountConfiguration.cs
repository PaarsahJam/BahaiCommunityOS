using CommunityOS.Identity.Domain.Aggregates;
using CommunityOS.Identity.Domain.Entities;
using CommunityOS.Identity.Domain.Enumerations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CommunityOS.Identity.Infrastructure.Persistence.Configurations;

public sealed class UserAccountConfiguration : IEntityTypeConfiguration<UserAccount>
{
    public void Configure(EntityTypeBuilder<UserAccount> builder)
    {
        builder.ToTable("user_accounts");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Email)
            .HasConversion(
                email => email.Value,
                value => CommunityOS.Identity.Domain.ValueObjects.Email.Create(value))
            .HasColumnName("email")
            .IsRequired()
            .HasMaxLength(254);

        builder.HasIndex(x => x.Email).IsUnique();

        builder.Property(x => x.Status)
            .HasConversion(
                status => status.Id,
                id => AccountStatus.FromId(id))
            .HasColumnName("status_id")
            .IsRequired();

        builder.Property(x => x.CreatedOn).HasColumnName("created_on").IsRequired();
        builder.Property(x => x.VerifiedOn).HasColumnName("verified_on");
        builder.Property(x => x.DeactivatedOn).HasColumnName("deactivated_on");
        builder.Property(x => x.LastLoginOn).HasColumnName("last_login_on");
        builder.Property(x => x.FailedLoginAttempts).HasColumnName("failed_login_attempts").IsRequired();
        builder.Property(x => x.LockedUntil).HasColumnName("locked_until");
        builder.Property(x => x.SessionRevocationEpoch)
            .HasColumnName("session_revocation_epoch")
            .IsRequired()
            .HasDefaultValue(0L);

        builder.OwnsMany<Credential>(
            x => x.Credentials,
            credential =>
            {
                credential.ToTable("user_account_credentials");
                credential.WithOwner().HasForeignKey("user_account_id");
                credential.HasKey("Id");
                credential.Property(x => x.Id).HasColumnName("id");
                credential.Property(x => x.Type)
                    .HasConversion(
                        type => type.Id,
                        id => CredentialType.FromId(id))
                    .HasColumnName("type_id")
                    .IsRequired();
                credential.Property(x => x.Secret).HasColumnName("secret").IsRequired();
                credential.Property(x => x.Metadata).HasColumnName("metadata");
                credential.Property(x => x.CreatedOn).HasColumnName("created_on").IsRequired();
                credential.Property(x => x.LastUsedOn).HasColumnName("last_used_on");
            });

        builder.OwnsMany<ExternalIdentity>(
            x => x.ExternalIdentities,
            external =>
            {
                external.ToTable("user_account_external_identities");
                external.WithOwner().HasForeignKey("user_account_id");
                external.HasKey("Id");
                external.Property(x => x.Id).HasColumnName("id");
                external.Property(x => x.Provider).HasColumnName("provider").IsRequired().HasMaxLength(100);
                external.Property(x => x.Subject).HasColumnName("subject").IsRequired().HasMaxLength(256);
                external.Property(x => x.LinkedOn).HasColumnName("linked_on").IsRequired();
                external.Property(x => x.UnlinkedOn).HasColumnName("unlinked_on");
                external.HasIndex("Provider", "Subject");
            });

        builder.OwnsMany<MfaMethod>(
            x => x.MfaMethods,
            mfa =>
            {
                mfa.ToTable("user_account_mfa_methods");
                mfa.WithOwner().HasForeignKey("user_account_id");
                mfa.HasKey("Id");
                mfa.Property(x => x.Id).HasColumnName("id");
                mfa.Property(x => x.Type)
                    .HasConversion(
                        type => type.Id,
                        id => MfaMethodType.FromId(id))
                    .HasColumnName("type_id")
                    .IsRequired();
                mfa.Property(x => x.Secret).HasColumnName("secret").IsRequired();
                mfa.Property(x => x.CreatedOn).HasColumnName("created_on").IsRequired();
                mfa.Property(x => x.VerifiedOn).HasColumnName("verified_on");
                mfa.Property(x => x.IsActive).HasColumnName("is_active").IsRequired();
            });

        builder.OwnsMany<Device>(
            x => x.Devices,
            device =>
            {
                device.ToTable("user_account_devices");
                device.WithOwner().HasForeignKey("user_account_id");
                device.HasKey("Id");
                device.Property(x => x.Id).HasColumnName("id");
                device.Property(x => x.Name).HasColumnName("name").IsRequired().HasMaxLength(100);
                device.Property(x => x.Platform).HasColumnName("platform").HasMaxLength(50);
                device.Property(x => x.UserAgent).HasColumnName("user_agent").HasMaxLength(512);
                device.Property(x => x.RegisteredOn).HasColumnName("registered_on").IsRequired();
                device.Property(x => x.LastSeenOn).HasColumnName("last_seen_on");
                device.Property(x => x.IsTrusted).HasColumnName("is_trusted").IsRequired();
                device.Property(x => x.TrustedUntil).HasColumnName("trusted_until");
            });
    }
}
