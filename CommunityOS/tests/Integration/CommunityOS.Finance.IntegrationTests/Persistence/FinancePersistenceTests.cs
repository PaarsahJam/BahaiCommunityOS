using CommunityOS.Finance.Domain.Aggregates;
using CommunityOS.Finance.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Xunit;

namespace CommunityOS.Finance.IntegrationTests.Persistence;

/// <summary>
/// Persistence integration tests for the Finance bounded context (ADR-032).
/// These assert the full <c>finance</c> schema mapping in the EF Core model
/// built offline from the configuration assemblies - table names, columns,
/// value conversions, delete behavior and the MassTransit outbox contract
/// committed atomically with the ledger (ADR-015 at the Finance gate).
/// </summary>
/// <remarks>
/// No Docker or live database is required: the relational model is produced
/// solely from the entity configurations applied in
/// <see cref="FinanceDbContext.OnModelCreating"/>.
/// </remarks>
public class FinancePersistenceTests
{
    private static FinanceDbContext CreateOfflineContext()
    {
        var options = new DbContextOptionsBuilder<FinanceDbContext>()
            .UseNpgsql(
                "Host=localhost;Port=5432;Database=communityos_finance;Username=communityos;Password=communityos",
                npgsql => npgsql.MigrationsAssembly("CommunityOS.Finance.Infrastructure"))
            .Options;

        return new FinanceDbContext(options);
    }

    private static IEntityType ModelEntity(FinanceDbContext db, Type clrType) =>
        db.Model.FindEntityType(clrType) ?? throw new InvalidOperationException(
            $"No entity mapped for {clrType.Name}.");

    [Fact]
    public void Context_uses_the_finance_schema_and_owns_the_mass_transit_outbox()
    {
        using var db = CreateOfflineContext();

        db.Model.GetRelationalModel();

        db.Model.GetDefaultSchema().Should().Be("finance");

        var outbox = db.Model.GetEntityTypes()
            .Where(e => e.ClrType.FullName?.StartsWith("MassTransit.", StringComparison.Ordinal) == true)
            .ToList();
        outbox.Should().NotBeEmpty();
        outbox.Should().OnlyContain(e => e.GetSchema() == "finance");
    }

    [Fact]
    public void Fund_maps_to_the_funds_table_with_conversion_and_restrict_delete()
    {
        using var db = CreateOfflineContext();
        db.Model.GetRelationalModel();

        var fund = ModelEntity(db, typeof(Fund));

        fund.GetSchema().Should().Be("finance");
        fund.GetTableName().Should().Be("funds");
        fund.GetProperty(nameof(Fund.OrganizationUnitId)).GetColumnName().Should().Be("organization_unit_id");
        fund.GetProperty(nameof(Fund.Name)).GetColumnName().Should().Be("name");
        fund.GetProperty(nameof(Fund.Currency)).GetColumnName().Should().Be("currency");
        fund.GetProperty(nameof(Fund.Revision)).IsConcurrencyToken.Should().BeTrue();
        fund.GetProperty(nameof(Fund.Status)).GetValueConverter().Should().NotBeNull();

        var ledger = fund.GetNavigations().Single(n => n.Name == nameof(Fund.Transactions));
        ledger.ForeignKey.DeleteBehavior.Should().Be(DeleteBehavior.Restrict);
    }

    [Fact]
    public void FinancialTransaction_maps_money_and_transfer_reference()
    {
        using var db = CreateOfflineContext();
        db.Model.GetRelationalModel();

        var tx = ModelEntity(db, typeof(FinancialTransaction));

        tx.GetSchema().Should().Be("finance");
        tx.GetTableName().Should().Be("transactions");
        tx.GetProperty(nameof(FinancialTransaction.FundId)).GetColumnName().Should().Be("fund_id");
        tx.GetProperty(nameof(FinancialTransaction.TransferDestinationFundId)).GetColumnName()
            .Should().Be("transfer_destination_fund_id");
        tx.GetProperty(nameof(FinancialTransaction.Type)).GetValueConverter().Should().NotBeNull();
        tx.GetProperty(nameof(FinancialTransaction.Direction)).GetValueConverter().Should().NotBeNull();
        tx.GetProperty(nameof(FinancialTransaction.Status)).GetValueConverter().Should().NotBeNull();

        var amount = tx.FindNavigation(nameof(FinancialTransaction.Amount));
        amount.Should().NotBeNull();
        amount!.TargetEntityType!.IsOwned().Should().BeTrue();
        amount.TargetEntityType.GetProperty("Currency").GetColumnName().Should().Be("currency");
        amount.TargetEntityType.GetProperty("MinorUnits").GetColumnName().Should().Be("minor_units");
    }

    [Fact]
    public void OrganizationUnitReference_maps_uniquely_per_organization_unit()
    {
        using var db = CreateOfflineContext();
        db.Model.GetRelationalModel();

        var unit = ModelEntity(db, typeof(OrganizationUnitReference));

        unit.GetSchema().Should().Be("finance");
        unit.GetTableName().Should().Be("organization_unit_references");

        var uniqueIndex = unit.GetIndexes().Single(i => i.IsUnique);
        uniqueIndex.Properties.Select(p => p.Name).Should().Contain(nameof(OrganizationUnitReference.OrganizationUnitId));
    }
}