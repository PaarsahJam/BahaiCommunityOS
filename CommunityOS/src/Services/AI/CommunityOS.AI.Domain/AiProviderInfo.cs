namespace CommunityOS.AI.Domain;

/// <summary>
/// Metadata about the configured AI provider. At this gate, the only
/// provider is "disabled".
/// </summary>
public sealed record AiProviderInfo
{
    /// <summary>Provider name (e.g., "disabled", "openai", "azure-openai").</summary>
    public required string Name { get; init; }

    /// <summary>Provider status: "enabled" or "disabled".</summary>
    public required string Status { get; init; }
}
