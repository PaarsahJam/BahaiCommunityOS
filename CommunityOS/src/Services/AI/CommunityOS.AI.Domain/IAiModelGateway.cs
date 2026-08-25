namespace CommunityOS.AI.Domain;

/// <summary>
/// Provider-neutral gateway seam (ADR-030 decision 6). The sole implementation
/// at this gate is <c>DisabledAiModelGateway</c>. External providers are
/// prohibited while OQ-3 remains unresolved.
/// </summary>
public interface IAiModelGateway
{
    /// <summary>
    /// Executes an inference request. With the disabled provider, this always
    /// throws <see cref="Exceptions.AiProviderDisabledException"/>.
    /// </summary>
    Task<AiInferenceResponse> InferAsync(AiInferenceRequest request, CancellationToken ct);

    /// <summary>
    /// Returns the list of capabilities available through this provider.
    /// With the disabled provider, this returns an empty list.
    /// </summary>
    IReadOnlyList<string> ListCapabilities();

    /// <summary>
    /// Returns metadata about the configured provider.
    /// </summary>
    AiProviderInfo GetProviderInfo();
}
