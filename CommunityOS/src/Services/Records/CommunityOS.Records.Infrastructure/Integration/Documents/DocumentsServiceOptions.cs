using CommunityOS.ServiceClients;

namespace CommunityOS.Records.Infrastructure.Integration.Documents;

/// <summary>
/// Configuration for the Records → Documents service integration. Records
/// never reads the Documents database; evidence references and document-level
/// hold references are commanded through the Documents API as the
/// <c>communityos-records</c> service principal (ADR-023). Fail-closed: a
/// misconfigured base URL or token causes the outbound command to fail rather
/// than silently leaving the Documents side inconsistent.
/// </summary>
public sealed class DocumentsServiceOptions : ServiceClientOptions
{
    public const string SectionName = "DocumentsService";

    public DocumentsServiceOptions()
    {
        ClientId = "communityos-records";
    }
}