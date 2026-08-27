using CommunityOS.SharedKernel.Domain.Primitives;

namespace CommunityOS.Finance.Domain.Enumerations;

/// <summary>
/// The signed effect of a ledger entry relative to its fund
/// (ADR-032). Amounts are stored as positive magnitudes; the direction is
/// derived from the transaction type at creation and never accepted from a
/// client, so a reversal/correction appends a counter-direction entry rather
/// than a negative row.
/// </summary>
public sealed class FinancialTransactionDirection : Enumeration<int>
{
    public static readonly FinancialTransactionDirection Inflow = new(1, "inflow");
    public static readonly FinancialTransactionDirection Outflow = new(2, "outflow");

    private FinancialTransactionDirection(int id, string name) : base(id, name) { }

    public static IEnumerable<FinancialTransactionDirection> All => [Inflow, Outflow];

    public static FinancialTransactionDirection FromId(int id) =>
        All.FirstOrDefault(d => d.Id == id)
        ?? throw new ArgumentException($"Unknown FinancialTransactionDirection id: {id}");

    public static FinancialTransactionDirection FromName(string name) =>
        All.FirstOrDefault(d => string.Equals(d.Name, name, StringComparison.OrdinalIgnoreCase))
        ?? throw new ArgumentException($"Unknown FinancialTransactionDirection name: {name}");
}