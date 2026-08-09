using CommunityOS.SharedKernel.Domain.Primitives;

namespace CommunityOS.Identity.Domain.Enumerations;

/// <summary>
/// Supported multi-factor authentication mechanisms.
/// </summary>
public sealed class MfaMethodType : Enumeration<int>
{
    public static readonly MfaMethodType AuthenticatorApp = new(1, "AuthenticatorApp");
    public static readonly MfaMethodType Sms = new(2, "Sms");
    public static readonly MfaMethodType Email = new(3, "Email");

    private MfaMethodType(int id, string name) : base(id, name) { }

    public static IEnumerable<MfaMethodType> All => [AuthenticatorApp, Sms, Email];

    public static MfaMethodType FromId(int id) =>
        All.FirstOrDefault(t => t.Id == id)
        ?? throw new ArgumentException($"Unknown MfaMethodType id: {id}");
}
