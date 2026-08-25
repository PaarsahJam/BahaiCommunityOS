using CommunityOS.AI.Domain.Exceptions;
using CommunityOS.AI.Infrastructure;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CommunityOS.AI.IntegrationTests;

/// <summary>
/// Integration tests for AI Platform. These tests verify the provider seam
/// behavior in a more realistic environment. Docker is not required because
/// AI Platform is stateless at this gate.
/// </summary>
public class AssistInvocationIntegrationTests
{
    private readonly DisabledAiModelGateway _gateway;

    public AssistInvocationIntegrationTests()
    {
        _gateway = new DisabledAiModelGateway(NullLogger<DisabledAiModelGateway>.Instance);
    }

    [Fact]
    public async Task ProviderSeam_ShouldAlwaysReturnDisabledProvider()
    {
        var info = _gateway.GetProviderInfo();

        info.Name.Should().Be("disabled");
        info.Status.Should().Be("disabled");
    }

    [Fact]
    public async Task ProviderSeam_ShouldAlwaysReturnEmptyCapabilities()
    {
        var capabilities = _gateway.ListCapabilities();

        capabilities.Should().BeEmpty();
    }

    [Fact]
    public async Task ProviderSeam_ShouldAlwaysThrowOnInference()
    {
        var request = new Domain.AiInferenceRequest
        {
            SubjectId = Guid.NewGuid(),
            Capability = "any-capability",
            Input = "any input"
        };

        var act = () => _gateway.InferAsync(request, CancellationToken.None);

        await act.Should().ThrowAsync<AiProviderDisabledException>();
    }
}
