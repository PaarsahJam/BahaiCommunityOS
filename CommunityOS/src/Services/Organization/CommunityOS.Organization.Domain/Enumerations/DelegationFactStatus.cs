using CommunityOS.SharedKernel.Domain.Primitives;

namespace CommunityOS.Organization.Domain.Enumerations;

/// <summary>
/// Lifecycle state of a delegation fact.
/// </summary>
public sealed class DelegationFactStatus : Enumeration<int>
{
    public static readonly DelegationFactStatus Active  = new(1, "Active");
    public static readonly DelegationFactStatus Revoked = new(2, "Revoked");

    private DelegationFactStatus(int id, string name) : base(id, name) { }

    public static IEnumerable<DelegationFactStatus> All => [Active, Revoked];

    public static DelegationFactStatus FromName(string name) =>
        All.FirstOrDefault(s => string.Equals(s.Name, name, StringComparison.OrdinalIgnoreCase))
        ?? throw new ArgumentException($"Unknown DelegationFactStatus name: {name}");

    public static DelegationFactStatus FromId(int id) =>
        All.FirstOrDefault(s => s.Id == id)
        ?? throw new ArgumentException($"Unknown DelegationFactStatus id: {id}");
}
