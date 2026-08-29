using CommunityOS.ServiceClients;

namespace CommunityOS.Workflow.Infrastructure.Integration.Documents;

/// <summary>
/// Configuration for the Workflow → Documents service integration. Workflow
/// never reads the Documents database; task document references are created
/// through the Documents API as the <c>communityos-workflow</c> service
/// principal (ADR-024). Fail-closed: a misconfigured base URL or token causes
/// the outbound command to fail rather than silently leaving the Documents side
/// inconsistent.
/// </summary>
public sealed class DocumentsServiceOptions : ServiceClientOptions
{
    public const string SectionName = "DocumentsService";

    public DocumentsServiceOptions()
    {
        ClientId = "communityos-workflow";
    }
}