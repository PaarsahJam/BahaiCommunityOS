using CommunityOS.SharedKernel.Domain.Primitives;

namespace CommunityOS.Knowledge.Domain.Enumerations;

/// <summary>
/// Moderation state of a discussion thread. <c>Deleted</c> is terminal.
/// </summary>
public sealed class DiscussionStatus : Enumeration<int>
{
    public static readonly DiscussionStatus Active = new(1, "active");
    public static readonly DiscussionStatus Hidden = new(2, "hidden");
    public static readonly DiscussionStatus Deleted = new(3, "deleted");

    private DiscussionStatus(int id, string name) : base(id, name) { }

    public static IEnumerable<DiscussionStatus> All => [Active, Hidden, Deleted];

    public static DiscussionStatus FromId(int id) =>
        All.FirstOrDefault(s => s.Id == id)
        ?? throw new ArgumentException($"Unknown DiscussionStatus id: {id}");

    public static DiscussionStatus FromName(string name) =>
        All.FirstOrDefault(s => string.Equals(s.Name, name, StringComparison.OrdinalIgnoreCase))
        ?? throw new ArgumentException($"Unknown DiscussionStatus name: {name}");
}