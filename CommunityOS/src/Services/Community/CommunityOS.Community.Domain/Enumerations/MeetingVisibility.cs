using CommunityOS.SharedKernel.Domain.Primitives;

namespace CommunityOS.Community.Domain.Enumerations;

/// <summary>
/// Visibility / access level of a meeting.
/// </summary>
public sealed class MeetingVisibility : Enumeration<int>
{
    public static readonly MeetingVisibility Public = new(1, "public");
    public static readonly MeetingVisibility Members = new(2, "members");
    public static readonly MeetingVisibility ParticipantsOnly = new(3, "participants_only");

    private MeetingVisibility(int id, string name) : base(id, name) { }

    public static IEnumerable<MeetingVisibility> All => [Public, Members, ParticipantsOnly];

    public static MeetingVisibility FromId(int id) =>
        All.FirstOrDefault(v => v.Id == id)
        ?? throw new ArgumentException($"Unknown MeetingVisibility id: {id}");

    public static MeetingVisibility FromName(string name) =>
        All.FirstOrDefault(v => string.Equals(v.Name, name, StringComparison.OrdinalIgnoreCase))
        ?? throw new ArgumentException($"Unknown MeetingVisibility name: {name}");
}
