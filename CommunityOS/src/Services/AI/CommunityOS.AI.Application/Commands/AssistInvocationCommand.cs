using MediatR;

namespace CommunityOS.AI.Application.Commands;

/// <summary>
/// Command to invoke AI assistance for a specific capability.
/// Requires ai.assist.invoke permission (ADR-030 decision 10).
/// </summary>
public sealed record AssistInvocationCommand : IRequest<AssistInvocationResult>
{
    /// <summary>Subject identifier of the requesting principal.</summary>
    public required Guid SubjectId { get; init; }

    /// <summary>Capability being requested.</summary>
    public required string Capability { get; init; }

    /// <summary>Input text to process.</summary>
    public required string Input { get; init; }
}

/// <summary>
/// Result of an AI assistance invocation. At this gate, the only possible
/// outcome is a failure — the disabled provider never produces content.
/// </summary>
public sealed record AssistInvocationResult
{
    /// <summary>Outcome code: "provider_disabled", "capability_unsupported".</summary>
    public required string Outcome { get; init; }

    /// <summary>Error message, if any.</summary>
    public string? Error { get; init; }
}
