using CommunityOS.SharedKernel.Domain.Primitives;

namespace CommunityOS.Enrollment.Domain.ValueObjects;

public sealed class ProgressStatus : Enumeration<int>
{
    public static readonly ProgressStatus NotStarted  = new(1, "NotStarted");
    public static readonly ProgressStatus InProgress  = new(2, "InProgress");
    public static readonly ProgressStatus Completed   = new(3, "Completed");
    public static readonly ProgressStatus Withdrawn   = new(4, "Withdrawn");

    private ProgressStatus(int id, string name) : base(id, name) { }

    public static IEnumerable<ProgressStatus> All => [NotStarted, InProgress, Completed, Withdrawn];

    public static ProgressStatus FromId(int id) =>
        All.FirstOrDefault(s => s.Id == id)
        ?? throw new ArgumentException($"Unknown ProgressStatus id: {id}");
}
