using CommunityOS.SharedKernel.Domain.Events;

namespace CommunityOS.Finance.Domain.Events;

/// <summary>
/// Raised when a ledger entry is recorded (ADR-032). This is the only finance
/// domain event in the ratified first gate: approve/reject and fund lifecycle
/// changes raise nothing, so the outbox exports exactly one contract —
/// <c>FinanceTransactionRecorded</c> — that carries no financial figures.
/// </summary>
public sealed record FinanceTransactionRecordedEvent(
    Guid TransactionId,
    Guid FundId,
    string TransactionType,
    string Direction,
    string Status,
    Guid RecordedBy) : DomainEvent;