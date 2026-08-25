using MediatR;

namespace CommunityOS.AI.Application.Commands;

/// <summary>
/// Query to list available AI capabilities.
/// Requires ai.assist.invoke permission (ADR-030 decision 12).
/// At this gate, returns an empty list (no provider enabled).
/// </summary>
public sealed record ListCapabilitiesQuery : IRequest<ListCapabilitiesResult>
{
    /// <summary>Subject identifier of the requesting principal.</summary>
    public required Guid SubjectId { get; init; }
}

/// <summary>
/// Result of capability listing. Empty at this gate.
/// </summary>
public sealed record ListCapabilitiesResult
{
    /// <summary>List of available capability names. Empty when no provider is enabled.</summary>
    public required IReadOnlyList<string> Capabilities { get; init; }
}
