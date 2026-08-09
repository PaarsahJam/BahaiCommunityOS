using CommunityOS.Identity.Domain.Enumerations;
using CommunityOS.SharedKernel.Domain.Primitives;
using CommunityOS.SharedKernel.Guards;

namespace CommunityOS.Identity.Domain.Entities;

/// <summary>
/// A stored authentication secret owned by a user account.
/// For password credentials the secret is a modern salted hash;
/// for passkeys it holds the WebAuthn public key material.
/// </summary>
public sealed class Credential : Entity<Guid>
{
    public CredentialType Type { get; private set; }
    public string Secret { get; private set; }
    public string? Metadata { get; private set; }
    public DateTime CreatedOn { get; private set; }
    public DateTime? LastUsedOn { get; private set; }

    private Credential(Guid id, CredentialType type, string secret, string? metadata) : base(id)
    {
        Type = type;
        Secret = secret;
        Metadata = metadata;
        CreatedOn = DateTime.UtcNow;
    }

    public static Credential Create(CredentialType type, string secret, string? metadata = null)
    {
        Guard.NotNull(type, nameof(type));
        Guard.NotNullOrWhiteSpace(secret, nameof(secret));
        return new Credential(Guid.NewGuid(), type, secret, metadata);
    }

    public void MarkUsed() => LastUsedOn = DateTime.UtcNow;
}
