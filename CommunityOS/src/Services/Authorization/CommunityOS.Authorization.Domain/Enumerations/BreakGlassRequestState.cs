using CommunityOS.SharedKernel.Domain.Primitives;

namespace CommunityOS.Authorization.Domain.Enumerations;

/// <summary>
/// Lifecycle of a break-glass request. Break-glass access is an explicit,
/// time-limited, narrowly-scoped emergency grant that is always audited.
/// </summary>
public sealed class BreakGlassRequestState : Enumeration<int>
{
    public static readonly BreakGlassRequestState Requested = new(1, "Requested");
    public static readonly BreakGlassRequestState Approved = new(2, "Approved");
    public static readonly BreakGlassRequestState Rejected = new(3, "Rejected");
    public static readonly BreakGlassRequestState Revoked = new(4, "Revoked");
    public static readonly BreakGlassRequestState Expired = new(5, "Expired");

    private BreakGlassRequestState(int id, string name) : base(id, name) { }

    public static IEnumerable<BreakGlassRequestState> All =>
        [Requested, Approved, Rejected, Revoked, Expired];

    public static BreakGlassRequestState FromId(int id) =>
        All.FirstOrDefault(t => t.Id == id)
        ?? throw new ArgumentException($"Unknown BreakGlassRequestState id: {id}");
}
