using CommunityOS.SharedKernel.Domain.Primitives;

namespace CommunityOS.Community.Domain.Enumerations;

/// <summary>
/// Kind of target a participation record refers to. A single participation
/// abstraction covers activity, event, meeting and volunteer/service
/// participation without duplicating models. A volunteer assignment is not an
/// organization appointment unless it is explicitly created as one.
/// </summary>
public sealed class ParticipationTargetType : Enumeration<int>
{
    public static readonly ParticipationTargetType Activity = new(1, "activity");
    public static readonly ParticipationTargetType Event = new(2, "event");
    public static readonly ParticipationTargetType Meeting = new(3, "meeting");
    public static readonly ParticipationTargetType VolunteerService = new(4, "volunteer_service");

    private ParticipationTargetType(int id, string name) : base(id, name) { }

    public static IEnumerable<ParticipationTargetType> All =>
        [Activity, Event, Meeting, VolunteerService];

    public static ParticipationTargetType FromId(int id) =>
        All.FirstOrDefault(t => t.Id == id)
        ?? throw new ArgumentException($"Unknown ParticipationTargetType id: {id}");

    public static ParticipationTargetType FromName(string name) =>
        All.FirstOrDefault(t => string.Equals(t.Name, name, StringComparison.OrdinalIgnoreCase))
        ?? throw new ArgumentException($"Unknown ParticipationTargetType name: {name}");
}
