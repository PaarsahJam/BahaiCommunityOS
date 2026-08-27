using CommunityOS.Finance.Domain.Aggregates;
using CommunityOS.Finance.Domain.Enumerations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CommunityOS.Finance.Infrastructure.Persistence.Configurations;

internal sealed class FundConfiguration : IEntityTypeConfiguration<Fund>
{
    public void Configure(EntityTypeBuilder<Fund> builder)
    {
        builder.ToTable("funds");
        builder.HasKey(f => f.Id);
        builder.Property(f => f.Id).ValueGeneratedNever();

        builder.Property(f => f.OrganizationUnitId).HasColumnName("organization_unit_id").IsRequired();
        builder.Property(f => f.OrganizationUnitDisplayName).HasColumnName("organization_unit_display_name").HasMaxLength(200).IsRequired();
        builder.Property(f => f.Name).HasColumnName("name").HasMaxLength(200).IsRequired();
        builder.Property(f => f.Currency).HasColumnName("currency").HasMaxLength(3).IsRequired();
        builder.Property(f => f.Revision).HasColumnName("revision").IsRequired().IsConcurrencyToken();
        builder.Property(f => f.CreatedOn).HasColumnName("created_on").IsRequired();
        builder.Property(f => f.UpdatedOn).HasColumnName("updated_on").IsRequired();

        builder.Property(f => f.Status)
            .HasColumnName("status")
            .HasConversion(v => v.Id, id => FundStatus.FromId(id))
            .IsRequired();

        // The ledger is an append-only collection: transactions are persisted
        // in their own table (they are queried independently for status
        // transitions and balance derivation) and nothing on the fund side is
        // ever hard-deleted, so restrict rather than cascade.
        builder.HasMany(f => f.Transactions)
            .WithOne()
            .HasForeignKey(t => t.FundId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(f => f.OrganizationUnitId)
            .HasDatabaseName("ix_funds_organization_unit_id");
        builder.Ignore(f => f.DomainEvents);
    }
}

internal sealed class FinancialTransactionConfiguration : IEntityTypeConfiguration<FinancialTransaction>
{
    public void Configure(EntityTypeBuilder<FinancialTransaction> builder)
    {
        builder.ToTable("transactions");
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id).ValueGeneratedNever();

        builder.Property(t => t.FundId).HasColumnName("fund_id").IsRequired();
        builder.Property(t => t.OrganizationUnitId).HasColumnName("organization_unit_id");
        builder.Property(t => t.OrganizationUnitDisplayName).HasColumnName("organization_unit_display_name").HasMaxLength(200);
        builder.Property(t => t.TransferDestinationFundId).HasColumnName("transfer_destination_fund_id");
        builder.Property(t => t.Description).HasColumnName("description").HasMaxLength(500);
        builder.Property(t => t.RecordedBy).HasColumnName("recorded_by").IsRequired();
        builder.Property(t => t.OccurredOn).HasColumnName("occurred_on").IsRequired();
        builder.Property(t => t.SubmittedBy).HasColumnName("submitted_by");
        builder.Property(t => t.SubmittedOn).HasColumnName("submitted_on");
        builder.Property(t => t.ApprovedBy).HasColumnName("approved_by");
        builder.Property(t => t.ApprovedOn).HasColumnName("approved_on");
        builder.Property(t => t.RejectedBy).HasColumnName("rejected_by");
        builder.Property(t => t.RejectedOn).HasColumnName("rejected_on");
        builder.Property(t => t.RejectionReason).HasColumnName("rejection_reason").HasMaxLength(500);

        builder.Property(t => t.Type)
            .HasColumnName("type")
            .HasConversion(v => v.Id, id => FinancialTransactionType.FromId(id))
            .IsRequired();
        builder.Property(t => t.Direction)
            .HasColumnName("direction")
            .HasConversion(v => v.Id, id => FinancialTransactionDirection.FromId(id))
            .IsRequired();
        builder.Property(t => t.Status)
            .HasColumnName("status")
            .HasConversion(v => v.Id, id => FinancialTransactionStatus.FromId(id))
            .IsRequired();

        builder.OwnsOne(t => t.Amount, a =>
        {
            a.Property(m => m.Currency).HasColumnName("currency").HasMaxLength(3).IsRequired();
            a.Property(m => m.MinorUnits).HasColumnName("minor_units").IsRequired();
        });

        builder.HasIndex(t => t.FundId)
            .HasDatabaseName("ix_transactions_fund_id");
        builder.HasIndex(t => new { t.OrganizationUnitId, t.TransferDestinationFundId })
            .HasDatabaseName("ix_transactions_scope_transfer_destination");
        builder.HasIndex(t => t.Status)
            .HasDatabaseName("ix_transactions_status");
        builder.Ignore(t => t.DomainEvents);
    }
}

internal sealed class FinanceOrganizationUnitReferenceConfiguration
    : IEntityTypeConfiguration<OrganizationUnitReference>
{
    public void Configure(EntityTypeBuilder<OrganizationUnitReference> builder)
    {
        builder.ToTable("organization_unit_references");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).ValueGeneratedNever();

        builder.Property(r => r.OrganizationUnitId).HasColumnName("organization_unit_id").IsRequired();
        builder.Property(r => r.DisplayName).HasColumnName("display_name").HasMaxLength(200).IsRequired();
        builder.Property(r => r.CreatedOn).HasColumnName("created_on").IsRequired();
        builder.Ignore(r => r.DomainEvents);

        builder.HasIndex(r => r.OrganizationUnitId).IsUnique()
            .HasDatabaseName("ix_finance_organization_unit_references_unit_id");
    }
}