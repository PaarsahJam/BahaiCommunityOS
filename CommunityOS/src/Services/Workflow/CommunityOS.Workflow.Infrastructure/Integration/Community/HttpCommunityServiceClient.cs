using CommunityOS.ServiceClients;
using CommunityOS.Workflow.Application.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CommunityOS.Workflow.Infrastructure.Integration.Community;

/// <summary>
/// HTTP <see cref="ICommunityServiceClient"/> implementing the Workflow-side
/// read surface over the Community API (ADR-024). Workflow never reads the
/// Community database; assignee/person details are resolved through the API as
/// the <c>communityos-workflow</c> service principal. Only stable person ids
/// are used — names are never stored. Fail-closed: any non-success or transport
/// failure throws.
/// </summary>
public sealed class HttpCommunityServiceClient(
    HttpClient httpClient,
    IOptions<CommunityServiceOptions> options,
    ILogger<HttpCommunityServiceClient> logger) : ICommunityServiceClient
{
    private readonly CommunityServiceOptions _options = options.Value;

    public async Task<bool> PersonExistsAsync(Guid personId, CancellationToken cancellationToken = default)
    {
        var path = $"/api/v1/persons/{personId}";

        using var request = ServiceHttpClient.CreateRequest(_options, HttpMethod.Get, path);

        HttpResponseMessage response;
        try
        {
            response = await httpClient.SendAsync(request, cancellationToken);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            logger.CommunityServiceUnreachable(ex);
            throw;
        }

        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            return false;

        if (!response.IsSuccessStatusCode)
        {
            logger.CommunityServiceRejected(path, response.StatusCode);
            throw new HttpRequestException(
                $"Community service rejected GET {path} with HTTP {(int)response.StatusCode}.");
        }

        return true;
    }
}

internal static partial class HttpCommunityServiceClientLogging
{
    [LoggerMessage(
        EventId = 0,
        Level = LogLevel.Error,
        Message = "Community service unreachable.")]
    public static partial void CommunityServiceUnreachable(this ILogger logger, Exception exception);

    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Warning,
        Message = "Community service rejected {Path} with HTTP {StatusCode}.")]
    public static partial void CommunityServiceRejected(
        this ILogger logger, string path, System.Net.HttpStatusCode statusCode);
}