namespace CommunityOS.AI.Domain;

/// <summary>
/// Result of an AI inference operation. At this gate, the only possible
/// outcome is a failure — the disabled provider never produces content.
/// </summary>
public sealed record AiInferenceResponse
{
    /// <summary>Outcome code: "success", "provider_disabled", "capability_unsupported".</summary>
    public required string Outcome { get; init; }

    /// <summary>
    /// Generated content. Null when the outcome is not "success".
    /// With the disabled provider, this is always null.
    /// </summary>
    public string? GeneratedContent { get; init; }

    /// <summary>
    /// Provenance metadata. Format: "human" | "machine:{provider}".
    /// Null when no content was generated.
    /// </summary>
    public string? Provenance { get; init; }
}
