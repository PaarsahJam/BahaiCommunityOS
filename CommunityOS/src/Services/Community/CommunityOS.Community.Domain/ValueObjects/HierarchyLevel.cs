using CommunityOS.SharedKernel.Domain.Primitives;

namespace CommunityOS.Community.Domain.ValueObjects;

public sealed class HierarchyLevel : Enumeration<int>
{
    public static readonly HierarchyLevel NationalSpiritual  = new(1, "National Spiritual Assembly");
    public static readonly HierarchyLevel Regional           = new(2, "Regional Council");
    public static readonly HierarchyLevel Cluster            = new(3, "Cluster");
    public static readonly HierarchyLevel LocalUnit          = new(4, "Local Unit");

    private HierarchyLevel(int id, string name) : base(id, name) { }

    public static IEnumerable<HierarchyLevel> All =>
        [NationalSpiritual, Regional, Cluster, LocalUnit];

    public static HierarchyLevel FromId(int id) =>
        All.FirstOrDefault(l => l.Id == id)
        ?? throw new ArgumentException($"Unknown HierarchyLevel id: {id}");
}
