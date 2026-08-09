using CommunityOS.Identity.Domain.Entities;
using CommunityOS.Identity.Domain.Enumerations;
using CommunityOS.Identity.Domain.Events;
using CommunityOS.Identity.Domain.Exceptions;
using CommunityOS.Identity.Domain.ValueObjects;
using CommunityOS.SharedKernel.Domain.Primitives;
using CommunityOS.SharedKernel.Guards;

namespace CommunityOS.Identity.Domain.Aggregates;

/// <summary>
/// Root aggregate of the Identity bounded context. Represents a single
/// identity account. Deliberately distinct from a person profile or a
/// community membership: a user may hold an account without being a
/// member, and vice versa.
/// </summary>
public sealed class UserAccount : AggregateRoot<Guid>
{
    private readonly List<Credential> _credentials = [];
    private readonly List<ExternalIdentity> _externalIdentities = [];
    private readonly List<MfaMethod> _mfaMethods = [];
    private readonly List<Device> _devices = [];

    private const int MaxFailedLoginAttempts = 5;
    private static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);

    public Email Email { get; private set; }
    public AccountStatus Status { get; private set; }
    public DateTime CreatedOn { get; private set; }
    public DateTime? VerifiedOn { get; private set; }
    public DateTime? DeactivatedOn { get; private set; }
    public DateTime? LastLoginOn { get; private set; }
    public int FailedLoginAttempts { get; private set; }
    public DateTime? LockedUntil { get; private set; }

    public IReadOnlyList<Credential> Credentials => _credentials.AsReadOnly();
    public IReadOnlyList<ExternalIdentity> ExternalIdentities => _externalIdentities.AsReadOnly();
    public IReadOnlyList<MfaMethod> MfaMethods => _mfaMethods.AsReadOnly();
    public IReadOnlyList<Device> Devices => _devices.AsReadOnly();

    private UserAccount(Guid id, Email email) : base(id)
    {
        Email = email;
        Status = AccountStatus.PendingVerification;
        CreatedOn = DateTime.UtcNow;
    }

    public static UserAccount Register(Email email, string passwordHash)
    {
        Guard.NotNull(email, nameof(email));
        Guard.NotNullOrWhiteSpace(passwordHash, nameof(passwordHash));

        var account = new UserAccount(Guid.NewGuid(), email);
        account._credentials.Add(Credential.Create(CredentialType.Password, passwordHash));
        account.RaiseDomainEvent(new UserAccountRegisteredEvent(account.Id, email.Value));
        return account;
    }

    /// <summary>Creates an account provisioned by an external identity provider.</summary>
    public static UserAccount RegisterWithExternalIdentity(
        Email email, string provider, string subject)
    {
        Guard.NotNull(email, nameof(email));
        Guard.NotNullOrWhiteSpace(provider, nameof(provider));
        Guard.NotNullOrWhiteSpace(subject, nameof(subject));

        var account = new UserAccount(Guid.NewGuid(), email);
        account.Verify();
        account.LinkExternalIdentity(provider, subject);
        return account;
    }

    public void Verify()
    {
        if (Status == AccountStatus.Deactivated)
            throw new AccountDeactivatedException(Id);

        Status = AccountStatus.Active;
        VerifiedOn = DateTime.UtcNow;
        FailedLoginAttempts = 0;
        LockedUntil = null;
        RaiseDomainEvent(new UserAccountVerifiedEvent(Id));
    }

    public void Deactivate()
    {
        Status = AccountStatus.Deactivated;
        DeactivatedOn = DateTime.UtcNow;
        RaiseDomainEvent(new UserAccountDeactivatedEvent(Id));
    }

    /// <summary>Returns the stored password hash so callers can verify a candidate.</summary>
    public string? GetPasswordHash() =>
        _credentials.FirstOrDefault(c => c.Type == CredentialType.Password)?.Secret;

    public bool HasPasswordCredential =>
        _credentials.Any(c => c.Type == CredentialType.Password);

    public void UpdatePassword(string newPasswordHash)
    {
        Guard.NotNullOrWhiteSpace(newPasswordHash, nameof(newPasswordHash));

        var existing = _credentials.FirstOrDefault(c => c.Type == CredentialType.Password);
        if (existing is not null)
            _credentials.Remove(existing);

        _credentials.Add(Credential.Create(CredentialType.Password, newPasswordHash));
        RaiseDomainEvent(new CredentialChangedEvent(Id));
    }

    public void LinkExternalIdentity(string provider, string subject)
    {
        Guard.NotNullOrWhiteSpace(provider, nameof(provider));
        Guard.NotNullOrWhiteSpace(subject, nameof(subject));

        if (_externalIdentities.Any(x => x.IsActive && x.Provider == provider && x.Subject == subject))
            return;

        var identity = ExternalIdentity.Create(provider, subject);
        _externalIdentities.Add(identity);
        RaiseDomainEvent(new ExternalIdentityLinkedEvent(Id, provider, subject));
    }

    public void UnlinkExternalIdentity(string provider, string subject)
    {
        var identity = _externalIdentities.FirstOrDefault(
            x => x.IsActive && x.Provider == provider && x.Subject == subject);
        if (identity is not null)
        {
            identity.Unlink();
            RaiseDomainEvent(new ExternalIdentityUnlinkedEvent(Id, provider, subject));
        }
    }

    public MfaMethod EnrollMfa(MfaMethodType type, string secret)
    {
        Guard.NotNull(type, nameof(type));
        Guard.NotNullOrWhiteSpace(secret, nameof(secret));

        var method = MfaMethod.Create(type, secret);
        _mfaMethods.Add(method);
        RaiseDomainEvent(new MfaMethodEnrolledEvent(Id, type.Name));
        return method;
    }

    public void VerifyMfa(Guid mfaMethodId)
    {
        var method = _mfaMethods.FirstOrDefault(m => m.Id == mfaMethodId)
            ?? throw new ArgumentException("MFA method not found.", nameof(mfaMethodId));
        method.Verify();
    }

    public void RemoveMfa(Guid mfaMethodId)
    {
        var method = _mfaMethods.FirstOrDefault(m => m.Id == mfaMethodId)
            ?? throw new ArgumentException("MFA method not found.", nameof(mfaMethodId));
        _mfaMethods.Remove(method);
        RaiseDomainEvent(new MfaMethodRemovedEvent(Id, method.Type.Name));
    }

    public bool HasVerifiedMfa => _mfaMethods.Any(m => m.IsVerified && m.IsActive);

    public Device RegisterDevice(string name, string? platform, string? userAgent)
    {
        Guard.NotNullOrWhiteSpace(name, nameof(name));
        var device = Device.Create(name, platform, userAgent);
        _devices.Add(device);
        RaiseDomainEvent(new DeviceRegisteredEvent(Id, device.Id, device.Name));
        return device;
    }

    public void RecordFailedLogin()
    {
        FailedLoginAttempts++;
        if (FailedLoginAttempts >= MaxFailedLoginAttempts)
            LockedUntil = DateTime.UtcNow.Add(LockoutDuration);
    }

    public void ResetFailedLogins()
    {
        FailedLoginAttempts = 0;
        LockedUntil = null;
    }

    public bool IsLocked =>
        LockedUntil is { } until && until > DateTime.UtcNow;

    public void RecordSuccessfulLogin()
    {
        LastLoginOn = DateTime.UtcNow;
        ResetFailedLogins();
    }
}
