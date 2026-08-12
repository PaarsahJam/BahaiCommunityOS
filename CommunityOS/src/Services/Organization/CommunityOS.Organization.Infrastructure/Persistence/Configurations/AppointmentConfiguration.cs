using CommunityOS.Organization.Domain.Aggregates;
using CommunityOS.Organization.Domain.Enumerations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CommunityOS.Organization.Infrastructure.Persistence.Configurations;

public sealed class AppointmentConfiguration : IEntityTypeConfiguration<Appointment>
{
    public void Configure(EntityTypeBuilder<Appointment> builder)
    {
        builder.ToTable("appointments");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.PersonId).HasColumnName("person_id").IsRequired();
        builder.Property(x => x.OrganizationUnitId).HasColumnName("organization_unit_id").IsRequired();
        builder.Property(x => x.AppointmentType).HasColumnName("appointment_type").IsRequired().HasMaxLength(100);
        builder.Property(x => x.Status)
            .HasConversion(status => status.Id, id => AppointmentStatus.FromId(id))
            .HasColumnName("status")
            .IsRequired();

        builder.OwnsOne(x => x.Period, period =>
        {
            period.Property(p => p.EffectiveFrom).HasColumnName("effective_from").IsRequired();
            period.Property(p => p.EffectiveUntil).HasColumnName("effective_until");
        });

        builder.Property(x => x.AssignedBy).HasColumnName("assigned_by");
        builder.Property(x => x.AssignedOn).HasColumnName("assigned_on").IsRequired();
        builder.Property(x => x.EndedBy).HasColumnName("ended_by");
        builder.Property(x => x.EndedOn).HasColumnName("ended_on");
        builder.Property(x => x.Reason).HasColumnName("reason").HasMaxLength(500);
        builder.Property(x => x.Version).HasColumnName("version").IsRequired();

        builder.HasIndex(x => x.PersonId);
        builder.HasIndex(x => x.OrganizationUnitId);
        builder.HasIndex(x => new { x.PersonId, x.OrganizationUnitId, x.AppointmentType });
    }
}