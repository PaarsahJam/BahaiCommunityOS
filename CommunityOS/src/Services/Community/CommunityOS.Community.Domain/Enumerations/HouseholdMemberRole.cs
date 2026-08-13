using CommunityOS.SharedKernel.Domain.Primitives;

namespace CommunityOS.Community.Domain.Enumerations;

/// <summary>
/// Role of a person within a household. A household is a cohabitation unit,
/// which is distinct from a family (members need not be related and need not
/// share an address).
/// </summary>
public sealed class HouseholdMemberRole : Enumeration<int>
{
    public static readonly HouseholdMemberRole Head = new(1, "head");
    public static readonly HouseholdMemberRole Adult = new(2, "adult");
    public static readonly HouseholdMemberRole Child = new(3, "child");
    public static readonly HouseholdMemberRole Dependent = new(4, "dependent");

    private HouseholdMemberRole(int id, string name) : base(id, name) { }

    public static IEnumerable<HouseholdMemberRole> All => [Head, Adult, Child, Dependent];

    public static HouseholdMemberRole FromId(int id) =>
        All.FirstOrDefault(r => r.Id == id)
        ?? throw new ArgumentException($"Unknown HouseholdMemberRole id: {id}");

    public static HouseholdMemberRole FromName(string name) =>
        All.FirstOrDefault(r => string.Equals(r.Name, name, StringComparison.OrdinalIgnoreCase))
        ?? throw new ArgumentException($"Unknown HouseholdMemberRole name: {name}");
}
