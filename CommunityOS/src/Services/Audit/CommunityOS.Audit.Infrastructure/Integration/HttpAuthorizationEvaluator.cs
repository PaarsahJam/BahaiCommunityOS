using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using CommunityOS.Authorization.Application.Authorization;
using CommunityOS.Authorization.Application.Interfaces;
using Microsoft.Extensions.Options;

namespace CommunityOS.Audit.Infrastructure.Integration;

public sealed class AuditAuthorizationOptions
{
    /// <summary>Base URL of the Authorization service.</summary>
    public string BaseUrl { get; set; } = "";

    public string AccessToken { get; set; } = "";

    public string ClientId { get; set; } = "communityos-audit";
}

/// <summary>
/// HTTP adapter over the Authorization service's check endpoint (Search
/// pattern). Any transport or protocol failure fails closed: the caller
/// receives a deny decision rather than an exception (ADR-027 decision 11 —
/// the audit boundary must never leak on infrastructure faults).
/// </summary>
public sealed class HttpAuthorizationEvaluator(HttpClient client, IOptions<AuditAuthorizationOptions> options)
    : IAuthorizationEvaluator
{
    private readonly AuditAuthorizationOptions settings = options.Value;

    public async Task<AuthorizationDecision> EvaluateAsync(AuthorizationRequest request, CancellationToken ct = default)
    {
        if (request.SubjectId == Guid.Empty || string.IsNullOrWhiteSpace(settings.BaseUrl))
        {
            return Deny();
        }

        try
        {
            using var msg = new HttpRequestMessage(HttpMethod.Post, "/api/v1/authz/check")
            {
                Content = JsonContent.Create(new
                {
                    request.SubjectId,
                    request.Permission,
                    OrganizationUnitId = request.Context.OrganizationUnitId,
                    ResourceType = request.Context.ResourceType,
                    ResourceId = request.Context.ResourceId
                })
            };
            msg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", settings.AccessToken);
            msg.Headers.TryAddWithoutValidation("X-Client-Id", settings.ClientId);
            using var response = await client.SendAsync(msg, ct);
            if (!response.IsSuccessStatusCode) return Deny();
            var dto = await response.Content.ReadFromJsonAsync<DecisionDto>(ct);
            return dto?.Allowed == true
                ? AuthorizationDecision.Allow(dto.DecisionId, [dto.DecisionId], dto.EvaluatedOn)
                : Deny();
        }
        catch (HttpRequestException)
        {
            return Deny();
        }
        catch (JsonException)
        {
            return Deny();
        }
    }

    public async Task<IReadOnlyList<AuthorizationDecision>> EvaluateBatchAsync(
        IReadOnlyList<AuthorizationRequest> requests,
        CancellationToken ct = default) =>
        [.. await Task.WhenAll(requests.Select(x => EvaluateAsync(x, ct)))];

    private static AuthorizationDecision Deny() => AuthorizationDecision.Deny(
        Guid.NewGuid().ToString("N"), AuthorizationDecisionReason.DeniedByDefault, DateTime.UtcNow);

    private sealed record DecisionDto(string DecisionId, bool Allowed, DateTime EvaluatedOn);
}
