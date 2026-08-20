namespace CommunityOS.Workflow.Application.Options;

/// <summary>
/// Application-level Workflow configuration (ratified, ADR-024). Bound from the
/// <c>Workflow</c> configuration section by the Infrastructure layer.
/// </summary>
public sealed class WorkflowOptions
{
    public const string SectionName = "Workflow";

    /// <summary>
    /// Reserved: trusted in-process caller id for future fact queries. Never
    /// presented to other services; only used as an audit/actor identifier.
    /// </summary>
    public string InternalClientId { get; set; } = string.Empty;
}