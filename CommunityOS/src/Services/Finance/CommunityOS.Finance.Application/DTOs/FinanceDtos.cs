namespace CommunityOS.Finance.Application.DTOs;

public sealed record MoneyDto(string Currency, long MinorUnits);

public sealed record FundSummaryDto(
    Guid Id,
    Guid OrganizationUnitId,
    string OrganizationUnitDisplayName,
    string Name,
    string Currency,
    string Status,
    long BalanceMinorUnits,
    DateTime UpdatedOn);

public sealed record FundDto(
    Guid Id,
    Guid OrganizationUnitId,
    string OrganizationUnitDisplayName,
    string Name,
    string Currency,
    string Status,
    int Revision,
    long BalanceMinorUnits,
    DateTime CreatedOn,
    DateTime UpdatedOn);

public sealed record FundBalanceDto(string Currency, long BalanceMinorUnits);

public sealed record FinancialTransactionDto(
    Guid Id,
    Guid FundId,
    Guid OrganizationUnitId,
    string OrganizationUnitDisplayName,
    MoneyDto Amount,
    string Type,
    string Direction,
    string Status,
    string? Description,
    Guid? TransferDestinationFundId,
    Guid RecordedBy,
    DateTime OccurredOn,
    Guid? SubmittedBy,
    DateTime? SubmittedOn,
    Guid? ApprovedBy,
    DateTime? ApprovedOn,
    Guid? RejectedBy,
    DateTime? RejectedOn,
    string? RejectionReason);