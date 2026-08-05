using CommunityOS.SharedKernel.Domain.Primitives;

namespace CommunityOS.Content.Domain.ValueObjects;

public sealed class ContentStatus : Enumeration<int>
{
    public static readonly ContentStatus Draft     = new(1, "Draft");
    public static readonly ContentStatus Published = new(2, "Published");
    public static readonly ContentStatus Archived  = new(3, "Archived");

    private ContentStatus(int id, string name) : base(id, name) { }

    public static IEnumerable<ContentStatus> All => [Draft, Published, Archived];

    public static ContentStatus FromId(int id) =>
        All.FirstOrDefault(s => s.Id == id)
        ?? throw new ArgumentException($"Unknown ContentStatus id: {id}");
}
