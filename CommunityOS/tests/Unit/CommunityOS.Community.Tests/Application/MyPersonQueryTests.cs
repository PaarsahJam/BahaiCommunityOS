using CommunityOS.Community.Application;
using CommunityOS.Community.Application.Queries;
using CommunityOS.Community.Domain.Aggregates;
using CommunityOS.Community.Domain.Enumerations;
using CommunityOS.Community.Domain.Exceptions;
using CommunityOS.Community.Domain.Repositories;
using CommunityOS.Community.Domain.ValueObjects;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace CommunityOS.Community.Tests.Application;

/// <summary>
/// Tests for the authenticated person resolution endpoint
/// (GET /api/v1/my-person). The endpoint is self-scoped: the JWT sub
/// claim is the sole input, and it resolves the Community Person linked
/// to that Identity account.
/// </summary>
public class MyPersonQueryTests
{
    private static readonly DateTime Now = DateTime.UtcNow;

    private sealed class Harness
    {
        public IPersonRepository Persons { get; } = Substitute.For<IPersonRepository>();
        public ServiceProvider Provider { get; }

        public Harness()
        {
            var services = new ServiceCollection();
            services.AddLogging(b => b.SetMinimumLevel(LogLevel.None));
            services.AddCommunityApplication();
            services.AddScoped(_ => Persons);
            Provider = services.BuildServiceProvider();
        }

        public ISender Sender => Provider.GetRequiredService<ISender>();
    }

    private static Harness CreateHarness() => new();

    private static Person StubPerson(Guid identityAccountId)
    {
        var person = Person.Create(
            "Mona", "Mona Smith", "en",
            PrivacyPreferences.Create(ContactVisibility.Public, ContactVisibility.Public, ContactVisibility.Public));
        person.LinkIdentityAccount(identityAccountId);
        return person;
    }

    [Fact]
    public async Task GetMyPerson_with_linked_account_returns_person()
    {
        var h = CreateHarness();
        var accountId = Guid.NewGuid();
        var person = StubPerson(accountId);
        h.Persons.GetByIdentityAccountIdAsync(accountId, Arg.Any<CancellationToken>()).Returns(person);

        var result = await h.Sender.Send(new GetMyPersonQuery(accountId));

        result.Id.Should().Be(person.Id);
        result.PreferredName.Should().Be("Mona");
        result.FormalName.Should().Be("Mona Smith");
        result.HasLinkedIdentityAccount.Should().BeTrue();
    }

    [Fact]
    public async Task GetMyPerson_with_linked_account_returns_correct_person_for_that_account()
    {
        var h = CreateHarness();
        var accountIdA = Guid.NewGuid();
        var accountIdB = Guid.NewGuid();
        var personA = StubPerson(accountIdA);
        var personB = StubPerson(accountIdB);
        h.Persons.GetByIdentityAccountIdAsync(accountIdA, Arg.Any<CancellationToken>()).Returns(personA);
        h.Persons.GetByIdentityAccountIdAsync(accountIdB, Arg.Any<CancellationToken>()).Returns(personB);

        var result = await h.Sender.Send(new GetMyPersonQuery(accountIdA));

        result.Id.Should().Be(personA.Id);
        result.PreferredName.Should().Be("Mona");
        // Verify the other person was not returned
        await h.Persons.Received(1).GetByIdentityAccountIdAsync(accountIdA, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetMyPerson_with_no_linked_account_throws()
    {
        var h = CreateHarness();
        var accountId = Guid.NewGuid();
        h.Persons.GetByIdentityAccountIdAsync(accountId, Arg.Any<CancellationToken>())
            .Returns((Person?)null);

        var act = () => h.Sender.Send(new GetMyPersonQuery(accountId));

        await act.Should().ThrowAsync<PersonNotLinkedToAccountException>();
    }

    [Fact]
    public async Task GetMyPerson_does_not_accept_person_id_from_caller()
    {
        var h = CreateHarness();
        var accountId = Guid.NewGuid();
        var person = StubPerson(accountId);
        h.Persons.GetByIdentityAccountIdAsync(accountId, Arg.Any<CancellationToken>()).Returns(person);

        // The query only accepts IdentityAccountId — no PersonId parameter exists.
        var query = new GetMyPersonQuery(accountId);
        var result = await h.Sender.Send(query);

        result.Id.Should().Be(person.Id);
        // Verify the repo was called with the JWT-derived account id only
        await h.Persons.Received(1).GetByIdentityAccountIdAsync(accountId, Arg.Any<CancellationToken>());
        // Verify GetByIdAsync (which takes a person ID) was never called
        await h.Persons.DidNotReceive().GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetMyPerson_uses_repository_method_not_direct_lookup()
    {
        var h = CreateHarness();
        var accountId = Guid.NewGuid();
        var person = StubPerson(accountId);
        h.Persons.GetByIdentityAccountIdAsync(accountId, Arg.Any<CancellationToken>()).Returns(person);

        await h.Sender.Send(new GetMyPersonQuery(accountId));

        // Ensure the correct repository method is used
        await h.Persons.Received(1).GetByIdentityAccountIdAsync(accountId, Arg.Any<CancellationToken>());
        // Ensure we never use the direct-person-id lookup
        await h.Persons.DidNotReceive().GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }
}
