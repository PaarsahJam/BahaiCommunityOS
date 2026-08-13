using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using CommunityOS.Authorization.Application.Authorization;
using CommunityOS.Authorization.Application.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CommunityOS.Knowledge.Infrastructure.Integration.Authorization;

/// <summary>
/// HTTP <see cref="IAuthorizationEvaluator"/> that delegates every decision to
/// the Authorization service's check endpoint. The Knowledge service must never
/// access the Authorization database; decisions are always answered by the
/// owning service over its API (ADR-018). Fail-closed: any transport,
/// authentication or validation failure denies.
/// </summary>
public sealed class HttpAuthorizationEvaluator(
    HttpClient httpClient,
    IOptions<AuthorizationServiceOptions> options,
    ILogger<HttpAuthorizationEvaluator> logger) : IAuthorizationEvaluator
{
    private readonly AuthorizationServiceOptions _options = options.Value;
    private readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web);

    public async Task<AuthorizationDecision> EvaluateAsync(
        AuthorizationRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var decisionId = Guid.NewGuid().ToString("N");
        var now = DateTime.UtcNow;

        if (request.SubjectId == Guid.Empty)
            return AuthorizationDecision.Deny(decisionId, AuthorizationDecisionReason.MissingSubject, now);

        var uri = new Uri(_options.BaseUrl.TrimEnd('/') + "/api/v1/authz/check");

        try
        {
            using var httpRequest = new HttpRequestMessage(HttpMethod.Post, uri)
            {
                Content = JsonContent.Create(new CheckRequestDto(
                    request.SubjectId,
                    request.Permission,
                    request.Context.OrganizationUnitId,
                    request.Context.ResourceType,
                    request.Context.ResourceId,
                    request.Context.Attributes), options: _json)
            };

            httpRequest.Headers.Authorization =
                new AuthenticationHeaderValue("Bearer", _options.AccessToken);
            httpRequest.Headers.TryAddWithoutValidation("X-Client-Id", _options.ClientId);

            using var response = await httpClient.SendAsync(httpRequest, ct);

            if (!response.IsSuccessStatusCode)
            {
                logger.CheckFailed(response.StatusCode, request.SubjectId, request.Permission);
                return AuthorizationDecision.Deny(decisionId, AuthorizationDecisionReason.DeniedByDefault, now);
            }

            var result = await response.Content.ReadFromJsonAsync<AuthorizationCheckDto>(_json, ct);
            if (result is null)
                return AuthorizationDecision.Deny(decisionId, AuthorizationDecisionReason.DeniedByDefault, now);

            if (result.Allowed)
                return AuthorizationDecision.Allow(
                    result.DecisionId, [request.Permission], result.EvaluatedOn);

            return AuthorizationDecision.Deny(
                result.DecisionId, MapReason(result.Reason), result.EvaluatedOn);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            logger.CheckTransportError(ex);
            return AuthorizationDecision.Deny(decisionId, AuthorizationDecisionReason.DeniedByDefault, now);
        }
    }

    public async Task<IReadOnlyList<AuthorizationDecision>> EvaluateBatchAsync(
        IReadOnlyList<AuthorizationRequest> requests, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(requests);

        var decisions = new List<AuthorizationDecision>(requests.Count);
        foreach (var request in requests)
            decisions.Add(await EvaluateAsync(request, ct));

        return decisions;
    }

    private static AuthorizationDecisionReason MapReason(string reason) =>
        Enum.TryParse<AuthorizationDecisionReason>(reason, ignoreCase: true, out var parsed)
            ? parsed
            : AuthorizationDecisionReason.DeniedByDefault;

    private sealed record CheckRequestDto(
        Guid? SubjectId,
        string Permission,
        Guid? OrganizationUnitId,
        string? ResourceType,
        Guid? ResourceId,
        IReadOnlyDictionary<string, string>? Attributes);

    private sealed record AuthorizationCheckDto(
        string CheckKey,
        bool Allowed,
        string DecisionId,
        string Reason,
        DateTime EvaluatedOn);
}

internal static partial class HttpAuthorizationEvaluatorLogging
{
    [LoggerMessage(
        EventId = 0,
        Level = LogLevel.Warning,
        Message = "Authorization check for subject '{SubjectId}' permission '{Permission}' failed with HTTP {StatusCode}.")]
    public static partial void CheckFailed(
        this ILogger logger, System.Net.HttpStatusCode statusCode, Guid subjectId, string permission);

    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Error,
        Message = "Authorization check transport error (Authorization service unavailable or malformed response).")]
    public static partial void CheckTransportError(this ILogger logger, Exception exception);
}