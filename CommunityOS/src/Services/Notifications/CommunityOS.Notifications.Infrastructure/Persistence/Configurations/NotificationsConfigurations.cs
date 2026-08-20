using CommunityOS.Notifications.Domain.Aggregates;
using CommunityOS.Notifications.Domain.Entities;
using CommunityOS.Notifications.Domain.Enumerations;
using CommunityOS.Notifications.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CommunityOS.Notifications.Infrastructure.Persistence.Configurations;

internal sealed class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> builder)
    {
        builder.ToTable("notifications");
        builder.HasKey(n => n.Id);
        builder.Property(n => n.Id).ValueGeneratedNever();

        builder.Property(n => n.TypeCode).HasColumnName("type_code").HasMaxLength(100).IsRequired();
        builder.Property(n => n.Channel)
            .HasColumnName("channel")
            .HasConversion(v => v.Id, id => NotificationChannel.FromId(id))
            .IsRequired();
        builder.Property(n => n.SourceType).HasColumnName("source_type").HasMaxLength(50);
        builder.Property(n => n.SourceId).HasColumnName("source_id");
        builder.Property(n => n.OrganizationUnitId).HasColumnName("organization_unit_id");
        builder.Property(n => n.ScheduledFor).HasColumnName("scheduled_for");
        builder.Property(n => n.IsSensitive).HasColumnName("is_sensitive").IsRequired();
        builder.Property(n => n.CreatedBy).HasColumnName("created_by").IsRequired();
        builder.Property(n => n.CreatedOn).HasColumnName("created_on").IsRequired();
        builder.Property(n => n.DispatchedOn).HasColumnName("dispatched_on");

        builder.Property(n => n.Status)
            .HasColumnName("status")
            .HasConversion(v => v.Id, id => NotificationLifecycleStatus.FromId(id))
            .IsRequired();

        builder.OwnsOne(n => n.Template, t =>
        {
            t.Property(x => x.Subject).HasColumnName("subject").HasMaxLength(500).IsRequired();
            t.Property(x => x.Body).HasColumnName("body").HasMaxLength(2000).IsRequired();
        });

        builder.OwnsMany(n => n.Recipients, r =>
        {
            r.ToTable("notification_recipients");
            r.WithOwner().HasForeignKey("notification_id");
            r.HasKey("Id");
            r.Property(x => x.Id).ValueGeneratedNever();
            r.Property(x => x.MemberId).HasColumnName("member_id").IsRequired();
            r.Property(x => x.Channel)
                .HasColumnName("channel")
                .HasConversion(v => v.Id, id => NotificationChannel.FromId(id))
                .IsRequired();
            r.Property(x => x.Status)
                .HasColumnName("status")
                .HasConversion(v => v.Id, id => NotificationRecipientStatus.FromId(id))
                .IsRequired();
            r.Property(x => x.DeliveredAt).HasColumnName("delivered_at");
            r.Property(x => x.ReadAt).HasColumnName("read_at");
            r.Property(x => x.FailureReason).HasColumnName("failure_reason").HasMaxLength(200);
            r.Property(x => x.RetryCount).HasColumnName("retry_count").IsRequired();

            r.HasIndex("notification_id", "MemberId")
                .HasDatabaseName("ix_notification_recipients_notification_member");
            r.HasIndex("MemberId")
                .HasDatabaseName("ix_notification_recipients_member_id");
        });

        builder.OwnsMany(n => n.AdditionalScopes, s =>
        {
            s.ToTable("notification_scopes");
            s.WithOwner().HasForeignKey("notification_id");
            s.HasKey("Id");
            s.Property(x => x.Id).ValueGeneratedNever();
            s.Property(x => x.OrganizationUnitId).HasColumnName("organization_unit_id").IsRequired();

            s.HasIndex("notification_id", "OrganizationUnitId")
                .IsUnique()
                .HasDatabaseName("ix_notification_scopes_notification_unit");
        });

        builder.HasIndex(n => n.OrganizationUnitId)
            .HasDatabaseName("ix_notifications_organization_unit_id");
        builder.HasIndex(n => n.TypeCode)
            .HasDatabaseName("ix_notifications_type_code");
        builder.HasIndex(n => n.SourceId)
            .HasDatabaseName("ix_notifications_source_id");

        // Idempotency: one notification per (type, source type, source id,
        // channel). Filtered to source-bound rows; free-standing general
        // notifications are exempt. The reconcile consumers rely on this for
        // create-if-absent reconciliation (ADR-025).
        builder.HasIndex("TypeCode", "SourceType", "SourceId", "Channel")
            .IsUnique()
            .HasFilter("\"source_type\" IS NOT NULL AND \"source_id\" IS NOT NULL")
            .HasDatabaseName("ix_notifications_source_idempotency");

        builder.Ignore(n => n.DomainEvents);
    }
}

internal sealed class NotificationTypeConfiguration : IEntityTypeConfiguration<NotificationType>
{
    public void Configure(EntityTypeBuilder<NotificationType> builder)
    {
        builder.ToTable("notification_types");
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id).ValueGeneratedNever();

        builder.Property(t => t.Code).HasColumnName("code").HasMaxLength(100).IsRequired();
        builder.Property(t => t.DisplayName).HasColumnName("display_name").HasMaxLength(200).IsRequired();
        builder.Property(t => t.DefaultChannel)
            .HasColumnName("default_channel")
            .HasConversion(v => v.Id, id => NotificationChannel.FromId(id))
            .IsRequired();
        builder.Property(t => t.SubjectTemplate).HasColumnName("subject_template").HasMaxLength(500).IsRequired();
        builder.Property(t => t.BodyTemplate).HasColumnName("body_template").HasMaxLength(2000).IsRequired();
        builder.Property(t => t.IsSensitive).HasColumnName("is_sensitive").IsRequired();
        builder.Property(t => t.IsActive).HasColumnName("is_active").IsRequired();
        builder.Property(t => t.CreatedBy).HasColumnName("created_by").IsRequired();
        builder.Property(t => t.CreatedOn).HasColumnName("created_on").IsRequired();
        builder.Property(t => t.UpdatedBy).HasColumnName("updated_by").IsRequired();
        builder.Property(t => t.UpdatedOn).HasColumnName("updated_on").IsRequired();
        builder.Property(t => t.RetiredBy).HasColumnName("retired_by");
        builder.Property(t => t.RetiredOn).HasColumnName("retired_on");

        builder.HasIndex(t => t.Code).IsUnique().HasDatabaseName("ix_notification_types_code");
        builder.Ignore(t => t.DomainEvents);
    }
}

internal sealed class NotificationPreferenceConfiguration : IEntityTypeConfiguration<NotificationPreference>
{
    public void Configure(EntityTypeBuilder<NotificationPreference> builder)
    {
        builder.ToTable("notification_preferences");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).ValueGeneratedNever();

        builder.Property(p => p.MemberId).HasColumnName("member_id").IsRequired();
        builder.Property(p => p.TypeCode).HasColumnName("type_code").HasMaxLength(100).IsRequired();
        builder.Property(p => p.Channel)
            .HasColumnName("channel")
            .HasConversion(v => v.Id, id => NotificationChannel.FromId(id))
            .IsRequired();
        builder.Property(p => p.Enabled).HasColumnName("enabled").IsRequired();

        builder.HasIndex(p => new { p.MemberId, p.TypeCode, p.Channel })
            .IsUnique()
            .HasDatabaseName("ix_notification_preferences_member_type_channel");
    }
}

internal sealed class NotificationsOrganizationUnitReferenceConfiguration
    : IEntityTypeConfiguration<OrganizationUnitReference>
{
    public void Configure(EntityTypeBuilder<OrganizationUnitReference> builder)
    {
        builder.ToTable("organization_unit_references");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).ValueGeneratedNever();

        builder.Property(r => r.OrganizationUnitId).HasColumnName("organization_unit_id").IsRequired();
        builder.Property(r => r.CreatedOn).HasColumnName("created_on").IsRequired();
        builder.Ignore(r => r.DomainEvents);

        builder.HasIndex(r => r.OrganizationUnitId).IsUnique()
            .HasDatabaseName("ix_notifications_organization_unit_references_unit_id");
    }
}