using CommunityOS.SharedKernel.Domain.Primitives;

namespace CommunityOS.Knowledge.Domain.Enumerations;

/// <summary>
/// The literary type of a Library work (a book, tablet, or compilation).
/// </summary>
public sealed class WorkType : Enumeration<int>
{
    public static readonly WorkType Book = new(1, "book");
    public static readonly WorkType Tablet = new(2, "tablet");
    public static readonly WorkType Compilation = new(3, "compilation");

    private WorkType(int id, string name) : base(id, name) { }

    public static IEnumerable<WorkType> All => [Book, Tablet, Compilation];

    public static WorkType FromId(int id) =>
        All.FirstOrDefault(s => s.Id == id)
        ?? throw new ArgumentException($"Unknown WorkType id: {id}");

    public static WorkType FromName(string name) =>
        All.FirstOrDefault(s => string.Equals(s.Name, name, StringComparison.OrdinalIgnoreCase))
        ?? throw new ArgumentException($"Unknown WorkType name: {name}");
}