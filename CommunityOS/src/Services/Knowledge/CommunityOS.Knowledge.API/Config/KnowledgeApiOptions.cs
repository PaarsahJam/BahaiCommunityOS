namespace CommunityOS.Knowledge.API.Config;

/// <summary>
/// Operational configuration for the Knowledge API host.
/// </summary>
public sealed class KnowledgeApiOptions
{
    public const string SectionName = "Knowledge";

    /// <summary>
    /// Identifier presented in the <c>X-Client-Id</c> header by trusted
    /// internal callers (currently the Authorization service's organization
    /// context provider). Only this client may query the narrow passage
    /// citation fact endpoint. Fail closed: anything else is rejected.
    /// </summary>
    public string InternalClientId { get; set; } = "communityos-authorization";
}