using CommunityOS.SharedKernel.Domain.Primitives;

namespace CommunityOS.Reporting.Domain.ValueObjects;

public sealed class ReportType : Enumeration<int>
{
    public static readonly ReportType MemberGrowth      = new(1, "MemberGrowth");
    public static readonly ReportType EventParticipation = new(2, "EventParticipation");
    public static readonly ReportType StudyCircleProgress = new(3, "StudyCircleProgress");
    public static readonly ReportType CommunityActivity  = new(4, "CommunityActivity");

    private ReportType(int id, string name) : base(id, name) { }

    public static IEnumerable<ReportType> All =>
        [MemberGrowth, EventParticipation, StudyCircleProgress, CommunityActivity];

    public static ReportType FromId(int id) =>
        All.FirstOrDefault(t => t.Id == id)
        ?? throw new ArgumentException($"Unknown ReportType id: {id}");
}
