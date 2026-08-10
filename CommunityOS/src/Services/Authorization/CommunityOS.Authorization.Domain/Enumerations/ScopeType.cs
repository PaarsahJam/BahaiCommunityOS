using CommunityOS.SharedKernel.Domain.Primitives;

namespace CommunityOS.Authorization.Domain.Enumerations;

/// <summary>
/// The kind of scope an authorization grant (role assignment, delegation,
/// break-glass request) is confined to. Scope is how authorization answers
/// "where does this person have authority?".
/// </summary>
public sealed class ScopeType : Enumeration<int>
{
    public static readonly ScopeType Global = new(1, "Global");
    public static readonly ScopeType National = new(2, "National");
    public static readonly ScopeType Regional = new(3, "Regional");
    public static readonly ScopeType Local = new(4, "Local");
    public static readonly ScopeType OrganizationUnit = new(5, "OrganizationUnit");
    public static readonly ScopeType Committee = new(6, "Committee");
    public static readonly ScopeType Resource = new(7, "Resource");

    private ScopeType(int id, string name) : base(id, name) { }

    public static IEnumerable<ScopeType> All => [Global, National, Regional, Local, OrganizationUnit, Committee, Resource];

    public static ScopeType FromId(int id) =>
        All.FirstOrDefault(t => t.Id == id)
        ?? throw new ArgumentException($"Unknown ScopeType id: {id}");
}
