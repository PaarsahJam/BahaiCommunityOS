namespace CommunityOS.Contracts.Finance;

/// <summary>
/// Raised when a financial transaction is recorded into the ledger
/// (ADR-032). Carries only stable identifiers and lifecycle metadata —
/// never amounts, currencies, categories, descriptions, confidential donor
/// attribution or transfer destinations. Consumers that need figures resolve
/// them through the Finance API on demand; the event exists to drive
/// projections and alerts, not to carry financial data.
/// </summary>
public sealed record FinanceTransactionRecorded(
    Guid TransactionId,
    Guid FundId,
    string TransactionType,
    string Direction,
    string Status,
    Guid RecordedBy,
    DateTime OccurredOn);