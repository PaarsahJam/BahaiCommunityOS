using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CommunityOS.Workflow.Infrastructure.Integration.Community;

/// <summary>
/// Configuration for the Workflow → Community service integration. Workflow
/// never reads the Community database; assignee/person details are resolved
/// through the Community API as the <c>communityos-workflow</c> service
/// principal (ADR-024). Workflow stores only stable person ids. Fail-closed: a
/// misconfigured base URL or token causes the outbound call to fail rather than
/// silently continuing without validation.
/// </summary>
public sealed class CommunityServiceOptions
{
    public const string SectionName = "CommunityService";

    /// <summary>Base URL of the Community service API.</summary>
    public string BaseUrl { get; set; } = string.Empty;

    /// <summary>Bearer service token Workflow presents to the Community service.</summary>
    public string AccessToken { get; set; } = string.Empty;

    /// <summary>Client identifier sent as the <c>X-Client-Id</c> header.</summary>
    public string ClientId { get; set; } = "communityos-workflow";
}

public static class WorkflowCommunityServiceOptionsExtensions
{
    public static IServiceCollection ConfigureWorkflowCommunityService(
        this IServiceCollection services, IConfiguration config)
    {
        services.AddOptions<CommunityServiceOptions>()
            .Bind(config.GetSection(CommunityServiceOptions.SectionName));
        return services;
    }
}