using CommunityOS.SharedKernel.Domain.Primitives;

namespace CommunityOS.Finance.Domain.Enumerations;

/// <summary>
/// Approval state of a ledger entry (ADR-032):
/// <c>Recorded → Pending Approval → Approved | Rejected</c>. Approved entries
/// are the only rows that contribute to the derived fund balance. <c>Rejected</c>
/// is terminal: a rejected entry is never re-approved (a new entry is recorded
/// instead). The entry's content is immutable once created; only the status
/// transitions.
/// </summary>
public sealed class FinancialTransactionStatus : Enumeration<int>
{
    public static readonly FinancialTransactionStatus Recorded = new(1, "recorded");
    public static readonly FinancialTransactionStatus PendingApproval = new(2, "pending_approval");
    public static readonly FinancialTransactionStatus Approved = new(3, "approved");
    public static readonly FinancialTransactionStatus Rejected = new(4, "rejected");

    private FinancialTransactionStatus(int id, string name) : base(id, name) { }

    public static IEnumerable<FinancialTransactionStatus> All =>
        [Recorded, PendingApproval, Approved, Rejected];

    public static FinancialTransactionStatus FromId(int id) =>
        All.FirstOrDefault(s => s.Id == id)
        ?? throw new ArgumentException($"Unknown FinancialTransactionStatus id: {id}");

    public static FinancialTransactionStatus FromName(string name) =>
        All.FirstOrDefault(s => string.Equals(s.Name, name, StringComparison.OrdinalIgnoreCase))
        ?? throw new ArgumentException($"Unknown FinancialTransactionStatus name: {name}");
}