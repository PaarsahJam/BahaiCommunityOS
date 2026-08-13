using CommunityOS.SharedKernel.Domain.Primitives;

namespace CommunityOS.Community.Domain.Enumerations;

/// <summary>
/// Visibility of a community activity, event or meeting.
/// </summary>
public sealed class ActivityVisibility : Enumeration<int>
{
    public static readonly ActivityVisibility Public = new(1, "public");
    public static readonly ActivityVisibility Members = new(2, "members");
    public static readonly ActivityVisibility ParticipantsOnly = new(3, "participants_only");

    private ActivityVisibility(int id, string name) : base(id, name) { }

    public static IEnumerable<ActivityVisibility> All => [Public, Members, ParticipantsOnly];

    public static ActivityVisibility FromId(int id) =>
        All.FirstOrDefault(v => v.Id == id)
        ?? throw new ArgumentException($"Unknown ActivityVisibility id: {id}");

    public static ActivityVisibility FromName(string name) =>
        All.FirstOrDefault(v => string.Equals(v.Name, name, StringComparison.OrdinalIgnoreCase))
        ?? throw new ArgumentException($"Unknown ActivityVisibility name: {name}");
}
