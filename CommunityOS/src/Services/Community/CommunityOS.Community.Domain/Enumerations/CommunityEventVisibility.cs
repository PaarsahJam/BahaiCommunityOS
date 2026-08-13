using CommunityOS.SharedKernel.Domain.Primitives;

namespace CommunityOS.Community.Domain.Enumerations;

/// <summary>
/// Visibility of a community event.
/// </summary>
public sealed class CommunityEventVisibility : Enumeration<int>
{
    public static readonly CommunityEventVisibility Public = new(1, "public");
    public static readonly CommunityEventVisibility Members = new(2, "members");
    public static readonly CommunityEventVisibility ParticipantsOnly = new(3, "participants_only");

    private CommunityEventVisibility(int id, string name) : base(id, name) { }

    public static IEnumerable<CommunityEventVisibility> All => [Public, Members, ParticipantsOnly];

    public static CommunityEventVisibility FromId(int id) =>
        All.FirstOrDefault(v => v.Id == id)
        ?? throw new ArgumentException($"Unknown CommunityEventVisibility id: {id}");

    public static CommunityEventVisibility FromName(string name) =>
        All.FirstOrDefault(v => string.Equals(v.Name, name, StringComparison.OrdinalIgnoreCase))
        ?? throw new ArgumentException($"Unknown CommunityEventVisibility name: {name}");
}
