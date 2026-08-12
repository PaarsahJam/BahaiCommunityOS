using CommunityOS.SharedKernel.Domain.Primitives;

namespace CommunityOS.Organization.Domain.Enumerations;

/// <summary>
/// The geographic / administrative scope an organization, committee or unit
/// operates within. Mirrors the scope taxonomy of the Authorization service so
/// organization facts can participate in scope-aware authorization.
/// </summary>
public sealed class JurisdictionType : Enumeration<int>
{
    public static readonly JurisdictionType Global            = new(1, "Global");
    public static readonly JurisdictionType National          = new(2, "National");
    public static readonly JurisdictionType Regional          = new(3, "Regional");
    public static readonly JurisdictionType Local             = new(4, "Local");
    public static readonly JurisdictionType OrganizationUnit  = new(5, "OrganizationUnit");
    public static readonly JurisdictionType Committee         = new(6, "Committee");

    private JurisdictionType(int id, string name) : base(id, name) { }

    public static IEnumerable<JurisdictionType> All =>
        [Global, National, Regional, Local, OrganizationUnit, Committee];

    public static JurisdictionType FromName(string name) =>
        All.FirstOrDefault(j => string.Equals(j.Name, name, StringComparison.OrdinalIgnoreCase))
        ?? throw new ArgumentException($"Unknown JurisdictionType name: {name}");

    public static JurisdictionType FromId(int id) =>
        All.FirstOrDefault(j => j.Id == id)
        ?? throw new ArgumentException($"Unknown JurisdictionType id: {id}");
}
