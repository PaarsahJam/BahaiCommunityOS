using CommunityOS.Identity.Domain.Enumerations;
using CommunityOS.SharedKernel.Domain.Primitives;
using CommunityOS.SharedKernel.Guards;

namespace CommunityOS.Identity.Domain.Entities;

/// <summary>
/// A registered multi-factor authentication method for a user account.
/// The secret is the TOTP shared secret (or SMS/email destination); it is
/// stored encrypted at rest and must never be logged.
/// </summary>
public sealed class MfaMethod : Entity<Guid>
{
    public MfaMethodType Type { get; private set; }
    public string Secret { get; private set; }
    public DateTime CreatedOn { get; private set; }
    public DateTime? VerifiedOn { get; private set; }
    public bool IsVerified => VerifiedOn is not null;
    public bool IsActive { get; private set; }

    private MfaMethod(Guid id, MfaMethodType type, string secret) : base(id)
    {
        Type = type;
        Secret = secret;
        CreatedOn = DateTime.UtcNow;
        IsActive = false;
    }

    public static MfaMethod Create(MfaMethodType type, string secret)
    {
        Guard.NotNull(type, nameof(type));
        Guard.NotNullOrWhiteSpace(secret, nameof(secret));
        return new MfaMethod(Guid.NewGuid(), type, secret);
    }

    public void Verify()
    {
        VerifiedOn = DateTime.UtcNow;
        IsActive = true;
    }

    public void Deactivate()
    {
        IsActive = false;
    }
}
