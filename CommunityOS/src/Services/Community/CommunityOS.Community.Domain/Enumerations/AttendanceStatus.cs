using CommunityOS.SharedKernel.Domain.Primitives;

namespace CommunityOS.Community.Domain.Enumerations;

/// <summary>
/// Attendance marking of a meeting participant.
/// </summary>
public sealed class AttendanceStatus : Enumeration<int>
{
    public static readonly AttendanceStatus NotMarked = new(1, "not_marked");
    public static readonly AttendanceStatus Attended = new(2, "attended");
    public static readonly AttendanceStatus Absent = new(3, "absent");

    private AttendanceStatus(int id, string name) : base(id, name) { }

    public static IEnumerable<AttendanceStatus> All => [NotMarked, Attended, Absent];

    public static AttendanceStatus FromId(int id) =>
        All.FirstOrDefault(s => s.Id == id)
        ?? throw new ArgumentException($"Unknown AttendanceStatus id: {id}");

    public static AttendanceStatus FromName(string name) =>
        All.FirstOrDefault(s => string.Equals(s.Name, name, StringComparison.OrdinalIgnoreCase))
        ?? throw new ArgumentException($"Unknown AttendanceStatus name: {name}");
}
