using CommunityOS.SharedKernel.Domain.Primitives;

namespace CommunityOS.Identity.Domain.Enumerations;

/// <summary>
/// Kind of authentication credential owned by a user account.
/// </summary>
public sealed class CredentialType : Enumeration<int>
{
    public static readonly CredentialType Password = new(1, "Password");
    public static readonly CredentialType Passkey = new(2, "Passkey");
    public static readonly CredentialType RecoveryCode = new(3, "RecoveryCode");

    private CredentialType(int id, string name) : base(id, name) { }

    public static IEnumerable<CredentialType> All => [Password, Passkey, RecoveryCode];

    public static CredentialType FromId(int id) =>
        All.FirstOrDefault(t => t.Id == id)
        ?? throw new ArgumentException($"Unknown CredentialType id: {id}");
}
