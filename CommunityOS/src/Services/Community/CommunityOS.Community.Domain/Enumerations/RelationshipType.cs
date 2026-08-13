using CommunityOS.SharedKernel.Domain.Primitives;

namespace CommunityOS.Community.Domain.Enumerations;

/// <summary>
/// Controlled family relationship types. Relationships are directional
/// (Parent → Child) and recorded on the Community side, never inside the
/// Organization hierarchy. Family relationships are sensitive and require
/// explicit authorization to read.
/// </summary>
public sealed class RelationshipType : Enumeration<int>
{
    public static readonly RelationshipType Spouse = new(1, "spouse");
    public static readonly RelationshipType Parent = new(2, "parent");
    public static readonly RelationshipType Child = new(3, "child");
    public static readonly RelationshipType Sibling = new(4, "sibling");
    public static readonly RelationshipType Grandparent = new(5, "grandparent");
    public static readonly RelationshipType Grandchild = new(6, "grandchild");
    public static readonly RelationshipType Guardian = new(7, "guardian");
    public static readonly RelationshipType Dependent = new(8, "dependent");
    public static readonly RelationshipType Other = new(9, "other");

    private RelationshipType(int id, string name) : base(id, name) { }

    public static IEnumerable<RelationshipType> All =>
        [Spouse, Parent, Child, Sibling, Grandparent, Grandchild, Guardian, Dependent, Other];

    public static RelationshipType FromId(int id) =>
        All.FirstOrDefault(r => r.Id == id)
        ?? throw new ArgumentException($"Unknown RelationshipType id: {id}");

    public static RelationshipType FromName(string name) =>
        All.FirstOrDefault(r => string.Equals(r.Name, name, StringComparison.OrdinalIgnoreCase))
        ?? throw new ArgumentException($"Unknown RelationshipType name: {name}");
}
