using CommunityOS.SharedKernel.Domain.Primitives;

namespace CommunityOS.Records.Domain.Enumerations;

/// <summary>
/// Lifecycle state of a record (ADR-023):
/// <c>Draft → Submitted → Under Review → Verified → Archived | Deactivated</c>,
/// plus <c>Rejected</c> (from <c>Under Review</c>). <c>Verified</c> is the
/// authoritative state. From <c>Verified</c> onward field changes require the
/// <c>correct</c> operation (a new superseding version), never in-place
/// mutation. <c>Deactivated</c> is a reversible soft-delete. <c>Rejected</c> is
/// a terminal state in the ratified lifecycle: there is no ratified resubmit
/// transition (a rejected fact must be re-created as a new Draft if needed).
/// </summary>
public sealed class RecordStatus : Enumeration<int>
{
    public static readonly RecordStatus Draft = new(1, "draft");
    public static readonly RecordStatus Submitted = new(2, "submitted");
    public static readonly RecordStatus UnderReview = new(3, "under_review");
    public static readonly RecordStatus Verified = new(4, "verified");
    public static readonly RecordStatus Archived = new(5, "archived");
    public static readonly RecordStatus Deactivated = new(6, "deactivated");
    public static readonly RecordStatus Rejected = new(7, "rejected");

    private RecordStatus(int id, string name) : base(id, name) { }

    public static IEnumerable<RecordStatus> All =>
        [Draft, Submitted, UnderReview, Verified, Archived, Deactivated, Rejected];

    public static RecordStatus FromId(int id) =>
        All.FirstOrDefault(s => s.Id == id)
        ?? throw new ArgumentException($"Unknown RecordStatus id: {id}");

    public static RecordStatus FromName(string name) =>
        All.FirstOrDefault(s => string.Equals(s.Name, name, StringComparison.OrdinalIgnoreCase))
        ?? throw new ArgumentException($"Unknown RecordStatus name: {name}");
}
