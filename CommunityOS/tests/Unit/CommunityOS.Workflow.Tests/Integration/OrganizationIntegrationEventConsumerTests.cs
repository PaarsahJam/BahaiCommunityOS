using CommunityOS.Workflow.Domain.Repositories;
using CommunityOS.Workflow.Infrastructure.Integration.Organization;
using MassTransit;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace CommunityOS.Workflow.Tests.Integration;

/// <summary>
/// ADR-016 read-model projection for the Workflow service: organization unit
/// events maintain the <c>organization_unit_references</c> table so task scope
/// checks never query the Organization database. Only the stable unit id is
/// projected — never names or hierarchy (Workflow scopes by id only).
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
        repository.ExistsAsync(UnitId, Arg.Any<CancellationToken>()).Returns(false);

        await consumer.Consume(Context(new CommunityOS.Contracts.Organization.OrganizationUnitCreated(
            UnitId, Guid.NewGuid(), "Local Assembly", "local-spiritual-assembly", null, Now)));

        await repository.Received(1).AddAsync(
            Arg.Is<CommunityOS.Workflow.Domain.Aggregates.OrganizationUnitReference>(r => r.OrganizationUnitId == UnitId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UnitCreated_skips_when_reference_already_exists()
    {
        var (consumer, repository) = Build();
        repository.ExistsAsync(UnitId, Arg.Any<CancellationToken>()).Returns(true);

        await consumer.Consume(Context(new CommunityOS.Contracts.Organization.OrganizationUnitCreated(
            UnitId, Guid.NewGuid(), "Local Assembly", "local-spiritual-assembly", null, Now)));

        await repository.DidNotReceive().AddAsync(Arg.Any<CommunityOS.Workflow.Domain.Aggregates.OrganizationUnitReference>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UnitUpdated_adds_missing_reference()
    {
        var (consumer, repository) = Build();
        repository.ExistsAsync(UnitId, Arg.Any<CancellationToken>()).Returns(false);

        await consumer.Consume(Context(new CommunityOS.Contracts.Organization.OrganizationUnitUpdated(
            UnitId, "Regional Council", "regional-council", Now)));

        await repository.Received(1).AddAsync(
            Arg.Any<CommunityOS.Workflow.Domain.Aggregates.OrganizationUnitReference>(),
            Arg.Any<CancellationToken>());
    }
}