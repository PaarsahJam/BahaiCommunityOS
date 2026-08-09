using CommunityOS.SharedKernel.Domain.Primitives;

namespace CommunityOS.Identity.Domain.Enumerations;

/// <summary>
/// Lifecycle state of a <see cref="Aggregates.UserAccount"/>.
/// </summary>
public sealed class AccountStatus : Enumeration<int>
{
    public static readonly AccountStatus PendingVerification = new(1, "PendingVerification");
    public static readonly AccountStatus Active = new(2, "Active");
    public static readonly AccountStatus Locked = new(3, "Locked");
    public static readonly AccountStatus Deactivated = new(4, "Deactivated");

    private AccountStatus(int id, string name) : base(id, name) { }

    public static IEnumerable<AccountStatus> All =>
        [PendingVerification, Active, Locked, Deactivated];

    public static AccountStatus FromId(int id) =>
        All.FirstOrDefault(s => s.Id == id)
        ?? throw new ArgumentException($"Unknown AccountStatus id: {id}");
}
