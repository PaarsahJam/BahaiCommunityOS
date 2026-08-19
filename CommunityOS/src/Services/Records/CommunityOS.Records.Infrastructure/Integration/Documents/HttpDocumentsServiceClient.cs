using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using CommunityOS.Records.Application.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CommunityOS.Records.Infrastructure.Integration.Documents;

/// <summary>
/// HTTP <see cref="IDocumentsServiceClient"/> implementing the Records-side
/// command surface over the Documents API (ADR-023). Records never reads the
/// Documents database. <c>POST /documents/{id}/classify</c> is a full-replacement
/// operation, so hold placement/release reads the document's current
/// classification values and resubmits them alongside the changed hold
/// reference. Fail-closed: any non-success or transport failure throws so the
/// Records write never commits with an inconsistent Documents side.
/// </summary>
public sealed class HttpDocumentsServiceClient(
    HttpClient httpClient,
    IOptions<DocumentsServiceOptions> options,
    ILogger<HttpDocumentsServiceClient> logger) : IDocumentsServiceClient
{
    private readonly DocumentsServiceOptions _options = options.Value;
    private readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web);

    public async Task<DocumentClassificationState> GetClassificationAsync(
        Guid documentId, CancellationToken cancellationToken = default)
    {
        var response = await SendAsync(
            HttpMethod.Get, $"/api/v1/documents/{documentId}", null, cancellationToken);

        var dto = await response.Content.ReadFromJsonAsync<DocumentDto>(_json, cancellationToken)
            ?? throw new HttpRequestException("Documents service returned an empty document payload.");

        var c = dto.Classification;
        return new DocumentClassificationState(
            c.ClassificationCode, c.IsSensitive, c.RetentionCategory,
            c.LegalHoldReference, c.AdministrativeHoldReference);
    }

    public async Task ClassifyAsync(
        Guid documentId, DocumentClassificationState state, CancellationToken cancellationToken = default)
    {
        var body = new ClassifyRequestDto(
            state.ClassificationCode,
            state.IsSensitive,
            state.RetentionCategory,
            state.LegalHoldReference,
            state.AdministrativeHoldReference);

        await SendAsync(HttpMethod.Post, $"/api/v1/documents/{documentId}/classify", body, cancellationToken);
    }

    public async Task CreateReferenceAsync(
        Guid documentId,
        string sourceContext,
        Guid sourceEntityId,
        string referenceType,
        CancellationToken cancellationToken = default)
    {
        var body = new CreateReferenceRequestDto(sourceContext, sourceEntityId, referenceType);
        await SendAsync(
            HttpMethod.Post, $"/api/v1/documents/{documentId}/references", body, cancellationToken);
    }

    private async Task<HttpResponseMessage> SendAsync(
        HttpMethod method, string path, object? body, CancellationToken ct)
    {
        var uri = new Uri(_options.BaseUrl.TrimEnd('/') + path);

        using var request = new HttpRequestMessage(method, uri);
        if (body is not null)
            request.Content = JsonContent.Create(body, options: _json);

        request.Headers.Authorization =
            new AuthenticationHeaderValue("Bearer", _options.AccessToken);
        request.Headers.TryAddWithoutValidation("X-Client-Id", _options.ClientId);

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

    private sealed record ClassifyRequestDto(
        string? ClassificationCode,
        bool IsSensitive,
        string? RetentionCategory,
        string? LegalHoldReference,
        string? AdministrativeHoldReference);

    private sealed record CreateReferenceRequestDto(
        string SourceContext, Guid SourceEntityId, string ReferenceType);

    private sealed record DocumentClassificationResponseDto(
        string? ClassificationCode,
        bool IsSensitive,
        string? RetentionCategory,
        string? LegalHoldReference,
        string? AdministrativeHoldReference);

    private sealed record DocumentDto(
        Guid Id, string Title, string Status, DocumentClassificationResponseDto Classification);
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