namespace CommunityOS.AI.Domain;

/// <summary>
/// Represents a request for AI assistance. Carries only operational metadata
/// and input content — no credentials, no provider details, no system prompts.
/// </summary>
public sealed record AiInferenceRequest
{
    /// <summary>Subject identifier of the requesting principal.</summary>
    public required Guid SubjectId { get; init; }

    /// <summary>Capability being requested (e.g., "summarization").</summary>
    public required string Capability { get; init; }

    /// <summary>Input text to process.</summary>
    public required string Input { get; init; }
}
