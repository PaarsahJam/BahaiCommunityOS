using CommunityOS.SharedKernel.Domain.Primitives;

namespace CommunityOS.Finance.Domain.Enumerations;

/// <summary>
/// Lifecycle state of a fund (ADR-032): <c>Active</c> or <c>Closed</c>.
/// A closed fund preserves its ledger (append-only entries are never removed)
/// but rejects new transactions and cannot be reopened in the ratified first
/// gate.
/// </summary>
public sealed class FundStatus : Enumeration<int>
{
    public static readonly FundStatus Active = new(1, "active");
    public static readonly FundStatus Closed = new(2, "closed");

    private FundStatus(int id, string name) : base(id, name) { }

    public static IEnumerable<FundStatus> All => [Active, Closed];

    public static FundStatus FromId(int id) =>
        All.FirstOrDefault(s => s.Id == id)
        ?? throw new ArgumentException($"Unknown FundStatus id: {id}");

    public static FundStatus FromName(string name) =>
        All.FirstOrDefault(s => string.Equals(s.Name, name, StringComparison.OrdinalIgnoreCase))
        ?? throw new ArgumentException($"Unknown FundStatus name: {name}");
}