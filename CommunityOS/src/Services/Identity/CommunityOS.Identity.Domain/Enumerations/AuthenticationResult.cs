using CommunityOS.SharedKernel.Domain.Primitives;

namespace CommunityOS.Identity.Domain.Enumerations;

/// <summary>
/// Result of an authentication attempt used to drive security event reporting.
/// </summary>
public sealed class AuthenticationResult : Enumeration<int>
{
    public static readonly AuthenticationResult Succeeded = new(1, "Succeeded");
    public static readonly AuthenticationResult InvalidCredentials = new(2, "InvalidCredentials");
    public static readonly AuthenticationResult LockedOut = new(3, "LockedOut");
    public static readonly AuthenticationResult MfaRequired = new(4, "MfaRequired");
    public static readonly AuthenticationResult MfaFailed = new(5, "MfaFailed");
    public static readonly AuthenticationResult NotVerified = new(6, "NotVerified");
    public static readonly AuthenticationResult Deactivated = new(7, "Deactivated");

    private AuthenticationResult(int id, string name) : base(id, name) { }

    public static IEnumerable<AuthenticationResult> All =>
        [Succeeded, InvalidCredentials, LockedOut, MfaRequired, MfaFailed, NotVerified, Deactivated];

    public static AuthenticationResult FromId(int id) =>
        All.FirstOrDefault(r => r.Id == id)
        ?? throw new ArgumentException($"Unknown AuthenticationResult id: {id}");
}
