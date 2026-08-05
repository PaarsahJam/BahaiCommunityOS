using CommunityOS.Identity.Domain.Aggregates;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CommunityOS.Identity.Infrastructure.Persistence.Configurations;

internal sealed class MemberConfiguration : IEntityTypeConfiguration<Member>
{
    public void Configure(EntityTypeBuilder<Member> builder)
    {
        builder.ToTable("members");

        builder.HasKey(m => m.Id);

        builder.Property(m => m.Id)
            .ValueGeneratedNever();

        builder.OwnsOne(m => m.Name, name =>
        {
            name.Property(n => n.FirstName).HasColumnName("first_name").HasMaxLength(100).IsRequired();
            name.Property(n => n.LastName).HasColumnName("last_name").HasMaxLength(100).IsRequired();
            name.Property(n => n.MiddleName).HasColumnName("middle_name").HasMaxLength(100);
        });

        builder.OwnsOne(m => m.Email, email =>
        {
            email.Property(e => e.Value).HasColumnName("email").HasMaxLength(254).IsRequired();
            email.HasIndex(e => e.Value).IsUnique();
        });

        builder.OwnsOne(m => m.PhoneNumber, phone =>
        {
            phone.Property(p => p.Value).HasColumnName("phone_number").HasMaxLength(20);
        });

        builder.OwnsOne(m => m.Address, addr =>
        {
            addr.Property(a => a.Line1).HasColumnName("address_line1").HasMaxLength(200);
            addr.Property(a => a.Line2).HasColumnName("address_line2").HasMaxLength(200);
            addr.Property(a => a.City).HasColumnName("address_city").HasMaxLength(100);
            addr.Property(a => a.StateProvince).HasColumnName("address_state").HasMaxLength(100);
            addr.Property(a => a.PostalCode).HasColumnName("address_postal_code").HasMaxLength(20);
            addr.Property(a => a.CountryCode).HasColumnName("address_country_code").HasMaxLength(2);
        });

        builder.Property(m => m.Status)
            .HasColumnName("status")
            .HasConversion(
                s => s.Id,
                id => Domain.ValueObjects.MembershipStatus.FromId(id))
            .IsRequired();

        builder.Property(m => m.EnrolledOn).HasColumnName("enrolled_on").IsRequired();
        builder.Property(m => m.TransferredOn).HasColumnName("transferred_on");
        builder.Property(m => m.LocalUnitId).HasColumnName("local_unit_id");
        builder.Property(m => m.Version).HasColumnName("version").IsConcurrencyToken();

        builder.Ignore(m => m.DomainEvents);
    }
}
