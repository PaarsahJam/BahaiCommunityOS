using CommunityOS.AI.Domain;
using CommunityOS.AI.Domain.Exceptions;
using CommunityOS.AI.Infrastructure;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CommunityOS.AI.Tests.Infrastructure;

public class AiSafetyEnforcerTests
{
    private readonly AiSafetyEnforcer _enforcer;

    public AiSafetyEnforcerTests()
    {
        _enforcer = new AiSafetyEnforcer(NullLogger<AiSafetyEnforcer>.Instance);
    }

    [Fact]
    public void ValidateBeforeInference_WithValidRequest_ShouldNotThrow()
    {
        var request = new AiInferenceRequest
        {
            SubjectId = Guid.NewGuid(),
            Capability = "test",
            Input = "test input"
        };

        var act = () => _enforcer.ValidateBeforeInference(request);

        act.Should().NotThrow();
    }

    [Fact]
    public void ValidateBeforeInference_WithEmptyInput_ShouldThrowAiCapabilityNotSupportedException()
    {
        var request = new AiInferenceRequest
        {
            SubjectId = Guid.NewGuid(),
            Capability = "test",
            Input = ""
        };

        var act = () => _enforcer.ValidateBeforeInference(request);

        act.Should().Throw<AiCapabilityNotSupportedException>();
    }

    [Fact]
    public void ValidateAfterInference_WithNoContent_ShouldNotThrow()
    {
        var response = new AiInferenceResponse
        {
            Outcome = "provider_disabled"
        };

        var act = () => _enforcer.ValidateAfterInference(response);

        act.Should().NotThrow();
    }

    [Fact]
    public void ValidateAfterInference_WithContent_ShouldNotThrow()
    {
        var response = new AiInferenceResponse
        {
            Outcome = "success",
            GeneratedContent = "test content"
        };

        var act = () => _enforcer.ValidateAfterInference(response);

        act.Should().NotThrow();
    }
}
