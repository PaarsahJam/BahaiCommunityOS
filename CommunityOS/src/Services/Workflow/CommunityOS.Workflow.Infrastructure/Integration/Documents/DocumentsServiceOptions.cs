using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CommunityOS.Workflow.Infrastructure.Integration.Documents;

/// <summary>
/// Configuration for the Workflow → Documents service integration. Workflow
/// never reads the Documents database; task document references are created
/// through the Documents API as the <c>communityos-workflow</c> service
/// principal (ADR-024). Fail-closed: a misconfigured base URL or token causes
/// the outbound command to fail rather than silently leaving the Documents side
/// inconsistent.
/// </summary>
public sealed class DocumentsServiceOptions
{
    public const string SectionName = "DocumentsService";

    /// <summary>Base URL of the Documents service API.</summary>
    public string BaseUrl { get; set; } = string.Empty;

    /// <summary>Bearer service token Workflow presents to the Documents service.</summary>
    public string AccessToken { get; set; } = string.Empty;

    /// <summary>Client identifier sent as the <c>X-Client-Id</c> header.</summary>
    public string ClientId { get; set; } = "communityos-workflow";
}

public static class WorkflowDocumentsServiceOptionsExtensions
{
    public static IServiceCollection ConfigureWorkflowDocumentsService(
        this IServiceCollection services, IConfiguration config)
    {
        services.AddOptions<DocumentsServiceOptions>()
            .Bind(config.GetSection(DocumentsServiceOptions.SectionName));
        return services;
    }
}