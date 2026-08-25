using CommunityOS.AI.Domain.Exceptions;
using CommunityOS.AI.Infrastructure;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CommunityOS.AI.Tests.Infrastructure;

public class DisabledAiModelGatewayTests
{
    private readonly DisabledAiModelGateway _gateway;

    public DisabledAiModelGatewayTests()
    {
        _gateway = new DisabledAiModelGateway(NullLogger<DisabledAiModelGateway>.Instance);
    }

    [Fact]
    public void InferAsync_ShouldThrowAiProviderDisabledException()
    {
        var request = new Domain.AiInferenceRequest
        {
            SubjectId = Guid.NewGuid(),
            Capability = "test",
            Input = "test input"
        };

        var act = () => _gateway.InferAsync(request, CancellationToken.None);

        act.Should().ThrowAsync<AiProviderDisabledException>();
    }

    [Fact]
    public void ListCapabilities_ShouldReturnEmptyList()
    {
        var capabilities = _gateway.ListCapabilities();

        capabilities.Should().BeEmpty();
    }

    [Fact]
    public void GetProviderInfo_ShouldReturnDisabledProvider()
    {
        var info = _gateway.GetProviderInfo();

        info.Name.Should().Be("disabled");
        info.Status.Should().Be("disabled");
    }

    [Fact]
    public void InferAsync_ShouldNotGenerateContent()
    {
        var request = new Domain.AiInferenceRequest
        {
            SubjectId = Guid.NewGuid(),
            Capability = "translation-assistance",
            Input = "translate this text"
        };

        var act = () => _gateway.InferAsync(request, CancellationToken.None);

        act.Should().ThrowAsync<AiProviderDisabledException>()
            .WithMessage("*disabled*");
    }
}
