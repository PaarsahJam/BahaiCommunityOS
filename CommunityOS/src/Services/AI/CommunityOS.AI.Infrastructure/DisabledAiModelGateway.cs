using CommunityOS.AI.Domain;
using CommunityOS.AI.Domain.Exceptions;
using Microsoft.Extensions.Logging;

namespace CommunityOS.AI.Infrastructure;

/// <summary>
/// Sole provider implementation at this gate. Always returns a typed failure
/// (ADR-030 decision 14). No external provider is enabled (OQ-3 unresolved,
/// OQ-8 deferred). No content is generated, no provenance is attached.
/// </summary>
internal sealed class DisabledAiModelGateway : IAiModelGateway
{
    private static readonly AiProviderInfo ProviderInfo = new()
    {
        Name = "disabled",
        Status = "disabled"
    };

    private readonly ILogger<DisabledAiModelGateway> _logger;

    public DisabledAiModelGateway(ILogger<DisabledAiModelGateway> logger)
    {
        _logger = logger;
    }

    public Task<AiInferenceResponse> InferAsync(AiInferenceRequest request, CancellationToken ct)
    {
        _logger.InferenceAttemptedButProviderDisabled(request.SubjectId, request.Capability);

        throw new AiProviderDisabledException();
    }

    public IReadOnlyList<string> ListCapabilities()
    {
        return [];
    }

    public AiProviderInfo GetProviderInfo()
    {
        return ProviderInfo;
    }
}

internal static partial class DisabledAiModelGatewayLog
{
    [LoggerMessage(
        EventId = 0,
        Level = LogLevel.Warning,
        Message = "AI inference attempted but provider is disabled: subject={SubjectId}, capability={Capability}")]
    public static partial void InferenceAttemptedButProviderDisabled(this ILogger logger, Guid subjectId, string capability);
}
