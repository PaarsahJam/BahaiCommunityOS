namespace CommunityOS.AI.Domain.Exceptions;

/// <summary>
/// Thrown when an AI operation is attempted but no provider is enabled.
/// Maps to 503 Service Unavailable (ADR-030 decision 14).
/// </summary>
public sealed class AiProviderDisabledException : Exception
{
    public AiProviderDisabledException()
        : base("AI provider is disabled. No external AI provider is configured.") { }

    public AiProviderDisabledException(string message)
        : base(message) { }
}

/// <summary>
/// Thrown when an unsupported capability is requested.
/// Maps to 400 Bad Request (ADR-030 decision 14).
/// </summary>
public sealed class AiCapabilityNotSupportedException : Exception
{
    public AiCapabilityNotSupportedException(string capability)
        : base($"AI capability '{capability}' is not supported by the current provider.") { }
}
