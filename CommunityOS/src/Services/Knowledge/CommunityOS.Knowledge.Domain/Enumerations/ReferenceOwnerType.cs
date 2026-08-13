using CommunityOS.SharedKernel.Domain.Primitives;

namespace CommunityOS.Knowledge.Domain.Enumerations;

/// <summary>
/// The type of community content a Library reference is attached to.
/// </summary>
public sealed class ReferenceOwnerType : Enumeration<int>
{
    public static readonly ReferenceOwnerType Question = new(1, "question");
    public static readonly ReferenceOwnerType Answer = new(2, "answer");
    public static readonly ReferenceOwnerType Comment = new(3, "comment");
    public static readonly ReferenceOwnerType Discussion = new(4, "discussion");

    private ReferenceOwnerType(int id, string name) : base(id, name) { }

    public static IEnumerable<ReferenceOwnerType> All =>
        [Question, Answer, Comment, Discussion];

    public static ReferenceOwnerType FromId(int id) =>
        All.FirstOrDefault(s => s.Id == id)
        ?? throw new ArgumentException($"Unknown ReferenceOwnerType id: {id}");

    public static ReferenceOwnerType FromName(string name) =>
        All.FirstOrDefault(s => string.Equals(s.Name, name, StringComparison.OrdinalIgnoreCase))
        ?? throw new ArgumentException($"Unknown ReferenceOwnerType name: {name}");
}