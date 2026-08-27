using CommunityOS.Authorization.Application.Permissions;
using CommunityOS.Finance.Application.Permissions;

namespace CommunityOS.Finance.Tests.Permissions;

/// <summary>
/// Locks the permission boundary (ADR-032 decision 12): exactly six baseline
/// capabilities exist, each matching the shared <see cref="PermissionCatalog"/>
/// verbatim. Deferred capabilities must not be registered.
/// </summary>
public class FinancePermissionsTests
{
    [Fact]
    public void FinancePermissions_ShouldContainExactlySixPermissions()
    {
        FinancePermissions.All.Should().HaveCount(6);
    }

    [Fact]
    public void FinancePermissions_ShouldContainTheSixBaselineCapabilities()
    {
        FinancePermissions.All.Should().Contain(FinancePermissions.FundRead);
        FinancePermissions.All.Should().Contain(FinancePermissions.FundManage);
        FinancePermissions.All.Should().Contain(FinancePermissions.TransactionRead);
        FinancePermissions.All.Should().Contain(FinancePermissions.TransactionRecord);
        FinancePermissions.All.Should().Contain(FinancePermissions.TransactionApprove);
        FinancePermissions.All.Should().Contain(FinancePermissions.TransactionAdmin);
    }

    [Theory]
    [InlineData("finance.fund.read", nameof(PermissionCatalog.FinanceFundRead))]
    [InlineData("finance.fund.manage", nameof(PermissionCatalog.FinanceFundManage))]
    [InlineData("finance.transaction.read", nameof(PermissionCatalog.FinanceTransactionRead))]
    [InlineData("finance.transaction.record", nameof(PermissionCatalog.FinanceTransactionRecord))]
    [InlineData("finance.transaction.approve", nameof(PermissionCatalog.FinanceTransactionApprove))]
    [InlineData("finance.transaction.admin", nameof(PermissionCatalog.FinanceTransactionAdmin))]
    public void PermissionCatalog_ShouldMatchFinanceNames(string expected, string catalogMember)
    {
        typeof(PermissionCatalog).GetField(catalogMember)!.GetValue(null)
            .Should().Be(expected);
    }

    [Fact]
    public void FinancePermissions_ShouldMatchPermissionCatalog()
    {
        FinancePermissions.FundRead.Should().Be(PermissionCatalog.FinanceFundRead);
        FinancePermissions.FundManage.Should().Be(PermissionCatalog.FinanceFundManage);
        FinancePermissions.TransactionRead.Should().Be(PermissionCatalog.FinanceTransactionRead);
        FinancePermissions.TransactionRecord.Should().Be(PermissionCatalog.FinanceTransactionRecord);
        FinancePermissions.TransactionApprove.Should().Be(PermissionCatalog.FinanceTransactionApprove);
        FinancePermissions.TransactionAdmin.Should().Be(PermissionCatalog.FinanceTransactionAdmin);
    }

    [Fact]
    public void Deferred_capabilities_are_not_registered()
    {
        var allText = string.Join(" ", FinancePermissions.All);

        allText.Should().NotContain("budget");
        allText.Should().NotContain("wallet");
        allText.Should().NotContain("attribution");
    }
}