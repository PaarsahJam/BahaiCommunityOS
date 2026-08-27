using CommunityOS.Finance.Application.DTOs;
using CommunityOS.Finance.Domain.Aggregates;

namespace CommunityOS.Finance.Application;

internal static class FinanceMappingExtensions
{
    internal static FundSummaryDto ToSummaryDto(this Fund fund, long balanceMinorUnits) =>
        new(fund.Id,
            fund.OrganizationUnitId,
            fund.OrganizationUnitDisplayName,
            fund.Name,
            fund.Currency,
            fund.Status.Name,
            balanceMinorUnits,
            fund.UpdatedOn);

    internal static FundDto ToDto(this Fund fund, long balanceMinorUnits) =>
        new(fund.Id,
            fund.OrganizationUnitId,
            fund.OrganizationUnitDisplayName,
            fund.Name,
            fund.Currency,
            fund.Status.Name,
            fund.Revision,
            balanceMinorUnits,
            fund.CreatedOn,
            fund.UpdatedOn);

    internal static FinancialTransactionDto ToDto(this FinancialTransaction tx) =>
        new(tx.Id,
            tx.FundId,
            tx.OrganizationUnitId ?? Guid.Empty,
            tx.OrganizationUnitDisplayName,
            new MoneyDto(tx.Amount.Currency, tx.Amount.MinorUnits),
            tx.Type.Name,
            tx.Direction.Name,
            tx.Status.Name,
            string.IsNullOrWhiteSpace(tx.Description) ? null : tx.Description,
            tx.TransferDestinationFundId,
            tx.RecordedBy,
            tx.OccurredOn,
            tx.SubmittedBy,
            tx.SubmittedOn,
            tx.ApprovedBy,
            tx.ApprovedOn,
            tx.RejectedBy,
            tx.RejectedOn,
            tx.RejectionReason);
}