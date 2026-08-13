using CommunityOS.SharedKernel.Domain.Primitives;

namespace CommunityOS.Community.Domain.Enumerations;

/// <summary>
/// Lifecycle state of a community event.
/// </summary>
public sealed class CommunityEventStatus : Enumeration<int>
{
    public static readonly CommunityEventStatus Scheduled = new(1, "scheduled");
    public static readonly CommunityEventStatus Confirmed = new(2, "confirmed");
    public static readonly CommunityEventStatus Cancelled = new(3, "cancelled");
    public static readonly CommunityEventStatus Completed = new(4, "completed");

    private CommunityEventStatus(int id, string name) : base(id, name) { }

    public static IEnumerable<CommunityEventStatus> All => [Scheduled, Confirmed, Cancelled, Completed];

    public static CommunityEventStatus FromId(int id) =>
        All.FirstOrDefault(s => s.Id == id)
        ?? throw new ArgumentException($"Unknown CommunityEventStatus id: {id}");

    public static CommunityEventStatus FromName(string name) =>
        All.FirstOrDefault(s => string.Equals(s.Name, name, StringComparison.OrdinalIgnoreCase))
        ?? throw new ArgumentException($"Unknown CommunityEventStatus name: {name}");
}
