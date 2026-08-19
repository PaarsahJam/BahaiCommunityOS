using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CommunityOS.Records.Infrastructure.Integration.Documents;

/// <summary>
/// Configuration for the Records → Documents service integration. Records
/// never reads the Documents database; evidence references and document-level
/// hold references are commanded through the Documents API as the
/// <c>communityos-records</c> service principal (ADR-023). Fail-closed: a
/// misconfigured base URL or token causes the outbound command to fail rather
/// than silently leaving the Documents side inconsistent.
/// </summary>
public sealed class DocumentsServiceOptions
{
    public const string SectionName = "DocumentsService";

    /// <summary>Base URL of the Documents service API.</summary>
    public string BaseUrl { get; set; } = string.Empty;

    /// <summary>Bearer service token Records presents to the Documents service.</summary>
    public string AccessToken { get; set; } = string.Empty;

    /// <summary>Client identifier sent as the <c>X-Client-Id</c> header.</summary>
    public string ClientId { get; set; } = "communityos-records";
}

public static class RecordsDocumentsServiceOptionsExtensions
{
    public static IServiceCollection ConfigureRecordsDocumentsService(
        this IServiceCollection services, IConfiguration config)
    {
        services.AddOptions<DocumentsServiceOptions>()
            .Bind(config.GetSection(DocumentsServiceOptions.SectionName));
        return services;
    }
}