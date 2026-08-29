using CommunityOS.ServiceClients;

namespace CommunityOS.Workflow.Infrastructure.Integration.Community;

/// <summary>
/// Configuration for the Workflow → Community service integration. Workflow
/// never reads the Community database; assignee/person details are resolved
/// through the Community API as the <c>communityos-workflow</c> service
/// principal (ADR-024). Workflow stores only stable person ids. Fail-closed: a
/// misconfigured base URL or token causes the outbound call to fail rather than
/// silently continuing without validation.
/// </summary>
public sealed class CommunityServiceOptions : ServiceClientOptions
{
    public const string SectionName = "CommunityService";

    public CommunityServiceOptions()
    {
        ClientId = "communityos-workflow";
    }
}