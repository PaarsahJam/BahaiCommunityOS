using CommunityOS.AI.Application.Commands;
using CommunityOS.AI.Domain;
using CommunityOS.AI.Domain.Exceptions;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace CommunityOS.AI.Tests.Application;

public class AssistInvocationHandlerTests
{
    private readonly IAiModelGateway _gateway;
    private readonly AssistInvocationHandler _handler;

    public AssistInvocationHandlerTests()
    {
        _gateway = Substitute.For<IAiModelGateway>();
        _handler = new AssistInvocationHandler(_gateway, NullLogger<AssistInvocationHandler>.Instance);
    }

    [Fact]
    public async Task Handle_WhenProviderDisabled_ShouldReturnProviderDisabledOutcome()
    {
        var command = new AssistInvocationCommand
        {
            SubjectId = Guid.NewGuid(),
            Capability = "test",
            Input = "test input"
        };

        _gateway.InferAsync(Arg.Any<AiInferenceRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<AiInferenceResponse>(new AiProviderDisabledException()));

        var result = await _handler.Handle(command, CancellationToken.None);

        result.Outcome.Should().Be("provider_disabled");
        result.Error.Should().Contain("disabled");
    }

    [Fact]
    public async Task Handle_WhenCapabilityNotSupported_ShouldReturnCapabilityUnsupportedOutcome()
    {
        var command = new AssistInvocationCommand
        {
            SubjectId = Guid.NewGuid(),
            Capability = "unsupported",
            Input = "test input"
        };

        _gateway.InferAsync(Arg.Any<AiInferenceRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<AiInferenceResponse>(new AiCapabilityNotSupportedException("unsupported")));

        var result = await _handler.Handle(command, CancellationToken.None);

        result.Outcome.Should().Be("capability_unsupported");
        result.Error.Should().Contain("unsupported");
    }

    [Fact]
    public async Task Handle_ShouldCallGatewayWithCorrectRequest()
    {
        var subjectId = Guid.NewGuid();
        var command = new AssistInvocationCommand
        {
            SubjectId = subjectId,
            Capability = "test-capability",
            Input = "test input"
        };

        _gateway.InferAsync(Arg.Any<AiInferenceRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<AiInferenceResponse>(new AiProviderDisabledException()));

        await _handler.Handle(command, CancellationToken.None);

        await _gateway.Received(1).InferAsync(
            Arg.Is<AiInferenceRequest>(r =>
                r.SubjectId == subjectId &&
                r.Capability == "test-capability" &&
                r.Input == "test input"),
            Arg.Any<CancellationToken>());
    }
}
