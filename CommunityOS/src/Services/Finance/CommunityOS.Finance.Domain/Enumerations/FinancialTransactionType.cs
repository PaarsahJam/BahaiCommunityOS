using CommunityOS.SharedKernel.Domain.Primitives;

namespace CommunityOS.Finance.Domain.Enumerations;

/// <summary>
/// Kind of a ledger entry (ADR-032). The direction is attached to the type:
/// <c>contribution</c> is money in; <c>expense</c>, <c>reimbursement</c> and
/// <c>disbursement</c> are money out; <c>transfer</c> is money out of the
/// source fund (the destination fund reference receives the inflow). The set
/// is closed in the ratified first gate.
/// </summary>
public sealed class FinancialTransactionType : Enumeration<int>
{
    public static readonly FinancialTransactionType Contribution = new(1, "contribution", FinancialTransactionDirection.Inflow);
    public static readonly FinancialTransactionType Expense = new(2, "expense", FinancialTransactionDirection.Outflow);
    public static readonly FinancialTransactionType Reimbursement = new(3, "reimbursement", FinancialTransactionDirection.Outflow);
    public static readonly FinancialTransactionType Disbursement = new(4, "disbursement", FinancialTransactionDirection.Outflow);
    public static readonly FinancialTransactionType Transfer = new(5, "transfer", FinancialTransactionDirection.Outflow);

    private FinancialTransactionType(int id, string name, FinancialTransactionDirection direction)
        : base(id, name)
    {
        Direction = direction;
    }

    public FinancialTransactionDirection Direction { get; }

    public static IEnumerable<FinancialTransactionType> All =>
        [Contribution, Expense, Reimbursement, Disbursement, Transfer];

    public static FinancialTransactionType FromId(int id) =>
        All.FirstOrDefault(t => t.Id == id)
        ?? throw new ArgumentException($"Unknown FinancialTransactionType id: {id}");

    public static FinancialTransactionType FromName(string name) =>
        All.FirstOrDefault(t => string.Equals(t.Name, name, StringComparison.OrdinalIgnoreCase))
        ?? throw new ArgumentException($"Unknown FinancialTransactionType name: {name}");

    public static bool IsKnown(string? name) =>
        name is not null &&
        All.Any(t => string.Equals(t.Name, name, StringComparison.OrdinalIgnoreCase));
}