using CommunityOS.AI.Domain;
using CommunityOS.AI.Domain.Exceptions;
using MediatR;
using Microsoft.Extensions.Logging;

namespace CommunityOS.AI.Application.Commands;

internal sealed class AssistInvocationHandler(
    IAiModelGateway gateway,
    ILogger<AssistInvocationHandler> logger)
    : IRequestHandler<AssistInvocationCommand, AssistInvocationResult>
{
    public async Task<AssistInvocationResult> Handle(
        AssistInvocationCommand request,
        CancellationToken ct)
    {
        logger.AssistInvocationRequested(request.SubjectId, request.Capability);

        try
        {
            var inferenceRequest = new AiInferenceRequest
            {
                SubjectId = request.SubjectId,
                Capability = request.Capability,
                Input = request.Input
            };

            var response = await gateway.InferAsync(inferenceRequest, ct);

            logger.AssistInvocationCompleted(request.SubjectId, request.Capability, response.Outcome);

            return new AssistInvocationResult
            {
                Outcome = response.Outcome,
                Error = response.GeneratedContent is null ? "No content generated." : null
            };
        }
        catch (AiProviderDisabledException ex)
        {
            logger.AssistInvocationFailedProviderDisabled(request.SubjectId, request.Capability);

            return new AssistInvocationResult
            {
                Outcome = "provider_disabled",
                Error = ex.Message
            };
        }
        catch (AiCapabilityNotSupportedException ex)
        {
            logger.AssistInvocationFailedCapabilityUnsupported(request.SubjectId, request.Capability);

            return new AssistInvocationResult
            {
                Outcome = "capability_unsupported",
                Error = ex.Message
            };
        }
    }
}

internal static partial class AssistInvocationLog
{
    [LoggerMessage(
        EventId = 0,
        Level = LogLevel.Information,
        Message = "AI assist invocation requested: subject={SubjectId}, capability={Capability}")]
    public static partial void AssistInvocationRequested(this ILogger logger, Guid subjectId, string capability);

    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Information,
        Message = "AI assist invocation completed: subject={SubjectId}, capability={Capability}, outcome={Outcome}")]
    public static partial void AssistInvocationCompleted(this ILogger logger, Guid subjectId, string capability, string outcome);

    [LoggerMessage(
        EventId = 2,
        Level = LogLevel.Warning,
        Message = "AI assist invocation failed: subject={SubjectId}, capability={Capability}, reason=provider_disabled")]
    public static partial void AssistInvocationFailedProviderDisabled(this ILogger logger, Guid subjectId, string capability);

    [LoggerMessage(
        EventId = 3,
        Level = LogLevel.Warning,
        Message = "AI assist invocation failed: subject={SubjectId}, capability={Capability}, reason=capability_unsupported")]
    public static partial void AssistInvocationFailedCapabilityUnsupported(this ILogger logger, Guid subjectId, string capability);
}
