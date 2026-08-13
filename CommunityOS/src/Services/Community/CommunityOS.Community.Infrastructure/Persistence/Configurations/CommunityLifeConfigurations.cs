using CommunityOS.Community.Domain.Aggregates;
using CommunityOS.Community.Domain.Entities;
using CommunityOS.Community.Domain.Enumerations;
using CommunityOS.Community.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CommunityOS.Community.Infrastructure.Persistence.Configurations;

internal sealed class PersonConfiguration : IEntityTypeConfiguration<Person>
{
    public void Configure(EntityTypeBuilder<Person> builder)
    {
        builder.ToTable("persons");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).ValueGeneratedNever();

        builder.Property(p => p.PreferredName).HasColumnName("preferred_name").HasMaxLength(200).IsRequired();
        builder.Property(p => p.FormalName).HasColumnName("formal_name").HasMaxLength(200);
        builder.Property(p => p.DateOfBirth).HasColumnName("date_of_birth");
        builder.Property(p => p.PreferredLanguage).HasColumnName("preferred_language").HasMaxLength(20);
        builder.Property(p => p.Status).HasColumnName("status").HasMaxLength(50).IsRequired();
        builder.Property(p => p.IdentityAccountId).HasColumnName("identity_account_id");
        builder.Property(p => p.IdentityLinkedOn).HasColumnName("identity_linked_on");
        builder.Property(p => p.IdentityUnlinkedOn).HasColumnName("identity_unlinked_on");
        builder.Property(p => p.CreatedOn).HasColumnName("created_on").IsRequired();

        builder.OwnsOne(p => p.Privacy, privacy =>
        {
            privacy.Property(x => x.ProfileVisibility)
                .HasColumnName("profile_visibility")
                .HasConversion(v => v.Id, id => ContactVisibility.FromId(id))
                .IsRequired();
            privacy.Property(x => x.ContactVisibility)
                .HasColumnName("contact_visibility")
                .HasConversion(v => v.Id, id => ContactVisibility.FromId(id))
                .IsRequired();
            privacy.Property(x => x.DateOfBirthVisibility)
                .HasColumnName("date_of_birth_visibility")
                .HasConversion(v => v.Id, id => ContactVisibility.FromId(id))
                .IsRequired();
        });

        builder.OwnsMany(p => p.ContactMethods, contact =>
        {
            contact.ToTable("contact_methods");
            contact.WithOwner().HasForeignKey("person_id");
            contact.HasKey("Id");
            contact.Property(x => x.Id).ValueGeneratedNever();
            contact.Property(x => x.Value).HasColumnName("value").HasMaxLength(320).IsRequired();
            contact.Property(x => x.IsPreferred).HasColumnName("is_preferred").IsRequired();
            contact.Property(x => x.Type)
                .HasColumnName("type")
                .HasConversion(v => v.Id, id => ContactMethodType.FromId(id))
                .IsRequired();
            contact.Property(x => x.Visibility)
                .HasColumnName("visibility")
                .HasConversion(v => v.Id, id => ContactVisibility.FromId(id))
                .IsRequired();
        });

        builder.Ignore(p => p.DomainEvents);
    }
}

internal sealed class HouseholdConfiguration : IEntityTypeConfiguration<Household>
{
    public void Configure(EntityTypeBuilder<Household> builder)
    {
        builder.ToTable("households");
        builder.HasKey(h => h.Id);
        builder.Property(h => h.Id).ValueGeneratedNever();

        builder.Property(h => h.Name).HasColumnName("name").HasMaxLength(200);

        builder.OwnsOne(h => h.Address, address =>
        {
            address.Property(x => x.Line1).HasColumnName("address_line1").HasMaxLength(200);
            address.Property(x => x.Line2).HasColumnName("address_line2").HasMaxLength(200);
            address.Property(x => x.City).HasColumnName("address_city").HasMaxLength(100);
            address.Property(x => x.Region).HasColumnName("address_region").HasMaxLength(100);
            address.Property(x => x.PostalCode).HasColumnName("address_postal_code").HasMaxLength(20);
            address.Property(x => x.Country).HasColumnName("address_country").HasMaxLength(100);
        });

        builder.OwnsMany(h => h.Members, member =>
        {
            member.ToTable("household_members");
            member.WithOwner().HasForeignKey("household_id");
            member.HasKey("Id");
            member.Property(x => x.Id).ValueGeneratedNever();
            member.Property(x => x.PersonId).HasColumnName("person_id").IsRequired();
            member.Property(x => x.Role)
                .HasColumnName("role")
                .HasConversion(v => v.Id, id => HouseholdMemberRole.FromId(id))
                .IsRequired();
            member.OwnsOne(x => x.Period, period =>
            {
                period.Property(p => p.EffectiveFrom).HasColumnName("effective_from").IsRequired();
                period.Property(p => p.EffectiveUntil).HasColumnName("effective_until");
            });
        });

        builder.Ignore(h => h.DomainEvents);
    }
}

internal sealed class FamilyRelationshipConfiguration : IEntityTypeConfiguration<FamilyRelationship>
{
    public void Configure(EntityTypeBuilder<FamilyRelationship> builder)
    {
        builder.ToTable("family_relationships");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).ValueGeneratedNever();

        builder.Property(r => r.PersonIdA).HasColumnName("person_id_a").IsRequired();
        builder.Property(r => r.PersonIdB).HasColumnName("person_id_b").IsRequired();
        builder.Property(r => r.RelationshipType)
            .HasColumnName("relationship_type")
            .HasConversion(v => v.Id, id => RelationshipType.FromId(id))
            .IsRequired();

        builder.OwnsOne(r => r.Period, period =>
        {
            period.Property(p => p.EffectiveFrom).HasColumnName("effective_from").IsRequired();
            period.Property(p => p.EffectiveUntil).HasColumnName("effective_until");
        });

        builder.HasIndex(r => new { r.PersonIdA, r.PersonIdB });
        builder.Ignore(r => r.DomainEvents);
    }
}

internal sealed class MembershipConfiguration : IEntityTypeConfiguration<Membership>
{
    public void Configure(EntityTypeBuilder<Membership> builder)
    {
        builder.ToTable("memberships");
        builder.HasKey(m => m.Id);
        builder.Property(m => m.Id).ValueGeneratedNever();

        builder.Property(m => m.PersonId).HasColumnName("person_id").IsRequired();
        builder.Property(m => m.Status)
            .HasColumnName("status")
            .HasConversion(v => v.Id, id => MembershipStatus.FromId(id))
            .IsRequired();
        builder.Property(m => m.WithdrawnOn).HasColumnName("withdrawn_on");

        builder.OwnsOne(m => m.Period, period =>
        {
            period.Property(p => p.EffectiveFrom).HasColumnName("effective_from").IsRequired();
            period.Property(p => p.EffectiveUntil).HasColumnName("effective_until");
        });

        builder.OwnsMany(m => m.PeriodHistory, history =>
        {
            history.ToTable("membership_periods");
            history.WithOwner().HasForeignKey("membership_id");
            history.HasKey("Id");
            history.Property(x => x.Id).ValueGeneratedNever();
            history.Property(x => x.Status)
                .HasColumnName("status")
                .HasConversion(v => v.Id, id => MembershipStatus.FromId(id))
                .IsRequired();
            history.OwnsOne(x => x.Period, period =>
            {
                period.Property(p => p.EffectiveFrom).HasColumnName("effective_from").IsRequired();
                period.Property(p => p.EffectiveUntil).HasColumnName("effective_until");
            });
        });

        builder.HasIndex(m => m.PersonId).IsUnique();
        builder.Ignore(m => m.DomainEvents);
    }
}

internal sealed class ActivityConfiguration : IEntityTypeConfiguration<Activity>
{
    public void Configure(EntityTypeBuilder<Activity> builder)
    {
        builder.ToTable("activities");
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).ValueGeneratedNever();

        builder.Property(a => a.Title).HasColumnName("title").HasMaxLength(200).IsRequired();
        builder.Property(a => a.Description).HasColumnName("description").HasMaxLength(2000);
        builder.Property(a => a.Category).HasColumnName("category").HasMaxLength(100);
        builder.Property(a => a.OrganizerPersonId).HasColumnName("organizer_person_id");
        builder.Property(a => a.OrganizationUnitId).HasColumnName("organization_unit_id");
        builder.Property(a => a.Location).HasColumnName("location").HasMaxLength(300);
        builder.Property(a => a.IsOnline).HasColumnName("is_online").IsRequired();
        builder.Property(a => a.OnlineUrl).HasColumnName("online_url").HasMaxLength(1000);
        builder.Property(a => a.Capacity).HasColumnName("capacity");
        builder.Property(a => a.CreatedOn).HasColumnName("created_on").IsRequired();

        builder.OwnsOne(a => a.Schedule, schedule =>
        {
            schedule.Property(s => s.StartsAt).HasColumnName("starts_at").IsRequired();
            schedule.Property(s => s.EndsAt).HasColumnName("ends_at");
        });

        builder.Property(a => a.Visibility)
            .HasColumnName("visibility")
            .HasConversion(v => v.Id, id => ActivityVisibility.FromId(id))
            .IsRequired();
        builder.Property(a => a.Status)
            .HasColumnName("status")
            .HasConversion(v => v.Id, id => ActivityStatus.FromId(id))
            .IsRequired();

        builder.HasIndex(a => a.OrganizationUnitId);
        builder.Ignore(a => a.DomainEvents);
    }
}

internal sealed class CommunityEventConfiguration : IEntityTypeConfiguration<CommunityEvent>
{
    public void Configure(EntityTypeBuilder<CommunityEvent> builder)
    {
        builder.ToTable("community_events");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedNever();

        builder.Property(e => e.Title).HasColumnName("title").HasMaxLength(200).IsRequired();
        builder.Property(e => e.Description).HasColumnName("description").HasMaxLength(2000);
        builder.Property(e => e.StartsAt).HasColumnName("starts_at").IsRequired();
        builder.Property(e => e.EndsAt).HasColumnName("ends_at");
        builder.Property(e => e.TimeZone).HasColumnName("time_zone").HasMaxLength(100).IsRequired();
        builder.Property(e => e.Location).HasColumnName("location").HasMaxLength(300);
        builder.Property(e => e.IsOnline).HasColumnName("is_online").IsRequired();
        builder.Property(e => e.OnlineUrl).HasColumnName("online_url").HasMaxLength(1000);
        builder.Property(e => e.OrganizerPersonId).HasColumnName("organizer_person_id");
        builder.Property(e => e.OrganizationUnitId).HasColumnName("organization_unit_id");
        builder.Property(e => e.RegistrationOpen).HasColumnName("registration_open").IsRequired();
        builder.Property(e => e.Capacity).HasColumnName("capacity");
        builder.Property(e => e.CreatedOn).HasColumnName("created_on").IsRequired();

        builder.Property(e => e.Status)
            .HasColumnName("status")
            .HasConversion(v => v.Id, id => CommunityEventStatus.FromId(id))
            .IsRequired();
        builder.Property(e => e.Visibility)
            .HasColumnName("visibility")
            .HasConversion(v => v.Id, id => CommunityEventVisibility.FromId(id))
            .IsRequired();

        builder.HasIndex(e => e.OrganizationUnitId);
        builder.Ignore(e => e.DomainEvents);
    }
}

internal sealed class MeetingConfiguration : IEntityTypeConfiguration<Meeting>
{
    public void Configure(EntityTypeBuilder<Meeting> builder)
    {
        builder.ToTable("meetings");
        builder.HasKey(m => m.Id);
        builder.Property(m => m.Id).ValueGeneratedNever();

        builder.Property(m => m.Title).HasColumnName("title").HasMaxLength(200).IsRequired();
        builder.Property(m => m.Description).HasColumnName("description").HasMaxLength(2000);
        builder.Property(m => m.StartsAt).HasColumnName("starts_at").IsRequired();
        builder.Property(m => m.EndsAt).HasColumnName("ends_at");
        builder.Property(m => m.TimeZone).HasColumnName("time_zone").HasMaxLength(100).IsRequired();
        builder.Property(m => m.Location).HasColumnName("location").HasMaxLength(300);
        builder.Property(m => m.OrganizerPersonId).HasColumnName("organizer_person_id");
        builder.Property(m => m.OrganizationUnitId).HasColumnName("organization_unit_id");
        builder.Property(m => m.Minutes).HasColumnName("minutes").HasMaxLength(20000);
        builder.Property(m => m.CreatedOn).HasColumnName("created_on").IsRequired();

        builder.Property(m => m.Status)
            .HasColumnName("status")
            .HasConversion(v => v.Id, id => MeetingStatus.FromId(id))
            .IsRequired();
        builder.Property(m => m.Visibility)
            .HasColumnName("visibility")
            .HasConversion(v => v.Id, id => MeetingVisibility.FromId(id))
            .IsRequired();

        builder.OwnsMany(m => m.Participants, participant =>
        {
            participant.ToTable("meeting_participants");
            participant.WithOwner().HasForeignKey("meeting_id");
            participant.HasKey("Id");
            participant.Property(x => x.Id).ValueGeneratedNever();
            participant.Property(x => x.PersonId).HasColumnName("person_id").IsRequired();
            participant.Property(x => x.Role).HasColumnName("role").HasMaxLength(100).IsRequired();
            participant.Property(x => x.Attendance)
                .HasColumnName("attendance")
                .HasConversion(v => v.Id, id => AttendanceStatus.FromId(id))
                .IsRequired();
        });

        builder.OwnsMany(m => m.AgendaItems, item =>
        {
            item.ToTable("meeting_agenda_items");
            item.WithOwner().HasForeignKey("meeting_id");
            item.HasKey("Id");
            item.Property(x => x.Id).ValueGeneratedNever();
            item.Property(x => x.Title).HasColumnName("title").HasMaxLength(200).IsRequired();
            item.Property(x => x.Description).HasColumnName("description").HasMaxLength(2000);
            item.Property(x => x.Order).HasColumnName("order").IsRequired();
            item.Property(x => x.IsCompleted).HasColumnName("is_completed").IsRequired();
        });

        builder.OwnsMany(m => m.Actions, action =>
        {
            action.ToTable("meeting_actions");
            action.WithOwner().HasForeignKey("meeting_id");
            action.HasKey("Id");
            action.Property(x => x.Id).ValueGeneratedNever();
            action.Property(x => x.Description).HasColumnName("description").HasMaxLength(2000).IsRequired();
            action.Property(x => x.AssigneePersonId).HasColumnName("assignee_person_id");
            action.Property(x => x.DueDate).HasColumnName("due_date");
            action.Property(x => x.IsCompleted).HasColumnName("is_completed").IsRequired();
            action.Property(x => x.CompletedOn).HasColumnName("completed_on");
        });

        builder.HasIndex(m => m.OrganizationUnitId);
        builder.Ignore(m => m.DomainEvents);
    }
}

internal sealed class ParticipationConfiguration : IEntityTypeConfiguration<Participation>
{
    public void Configure(EntityTypeBuilder<Participation> builder)
    {
        builder.ToTable("participations");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).ValueGeneratedNever();

        builder.Property(p => p.PersonId).HasColumnName("person_id").IsRequired();
        builder.Property(p => p.TargetType)
            .HasColumnName("target_type")
            .HasConversion(v => v.Id, id => ParticipationTargetType.FromId(id))
            .IsRequired();
        builder.Property(p => p.TargetId).HasColumnName("target_id").IsRequired();
        builder.Property(p => p.Role).HasColumnName("role").HasMaxLength(100);
        builder.Property(p => p.Status)
            .HasColumnName("status")
            .HasConversion(v => v.Id, id => ParticipationStatus.FromId(id))
            .IsRequired();
        builder.Property(p => p.RecordedOn).HasColumnName("recorded_on").IsRequired();

        builder.OwnsOne(p => p.Period, period =>
        {
            period.Property(x => x.EffectiveFrom).HasColumnName("effective_from").IsRequired();
            period.Property(x => x.EffectiveUntil).HasColumnName("effective_until");
        });

        builder.HasIndex(p => p.PersonId);
        builder.HasIndex(p => new { p.TargetType, p.TargetId });
        builder.Ignore(p => p.DomainEvents);
    }
}
