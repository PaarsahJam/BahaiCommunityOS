using CommunityOS.AI.Application.Commands;
using CommunityOS.AI.Domain;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace CommunityOS.AI.Tests.Application;

public class ListCapabilitiesHandlerTests
{
    private readonly IAiModelGateway _gateway;
    private readonly ListCapabilitiesHandler _handler;

    public ListCapabilitiesHandlerTests()
    {
        _gateway = Substitute.For<IAiModelGateway>();
        _handler = new ListCapabilitiesHandler(_gateway, NullLogger<ListCapabilitiesHandler>.Instance);
    }

    [Fact]
    public async Task Handle_ShouldReturnEmptyCapabilitiesList()
    {
        var query = new ListCapabilitiesQuery
        {
            SubjectId = Guid.NewGuid()
        };

        _gateway.ListCapabilities().Returns([]);

        var result = await _handler.Handle(query, CancellationToken.None);

        result.Capabilities.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_ShouldCallGatewayListCapabilities()
    {
        var query = new ListCapabilitiesQuery
        {
            SubjectId = Guid.NewGuid()
        };

        _gateway.ListCapabilities().Returns([]);

        await _handler.Handle(query, CancellationToken.None);

        _gateway.Received(1).ListCapabilities();
    }
}
