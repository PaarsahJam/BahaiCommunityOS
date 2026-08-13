using CommunityOS.SharedKernel.Domain.Primitives;

namespace CommunityOS.Community.Domain.Enumerations;

/// <summary>
/// Status of a participation record.
/// </summary>
public sealed class ParticipationStatus : Enumeration<int>
{
    public static readonly ParticipationStatus Registered = new(1, "registered");
    public static readonly ParticipationStatus Attended = new(2, "attended");
    public static readonly ParticipationStatus Cancelled = new(3, "cancelled");

    private ParticipationStatus(int id, string name) : base(id, name) { }

    public static IEnumerable<ParticipationStatus> All => [Registered, Attended, Cancelled];

    public static ParticipationStatus FromId(int id) =>
        All.FirstOrDefault(s => s.Id == id)
        ?? throw new ArgumentException($"Unknown ParticipationStatus id: {id}");

    public static ParticipationStatus FromName(string name) =>
        All.FirstOrDefault(s => string.Equals(s.Name, name, StringComparison.OrdinalIgnoreCase))
        ?? throw new ArgumentException($"Unknown ParticipationStatus name: {name}");
}
