using CommunityOS.SharedKernel.Domain.Primitives;

namespace CommunityOS.Community.Domain.Enumerations;

/// <summary>
/// Lifecycle state of a community activity.
/// </summary>
public sealed class ActivityStatus : Enumeration<int>
{
    public static readonly ActivityStatus Planned = new(1, "planned");
    public static readonly ActivityStatus Active = new(2, "active");
    public static readonly ActivityStatus Completed = new(3, "completed");
    public static readonly ActivityStatus Cancelled = new(4, "cancelled");

    private ActivityStatus(int id, string name) : base(id, name) { }

    public static IEnumerable<ActivityStatus> All => [Planned, Active, Completed, Cancelled];

    public static ActivityStatus FromId(int id) =>
        All.FirstOrDefault(s => s.Id == id)
        ?? throw new ArgumentException($"Unknown ActivityStatus id: {id}");

    public static ActivityStatus FromName(string name) =>
        All.FirstOrDefault(s => string.Equals(s.Name, name, StringComparison.OrdinalIgnoreCase))
        ?? throw new ArgumentException($"Unknown ActivityStatus name: {name}");
}
