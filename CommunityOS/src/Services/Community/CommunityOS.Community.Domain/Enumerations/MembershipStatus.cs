using CommunityOS.SharedKernel.Domain.Primitives;

namespace CommunityOS.Community.Domain.Enumerations;

/// <summary>
/// Lifecycle state of a community membership. Membership is a Community
/// concern and is never expressed as an authorization role or an organization
/// appointment.
/// </summary>
public sealed class MembershipStatus : Enumeration<int>
{
    public static readonly MembershipStatus Pending = new(1, "pending");
    public static readonly MembershipStatus Active = new(2, "active");
    public static readonly MembershipStatus Suspended = new(3, "suspended");
    public static readonly MembershipStatus Lapsed = new(4, "lapsed");
    public static readonly MembershipStatus Withdrawn = new(5, "withdrawn");

    private MembershipStatus(int id, string name) : base(id, name) { }

    public static IEnumerable<MembershipStatus> All => [Pending, Active, Suspended, Lapsed, Withdrawn];

    public static MembershipStatus FromId(int id) =>
        All.FirstOrDefault(s => s.Id == id)
        ?? throw new ArgumentException($"Unknown MembershipStatus id: {id}");

    public static MembershipStatus FromName(string name) =>
        All.FirstOrDefault(s => string.Equals(s.Name, name, StringComparison.OrdinalIgnoreCase))
        ?? throw new ArgumentException($"Unknown MembershipStatus name: {name}");
}
