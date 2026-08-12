using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using CommunityOS.Authorization.Application.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CommunityOS.Authorization.Infrastructure.Integration.Organization;

/// <summary>
/// HTTP <see cref="IOrganizationContextProvider"/> that resolves organization
/// hierarchy facts from the Organization service's narrow covers endpoint. The
/// Authorization service must never access the Organization database; facts are
/// always answered by the owning service over its API (ADR-018). Fail-closed:
/// any transport, authentication or validation failure denies.
/// </summary>
public sealed class HttpOrganizationContextProvider(
    HttpClient httpClient,
    IOptions<OrganizationServiceOptions> options,
    ILogger<HttpOrganizationContextProvider> logger) : IOrganizationContextProvider
{
    private readonly OrganizationServiceOptions _options = options.Value;
    private readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web);

    public async Task<bool> IsAncestorOrSelfAsync(
        Guid candidateAncestorId, Guid orgUnitId, CancellationToken ct = default)
    {
        var uri = new Uri(
            _options.BaseUrl.TrimEnd('/')
            + $"/api/v1/orgunits/{orgUnitId}/covers?ancestorId={candidateAncestorId}");

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, uri);
            request.Headers.Authorization =
                new AuthenticationHeaderValue("Bearer", _options.AccessToken);
            request.Headers.TryAddWithoutValidation("X-Client-Id", _options.ClientId);

            using var response = await httpClient.SendAsync(request, ct);

            if (!response.IsSuccessStatusCode)
            {
                logger.CoversCheckFailed(response.StatusCode, orgUnitId, candidateAncestorId);
                return false;
            }

            var result = await response.Content.ReadFromJsonAsync<OrganizationUnitCoverageDto>(_json, ct);
            if (result is null)
                return false;

            return result.Covers;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            logger.CoversCheckTransportError(ex);
            return false;
        }
    }

    private sealed record OrganizationUnitCoverageDto(
        Guid OrganizationUnitId,
        Guid CandidateAncestorId,
        bool Covers,
        DateTime EvaluatedOn);
}

internal static partial class HttpOrganizationContextProviderLogging
{
    [LoggerMessage(
        EventId = 0,
        Level = LogLevel.Warning,
        Message = "Organization covers check for unit '{OrganizationUnitId}' and candidate '{CandidateAncestorId}' failed with HTTP {StatusCode}.")]
    public static partial void CoversCheckFailed(
        this ILogger logger, System.Net.HttpStatusCode statusCode, Guid organizationUnitId, Guid candidateAncestorId);

    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Error,
        Message = "Organization covers check transport error (Organization service unavailable or malformed response).")]
    public static partial void CoversCheckTransportError(this ILogger logger, Exception exception);
}