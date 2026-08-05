using CommunityOS.SharedKernel.Domain.Primitives;

namespace CommunityOS.Identity.Domain.ValueObjects;

public sealed class MembershipStatus : Enumeration<int>
{
    public static readonly MembershipStatus Active      = new(1, "Active");
    public static readonly MembershipStatus Inactive    = new(2, "Inactive");
    public static readonly MembershipStatus Pending     = new(3, "Pending");
    public static readonly MembershipStatus Suspended   = new(4, "Suspended");
    public static readonly MembershipStatus Transferred = new(5, "Transferred");

    private MembershipStatus(int id, string name) : base(id, name) { }

    public static IEnumerable<MembershipStatus> All =>
        [Active, Inactive, Pending, Suspended, Transferred];

    public static MembershipStatus FromId(int id) =>
        All.FirstOrDefault(s => s.Id == id)
        ?? throw new ArgumentException($"Unknown MembershipStatus id: {id}");
}
