using CommunityOS.SharedKernel.Domain.Primitives;
using CommunityOS.SharedKernel.Guards;

namespace CommunityOS.Identity.Domain.Entities;

/// <summary>
/// A link between a user account and an external identity provider
/// (e.g. OIDC issuer, social login, or a service principal).
/// </summary>
public sealed class ExternalIdentity : Entity<Guid>
{
    public string Provider { get; private set; }
    public string Subject { get; private set; }
    public DateTime LinkedOn { get; private set; }
    public DateTime? UnlinkedOn { get; private set; }

    private ExternalIdentity(Guid id, string provider, string subject) : base(id)
    {
        Provider = provider;
        Subject = subject;
        LinkedOn = DateTime.UtcNow;
    }

    public static ExternalIdentity Create(string provider, string subject)
    {
        Guard.NotNullOrWhiteSpace(provider, nameof(provider));
        Guard.NotNullOrWhiteSpace(subject, nameof(subject));
        return new ExternalIdentity(Guid.NewGuid(), provider.Trim(), subject.Trim());
    }

    public void Unlink()
    {
        if (UnlinkedOn is null)
            UnlinkedOn = DateTime.UtcNow;
    }

    public bool IsActive => UnlinkedOn is null;
}
