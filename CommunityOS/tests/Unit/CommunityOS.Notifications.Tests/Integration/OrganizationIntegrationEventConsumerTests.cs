using CommunityOS.Contracts.Organization;
using CommunityOS.Notifications.Domain.Aggregates;
using CommunityOS.Notifications.Domain.Repositories;
using CommunityOS.Notifications.Infrastructure.Integration.Organization;
using MassTransit;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace CommunityOS.Notifications.Tests.Integration;

/// <summary>
/// ADR-016 read-model projection for the Notifications service: organization
/// unit events maintain the <c>organization_unit_references</c> table so
/// notification scope references can be validated without ever querying the
/// Organization database. Only the stable unit id is projected — never names or
/// hierarchy.
/// </summary>
public class OrganizationIntegrationEventConsumerTests
{
    private static readonly Guid UnitId = Guid.NewGuid();
    private static readonly DateTime Now = DateTime.UtcNow;

    private static ConsumeContext<T> Context<T>(T message)
        where T : class
    {
        var ctx = Substitute.For<ConsumeContext<T>>();
        ctx.Message.Returns(message);
        ctx.CancellationToken.Returns(CancellationToken.None);
        return ctx;
    }

    private static (OrganizationIntegrationEventConsumer Consumer, IOrganizationUnitReferenceRepository Repository) Build()
    {
        var repository = Substitute.For<IOrganizationUnitReferenceRepository>();
        var consumer = new OrganizationIntegrationEventConsumer(
            repository, NullLogger<OrganizationIntegrationEventConsumer>.Instance);
        return (consumer, repository);
    }

    [Fact]
    public async Task UnitCreated_adds_reference_when_absent()
    {
        var (consumer, repository) = Build();
        repository.GetByOrganizationUnitIdAsync(UnitId, Arg.Any<CancellationToken>()).Returns((OrganizationUnitReference?)null);

        await consumer.Consume(Context(new OrganizationUnitCreated(
            UnitId, Guid.NewGuid(), "Local Assembly", "local-spiritual-assembly", null, Now)));

        await repository.Received(1).AddAsync(
            Arg.Is<OrganizationUnitReference>(r => r.OrganizationUnitId == UnitId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UnitCreated_skips_when_reference_already_exists()
    {
        var (consumer, repository) = Build();
        repository.GetByOrganizationUnitIdAsync(UnitId, Arg.Any<CancellationToken>())
            .Returns(OrganizationUnitReference.Create(UnitId, Now));

        await consumer.Consume(Context(new OrganizationUnitCreated(
            UnitId, Guid.NewGuid(), "Local Assembly", "local-spiritual-assembly", null, Now)));

        await repository.DidNotReceive().AddAsync(Arg.Any<OrganizationUnitReference>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UnitUpdated_adds_missing_reference()
    {
        var (consumer, repository) = Build();
        repository.GetByOrganizationUnitIdAsync(UnitId, Arg.Any<CancellationToken>()).Returns((OrganizationUnitReference?)null);

        await consumer.Consume(Context(new OrganizationUnitUpdated(
            UnitId, "Regional Council", "regional-council", Now)));

        await repository.Received(1).AddAsync(
            Arg.Any<OrganizationUnitReference>(), Arg.Any<CancellationToken>());
    }
}