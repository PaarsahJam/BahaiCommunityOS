using CommunityOS.Community.Domain.Aggregates;
using CommunityOS.Community.Domain.Entities;
using CommunityOS.Community.Domain.Enumerations;
using CommunityOS.Community.Domain.Events;
using CommunityOS.Community.Domain.Exceptions;
using CommunityOS.Community.Domain.ValueObjects;

namespace CommunityOS.Community.Tests.Domain;

public class PersonTests
{
    private static readonly PrivacyPreferences Privacy = PrivacyPreferences.Create(
        ContactVisibility.Public,
        ContactVisibility.Members,
        ContactVisibility.Private);

    private static Person CreatePerson() => Person.Create(
        "Mona", "Mona Smith", "en", Privacy);

    [Fact]
    public void Create_initializes_person_as_active()
    {
        var person = CreatePerson();

        person.Id.Should().NotBeEmpty();
        person.PreferredName.Should().Be("Mona");
        person.Status.Should().Be(PersonStatus.Active.Name);
        person.IsDeactivated.Should().BeFalse();
        person.DomainEvents.Should().ContainSingle(e => e is PersonCreatedEvent);
    }

    [Fact]
    public void Create_raises_created_event()
    {
        var person = CreatePerson();

        var evt = person.DomainEvents.OfType<PersonCreatedEvent>().Single();
        evt.PersonId.Should().Be(person.Id);
        evt.Status.Should().Be(PersonStatus.Active.Name);
    }

    [Fact]
    public void Create_rejects_blank_preferred_name()
    {
        var act = () => Person.Create("   ", null, null, Privacy);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_rejects_overlong_preferred_name()
    {
        var act = () => Person.Create(new string('a', 201), null, null, Privacy);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void UpdateProfile_changes_fields_and_raises_event()
    {
        var person = CreatePerson();

        var dob = DateTime.SpecifyKind(new DateTime(1990, 1, 1), DateTimeKind.Utc);
        person.UpdateProfile("Ruhi", "Ruhi Jones", dob, "fa");

        person.PreferredName.Should().Be("Ruhi");
        person.DateOfBirth.Should().Be(dob);
        person.PreferredLanguage.Should().Be("fa");
        person.DomainEvents.Should().Contain(e => e is PersonProfileUpdatedEvent);
    }

    [Fact]
    public void SetContactMethods_rejects_multiple_preferred()
    {
        var person = CreatePerson();

        var act = () => person.SetContactMethods([
            ContactMethod.Create(ContactMethodType.Email, "a@example.com", true, ContactVisibility.Private),
            ContactMethod.Create(ContactMethodType.Phone, "+1-555-0100", true, ContactVisibility.Private)]);

        act.Should().Throw<MultiplePreferredContactException>();
    }

    [Fact]
    public void SetContactMethods_rejects_duplicate_values()
    {
        var person = CreatePerson();

        var act = () => person.SetContactMethods([
            ContactMethod.Create(ContactMethodType.Email, "a@example.com", true, ContactVisibility.Private),
            ContactMethod.Create(ContactMethodType.Email, "a@example.com", false, ContactVisibility.Private)]);

        act.Should().Throw<DuplicateContactMethodException>();
    }

    [Fact]
    public void AddContactMethod_clears_existing_preferred()
    {
        var person = CreatePerson();
        var first = ContactMethod.Create(ContactMethodType.Email, "a@example.com", true, ContactVisibility.Private);
        person.AddContactMethod(first);

        person.AddContactMethod(
            ContactMethod.Create(ContactMethodType.Phone, "+1-555-0100", true, ContactVisibility.Private));

        person.ContactMethods.Should().ContainSingle(m => m.IsPreferred);
        person.ContactMethods.Single(m => m.IsPreferred).Value.Should().Be("+1-555-0100");
    }

    [Fact]
    public void RemoveContactMethod_throws_when_absent()
    {
        var person = CreatePerson();

        var act = () => person.RemoveContactMethod(Guid.NewGuid());

        act.Should().Throw<ContactMethodNotFoundException>();
    }

    [Fact]
    public void LinkIdentityAccount_requires_account_and_raises_event()
    {
        var person = CreatePerson();
        var accountId = Guid.NewGuid();

        person.LinkIdentityAccount(accountId);

        person.IdentityAccountId.Should().Be(accountId);
        person.IdentityLinkedOn.Should().NotBeNull();
        person.DomainEvents.Should().Contain(e => e is PersonIdentityLinkedEvent);
    }

    [Fact]
    public void LinkIdentityAccount_rejects_second_link()
    {
        var person = CreatePerson();
        person.LinkIdentityAccount(Guid.NewGuid());

        var act = () => person.LinkIdentityAccount(Guid.NewGuid());

        act.Should().Throw<PersonAlreadyLinkedException>();
    }

    [Fact]
    public void UnlinkIdentityAccount_raises_event_and_keeps_account()
    {
        var person = CreatePerson();
        var accountId = Guid.NewGuid();
        person.LinkIdentityAccount(accountId);

        person.UnlinkIdentityAccount();

        person.IdentityAccountId.Should().BeNull();
        person.IdentityUnlinkedOn.Should().NotBeNull();
        person.DomainEvents.Should().Contain(e => e is PersonIdentityUnlinkedEvent);
    }

    [Fact]
    public void UnlinkIdentityAccount_throws_when_not_linked()
    {
        var person = CreatePerson();

        var act = () => person.UnlinkIdentityAccount();

        act.Should().Throw<PersonNotLinkedException>();
    }

    [Fact]
    public void Deactivate_marks_inactive_and_is_not_repeatable()
    {
        var person = CreatePerson();

        person.Deactivate();
        person.IsDeactivated.Should().BeTrue();
        person.DomainEvents.Should().Contain(e => e is PersonDeactivatedEvent);

        var act = () => person.Deactivate();
        act.Should().Throw<PersonAlreadyDeactivatedException>();
    }

    [Fact]
    public void Reactivate_restores_active_status()
    {
        var person = CreatePerson();
        person.Deactivate();

        person.Reactivate();

        person.IsDeactivated.Should().BeFalse();
        person.DomainEvents.Should().Contain(e => e is PersonReactivatedEvent);
    }

    [Fact]
    public void Reactivate_throws_when_already_active()
    {
        var person = CreatePerson();

        var act = () => person.Reactivate();

        act.Should().Throw<PersonNotDeactivatedException>();
    }
}
