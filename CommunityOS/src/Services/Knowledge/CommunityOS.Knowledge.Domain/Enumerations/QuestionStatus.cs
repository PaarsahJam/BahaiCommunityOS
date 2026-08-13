using CommunityOS.SharedKernel.Domain.Primitives;

namespace CommunityOS.Knowledge.Domain.Enumerations;

/// <summary>
/// Lifecycle state of a community question:
/// <c>Draft → Submitted → Published → Under Review → Merged | Archived</c>.
/// A <c>Canonicalized</c> question is the surviving target of one or more
/// merges; <c>Merged</c> and <c>Archived</c> are terminal.
/// </summary>
public sealed class QuestionStatus : Enumeration<int>
{
    public static readonly QuestionStatus Draft = new(1, "draft");
    public static readonly QuestionStatus Submitted = new(2, "submitted");
    public static readonly QuestionStatus Published = new(3, "published");
    public static readonly QuestionStatus UnderReview = new(4, "under_review");
    public static readonly QuestionStatus Merged = new(5, "merged");
    public static readonly QuestionStatus Archived = new(6, "archived");
    public static readonly QuestionStatus Canonicalized = new(7, "canonicalized");

    private QuestionStatus(int id, string name) : base(id, name) { }

    public static IEnumerable<QuestionStatus> All =>
        [Draft, Submitted, Published, UnderReview, Merged, Archived, Canonicalized];

    public bool IsTerminal => this == Merged || this == Archived;

    public static QuestionStatus FromId(int id) =>
        All.FirstOrDefault(s => s.Id == id)
        ?? throw new ArgumentException($"Unknown QuestionStatus id: {id}");

    public static QuestionStatus FromName(string name) =>
        All.FirstOrDefault(s => string.Equals(s.Name, name, StringComparison.OrdinalIgnoreCase))
        ?? throw new ArgumentException($"Unknown QuestionStatus name: {name}");
}