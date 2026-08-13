using CommunityOS.SharedKernel.Domain.Primitives;

namespace CommunityOS.Community.Domain.Enumerations;

/// <summary>
/// Lifecycle state of a meeting.
/// </summary>
public sealed class MeetingStatus : Enumeration<int>
{
    public static readonly MeetingStatus Scheduled = new(1, "scheduled");
    public static readonly MeetingStatus InProgress = new(2, "in_progress");
    public static readonly MeetingStatus Completed = new(3, "completed");
    public static readonly MeetingStatus Cancelled = new(4, "cancelled");

    private MeetingStatus(int id, string name) : base(id, name) { }

    public static IEnumerable<MeetingStatus> All => [Scheduled, InProgress, Completed, Cancelled];

    public static MeetingStatus FromId(int id) =>
        All.FirstOrDefault(s => s.Id == id)
        ?? throw new ArgumentException($"Unknown MeetingStatus id: {id}");

    public static MeetingStatus FromName(string name) =>
        All.FirstOrDefault(s => string.Equals(s.Name, name, StringComparison.OrdinalIgnoreCase))
        ?? throw new ArgumentException($"Unknown MeetingStatus name: {name}");
}
