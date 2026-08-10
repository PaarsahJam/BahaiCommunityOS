using CommunityOS.Authorization.Domain.Aggregates;
using CommunityOS.Authorization.Domain.Enumerations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace CommunityOS.Authorization.Infrastructure.Persistence.Configurations;

public sealed class BreakGlassRequestConfiguration : IEntityTypeConfiguration<BreakGlassRequest>
{
    public void Configure(EntityTypeBuilder<BreakGlassRequest> builder)
    {
        builder.ToTable("break_glass_requests");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.RequesterId).HasColumnName("requester_id").IsRequired();

        builder.OwnsOne(x => x.Scope, scope =>
        {
            scope.Property(s => s.Type)
                .HasConversion(type => type.Id, id => ScopeType.FromId(id))
                .HasColumnName("scope_type")
                .IsRequired();
            scope.Property(s => s.ScopeId).HasColumnName("scope_id");
            scope.Property(s => s.ResourceType).HasColumnName("scope_resource_type").HasMaxLength(128);
        });

        builder.PrimitiveCollection(x => x.Permissions).HasColumnName("permissions").HasField("_permissions");

        builder.Property(x => x.Reason).HasColumnName("reason").IsRequired().HasMaxLength(1000);
        builder.Property(x => x.RequestedAt).HasColumnName("requested_at").IsRequired();
        builder.Property(x => x.RequestedDuration)
            .HasColumnName("requested_duration_minutes")
            .HasConversion(new TimeSpanToMinutesConverter())
            .IsRequired();
        builder.Property(x => x.State)
            .HasConversion(state => state.Id, id => BreakGlassRequestState.FromId(id))
            .HasColumnName("state")
            .IsRequired();
        builder.Property(x => x.ApproverId).HasColumnName("approver_id");
        builder.Property(x => x.ApprovedOn).HasColumnName("approved_on");
        builder.Property(x => x.ApprovedUntil).HasColumnName("approved_until");
        builder.Property(x => x.RejectionReason).HasColumnName("rejection_reason").HasMaxLength(1000);
        builder.Property(x => x.RevokedBy).HasColumnName("revoked_by");
        builder.Property(x => x.RevokedAt).HasColumnName("revoked_at");
        builder.Property(x => x.RevocationReason).HasColumnName("revocation_reason").HasMaxLength(1000);

        builder.HasIndex(x => x.RequesterId);
        builder.HasIndex(x => x.State);
    }

    private sealed class TimeSpanToMinutesConverter : ValueConverter<TimeSpan, int>
    {
        public TimeSpanToMinutesConverter()
            : base(span => (int)Math.Round(span.TotalMinutes, MidpointRounding.AwayFromZero),
                   minutes => TimeSpan.FromMinutes(minutes))
        {
        }
    }
}
