using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using CommunityOS.Authorization.Application.Authorization;
using CommunityOS.Authorization.Application.Interfaces;
using CommunityOS.Organization.Infrastructure.Integration.Authorization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CommunityOS.Organization.Infrastructure.Integration.Authorization;

/// <summary>
/// HTTP <see cref="IAuthorizationEvaluator"/> that forwards authorization
/// requests to the Authorization service's check API. The Organization service
/// must never access the Authorization database; decisions are always made by
/// the owning service over its API (ADR-018). Fail-closed: any transport,
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

        try
        {
            var checkRequest = new AuthorizationCheckRequest(
                request.SubjectId,
                request.Permission,
                request.Context.OrganizationUnitId,
                request.Context.ResourceType,
                request.Context.ResourceId,
                decisionId,
                request.Context.Attributes);

            using var message = CreateRequest("/api/v1/authz/check", checkRequest);
            using var response = await httpClient.SendAsync(message, ct);

            if (!response.IsSuccessStatusCode)
            {
                logger.FailedAuthorizationCheck(response.StatusCode, request.Permission, request.SubjectId);
                return AuthorizationDecision.Deny(decisionId, AuthorizationDecisionReason.DeniedByDefault, now);
            }

            var result = await response.Content.ReadFromJsonAsync<AuthorizationCheckDto>(_json, ct);
            if (result is null)
                return AuthorizationDecision.Deny(decisionId, AuthorizationDecisionReason.DeniedByDefault, now);

            return result.Allowed
                ? AuthorizationDecision.Allow(result.DecisionId, [result.DecisionId], result.EvaluatedOn)
                : AuthorizationDecision.Deny(
                    result.DecisionId,
                    MapReason(result.Reason),
                    result.EvaluatedOn);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            logger.AuthorizationCheckTransportError(ex);
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

    private HttpRequestMessage CreateRequest(string path, object body)
    {
        var message = new HttpRequestMessage(HttpMethod.Post, path)
        {
            Content = JsonContent.Create(body, options: _json)
        };
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.AccessToken);
        message.Headers.TryAddWithoutValidation("X-Client-Id", _options.ClientId);
        return message;
    }

    private static AuthorizationDecisionReason MapReason(string? reason) => reason switch
    {
        "allowed" => AuthorizationDecisionReason.Allowed,
        "missing_subject" => AuthorizationDecisionReason.MissingSubject,
        "invalid_permission" => AuthorizationDecisionReason.InvalidPermission,
        "no_permission" => AuthorizationDecisionReason.NoPermission,
        "scope_mismatch" => AuthorizationDecisionReason.ScopeMismatch,
        _ => AuthorizationDecisionReason.DeniedByDefault
    };

    private sealed record AuthorizationCheckRequest(
        Guid SubjectId,
        string Permission,
        Guid? OrganizationUnitId,
        string? ResourceType,
        Guid? ResourceId,
        string? CheckKey,
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
        Message = "Authorization check for '{Permission}' on subject '{SubjectId}' failed with HTTP {StatusCode}.")]
    public static partial void FailedAuthorizationCheck(
        this ILogger logger, System.Net.HttpStatusCode statusCode, string permission, Guid subjectId);

    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Error,
        Message = "Authorization check transport error (Authorization service unavailable or malformed response).")]
    public static partial void AuthorizationCheckTransportError(this ILogger logger, Exception exception);
}