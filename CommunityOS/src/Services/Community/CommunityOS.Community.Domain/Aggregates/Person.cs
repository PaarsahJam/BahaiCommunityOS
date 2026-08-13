using CommunityOS.Community.Domain.Entities;
using CommunityOS.Community.Domain.Enumerations;
using CommunityOS.Community.Domain.Events;
using CommunityOS.Community.Domain.Exceptions;
using CommunityOS.Community.Domain.ValueObjects;
using CommunityOS.SharedKernel.Domain.Primitives;
using CommunityOS.SharedKernel.Guards;

namespace CommunityOS.Community.Domain.Aggregates;

/// <summary>
/// A real individual known to the community. A Person is <b>not</b> an Identity
/// account: a person may exist without a login, and an account may optionally
/// be linked to a person. Community never owns passwords or session state
/// (that is Identity's bounded context); Identity never owns the person
/// profile (that is Community's). See ADR-016/ADR-018.
/// </summary>
public sealed class Person : AggregateRoot<Guid>
{
    private readonly List<ContactMethod> _contactMethods = [];

    private Person() : base(Guid.Empty)
    {
        PreferredName = null!;
        Status = null!;
        Privacy = null!;
    }

    private Person(
        Guid id,
        string preferredName,
        string? formalName,
        string? preferredLanguage,
        PrivacyPreferences privacy) : base(id)
    {
        PreferredName = preferredName;
        FormalName = formalName;
        PreferredLanguage = preferredLanguage;
        Privacy = privacy;
        Status = PersonStatus.Active.Name;
        CreatedOn = DateTime.UtcNow;
    }

    public string PreferredName { get; private set; }
    public string? FormalName { get; private set; }
    public DateTime? DateOfBirth { get; private set; }
    public string? PreferredLanguage { get; private set; }
    public string Status { get; private set; }
    public Guid? IdentityAccountId { get; private set; }
    public DateTime? IdentityLinkedOn { get; private set; }
    public DateTime? IdentityUnlinkedOn { get; private set; }
    public PrivacyPreferences Privacy { get; private set; }
    public DateTime CreatedOn { get; private set; }

    public IReadOnlyList<ContactMethod> ContactMethods => _contactMethods.AsReadOnly();

    public bool IsDeactivated => Status == PersonStatus.Deactivated.Name;

    public static Person Create(
        string preferredName,
        string? formalName,
        string? preferredLanguage,
        PrivacyPreferences privacy)
    {
        Guard.NotNullOrWhiteSpace(preferredName, nameof(preferredName));
        Guard.MaxLength(preferredName, 200, nameof(preferredName));
        Guard.MaxLength(formalName ?? string.Empty, 200, nameof(formalName));
        Guard.MaxLength(preferredLanguage ?? string.Empty, 20, nameof(preferredLanguage));
        Guard.NotNull(privacy, nameof(privacy));

        var person = new Person(
            Guid.NewGuid(),
            preferredName.Trim(),
            NullIfBlank(formalName),
            NullIfBlank(preferredLanguage),
            privacy);

        person.RaiseDomainEvent(new PersonCreatedEvent(
            person.Id,
            person.Status,
            Person.OccurredAt()));        return person;
    }

    public void UpdateProfile(
        string preferredName,
        string? formalName,
        DateTime? dateOfBirth,
        string? preferredLanguage)
    {
        Guard.NotNullOrWhiteSpace(preferredName, nameof(preferredName));
        Guard.MaxLength(preferredName, 200, nameof(preferredName));
        Guard.MaxLength(formalName ?? string.Empty, 200, nameof(formalName));
        Guard.MaxLength(preferredLanguage ?? string.Empty, 20, nameof(preferredLanguage));

        PreferredName = preferredName.Trim();
        FormalName = NullIfBlank(formalName);
        DateOfBirth = dateOfBirth?.ToUniversalTime();
        PreferredLanguage = NullIfBlank(preferredLanguage);

        RaiseDomainEvent(new PersonProfileUpdatedEvent(Id, Status, OccurredAt()));
    }

    public void UpdatePrivacy(PrivacyPreferences privacy)
    {
        Guard.NotNull(privacy, nameof(privacy));
        Privacy = privacy;
    }

    /// <summary>
    /// Replaces the person's contact methods, enforcing the invariant that at
    /// most one method is preferred and no value is duplicated.
    /// </summary>
    public void SetContactMethods(IEnumerable<ContactMethod> contactMethods)
    {
        Guard.NotNull(contactMethods, nameof(contactMethods));

        var methods = contactMethods.ToList();
        if (methods.Count(m => m.IsPreferred) > 1)
            throw new MultiplePreferredContactException();

        var duplicates = methods
            .GroupBy(m => (m.Type.Name, m.Value))
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();
        if (duplicates.Count != 0)
            throw new DuplicateContactMethodException(duplicates[0].Value);

        _contactMethods.Clear();
        _contactMethods.AddRange(methods);

        RaiseDomainEvent(new PersonProfileUpdatedEvent(Id, Status, OccurredAt()));
    }

    public void AddContactMethod(ContactMethod contactMethod)
    {
        Guard.NotNull(contactMethod, nameof(contactMethod));

        if (_contactMethods.Any(m => m.Type == contactMethod.Type && m.Value == contactMethod.Value))
            throw new DuplicateContactMethodException(contactMethod.Value);

        if (contactMethod.IsPreferred)
        {
            foreach (var existing in _contactMethods)
                existing.ClearPreferred();
        }

        _contactMethods.Add(contactMethod);
        RaiseDomainEvent(new PersonProfileUpdatedEvent(Id, Status, OccurredAt()));
    }

    public void RemoveContactMethod(Guid contactMethodId)
    {
        var existing = _contactMethods.FirstOrDefault(m => m.Id == contactMethodId);
        if (existing is null)
            throw new ContactMethodNotFoundException(Id, contactMethodId);

        _contactMethods.Remove(existing);
        RaiseDomainEvent(new PersonProfileUpdatedEvent(Id, Status, OccurredAt()));
    }

    /// <summary>
    /// Explicitly links this person to an Identity account. The link is
    /// auditable (link/unlink events) and requires the account id. Linking is
    /// optional — a person may exist without an account.
    /// </summary>
    public void LinkIdentityAccount(Guid identityAccountId)
    {
        Guard.NotDefault(identityAccountId, nameof(identityAccountId));

        if (IdentityAccountId is not null)
            throw new PersonAlreadyLinkedException(Id, IdentityAccountId.Value);

        IdentityAccountId = identityAccountId;
        IdentityLinkedOn = DateTime.UtcNow;
        IdentityUnlinkedOn = null;

        RaiseDomainEvent(new PersonIdentityLinkedEvent(Id, identityAccountId, OccurredAt()));
    }

    /// <summary>
    /// Explicitly unlinks this person from its Identity account. The account
    /// itself remains valid; only the profile link is removed.
    /// </summary>
    public void UnlinkIdentityAccount()
    {
        if (IdentityAccountId is null)
            throw new PersonNotLinkedException(Id);

        var accountId = IdentityAccountId.Value;
        IdentityAccountId = null;
        IdentityUnlinkedOn = DateTime.UtcNow;

        RaiseDomainEvent(new PersonIdentityUnlinkedEvent(Id, accountId, OccurredAt()));
    }

    /// <summary>
    /// Deactivates the person record. Historical community participation is
    /// preserved; this is a lifecycle state, not a deletion.
    /// </summary>
    public void Deactivate()
    {
        if (IsDeactivated)
            throw new PersonAlreadyDeactivatedException(Id);

        Status = PersonStatus.Deactivated.Name;
        RaiseDomainEvent(new PersonDeactivatedEvent(Id, OccurredAt()));
    }

    public void Reactivate()
    {
        if (!IsDeactivated)
            throw new PersonNotDeactivatedException(Id);

        Status = PersonStatus.Active.Name;
        RaiseDomainEvent(new PersonReactivatedEvent(Id, OccurredAt()));
    }

    private static DateTime OccurredAt() => DateTime.UtcNow;

    private static string? NullIfBlank(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
