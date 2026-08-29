using CommunityOS.ServiceClients;
using CommunityOS.Workflow.Application.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CommunityOS.Workflow.Infrastructure.Integration.Documents;

/// <summary>
/// HTTP <see cref="IDocumentsServiceClient"/> implementing the Workflow-side
/// command surface over the Documents API (ADR-024). Workflow never reads the
/// Documents database; task document references are mirrored through
/// <c>POST /documents/{id}/references</c> with
/// <c>SourceContext = "workflow.task"</c>. Fail-closed: any non-success or
/// transport failure throws so a Workflow write never commits with an
/// inconsistent Documents side.
/// </summary>
public sealed class HttpDocumentsServiceClient(
    HttpClient httpClient,
    IOptions<DocumentsServiceOptions> options,
    ILogger<HttpDocumentsServiceClient> logger) : IDocumentsServiceClient
{
    private readonly DocumentsServiceOptions _options = options.Value;

    public async Task CreateTaskReferenceAsync(
        Guid documentId,
        Guid taskId,
        string referenceType,
        CancellationToken cancellationToken = default)
    {
        var body = new CreateReferenceRequestDto("workflow.task", taskId, referenceType);
        await SendAsync(
            HttpMethod.Post, $"/api/v1/documents/{documentId}/references", body, cancellationToken);
    }

    private async Task<HttpResponseMessage> SendAsync(
        HttpMethod method, string path, object? body, CancellationToken ct)
    {
        using var request = ServiceHttpClient.CreateRequest(_options, method, path, body);

        HttpResponseMessage response;
        try
        {
            response = await httpClient.SendAsync(request, ct);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            logger.DocumentsServiceUnreachable(method, path, ex);
            throw;
        }

        if (!response.IsSuccessStatusCode)
        {
            logger.DocumentsServiceRejected(method, path, response.StatusCode);
            throw new HttpRequestException(
                $"Documents service rejected {method} {path} with HTTP {(int)response.StatusCode}.");
        }

        return response;
    }

    private sealed record CreateReferenceRequestDto(
        string SourceContext, Guid SourceEntityId, string ReferenceType);
}

internal static partial class HttpDocumentsServiceClientLogging
{
    [LoggerMessage(
        EventId = 0,
        Level = LogLevel.Error,
        Message = "Documents service unreachable for {Method} {Path}.")]
    public static partial void DocumentsServiceUnreachable(
        this ILogger logger, HttpMethod method, string path, Exception exception);

    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Warning,
        Message = "Documents service rejected {Method} {Path} with HTTP {StatusCode}.")]
    public static partial void DocumentsServiceRejected(
        this ILogger logger, HttpMethod method, string path, System.Net.HttpStatusCode statusCode);
}