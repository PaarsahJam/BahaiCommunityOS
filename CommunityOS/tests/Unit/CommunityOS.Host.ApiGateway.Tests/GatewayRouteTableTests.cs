using CommunityOS.Host.ApiGateway.Forwarding;
using FluentAssertions;
using Xunit;

namespace CommunityOS.Host.ApiGateway.Tests;

public class GatewayRouteTableTests
{
    [Theory]
    [InlineData("/api/v1/auth/login", DownstreamService.Identity)]
    [InlineData("/api/v1/connect/token", DownstreamService.Identity)]
    [InlineData("/api/v1/.well-known/openid-configuration", DownstreamService.Identity)]
    [InlineData("/api/v1/me", DownstreamService.Identity)]
    [InlineData("/api/v1/account/password", DownstreamService.Identity)]
    [InlineData("/api/v1/mfa/status", DownstreamService.Identity)]
    [InlineData("/api/v1/authz/check", DownstreamService.Authorization)]
    [InlineData("/api/v1/organizations", DownstreamService.Organization)]
    [InlineData("/api/v1/orgunits/1", DownstreamService.Organization)]
    [InlineData("/api/v1/committees", DownstreamService.Organization)]
    [InlineData("/api/v1/appointments", DownstreamService.Organization)]
    [InlineData("/api/v1/institutions", DownstreamService.Organization)]
    [InlineData("/api/v1/delegations", DownstreamService.Organization)]
    [InlineData("/api/v1/persons/1", DownstreamService.Community)]
    [InlineData("/api/v1/my-person", DownstreamService.Community)]
    [InlineData("/api/v1/households", DownstreamService.Community)]
    [InlineData("/api/v1/memberships", DownstreamService.Community)]
    [InlineData("/api/v1/meetings", DownstreamService.Community)]
    [InlineData("/api/v1/activities", DownstreamService.Community)]
    [InlineData("/api/v1/community-events", DownstreamService.Community)]
    [InlineData("/api/v1/calendar", DownstreamService.Community)]
    [InlineData("/api/v1/communities", DownstreamService.Community)]
    [InlineData("/api/v1/family-relationships", DownstreamService.Community)]
    [InlineData("/api/v1/participations", DownstreamService.Community)]
    [InlineData("/api/v1/my-notifications", DownstreamService.Notifications)]
    [InlineData("/api/v1/my-notifications/unread-count", DownstreamService.Notifications)]
    [InlineData("/api/v1/my-notifications/00000000-0000-0000-0000-000000000000/read", DownstreamService.Notifications)]
    public void TryResolve_PublicRoutes_ResolvesToService(string path, DownstreamService expected)
    {
        GatewayRouteTable.TryResolve(path, out var service).Should().BeTrue();
        service.Should().Be(expected);
    }

    [Fact]
    public void TryResolve_PreservesAnyApiVersion()
    {
        foreach (var version in new[] { "v1", "v2", "v9" })
        {
            var path = $"/api/{version}/persons/42";
            GatewayRouteTable.TryResolve(path, out var service).Should().BeTrue();
            service.Should().Be(DownstreamService.Community);
        }
    }

    [Theory]
    [InlineData("/")]
    [InlineData("/health")]
    [InlineData("/api")]
    [InlineData("/api/v1")]
    [InlineData("/api/v1/unknown")]
    [InlineData("/api/v1/gateway")]
    [InlineData("/api/v1/notifications")]
    [InlineData("/api/v1/notifications/00000000-0000-0000-0000-000000000000")]
    [InlineData("/api/v1/notifications/00000000-0000-0000-0000-000000000000/sensitive")]
    [InlineData("/swagger")]
    [InlineData("/not-an-api/persons")]
    public void TryResolve_UnknownOrNonApiPaths_ReturnsFalse(string path)
    {
        GatewayRouteTable.TryResolve(path, out _).Should().BeFalse();
    }

    [Theory]
    [InlineData("/api/v1/orgunits/{00000000-0000-0000-0000-000000000000}/covers")]
    [InlineData("/api/v1/orgunits/{00000000-0000-0000-0000-000000000000}/covers/details")]
    public void IsInternalRoute_OrganizationCovers_IsInternal(string path)
    {
        GatewayRouteTable.IsInternalRoute(path).Should().BeTrue();
    }

    [Theory]
    [InlineData("/api/v1/orgunits/1")]
    [InlineData("/api/v1/orgunits/1/members")]
    [InlineData("/api/v1/committees/1")]
    public void IsInternalRoute_PublicOrganizationRoutes_AreNotInternal(string path)
    {
        GatewayRouteTable.IsInternalRoute(path).Should().BeFalse();
    }

    [Theory]
    [InlineData("auth")]
    [InlineData("connect")]
    [InlineData(".well-known")]
    public void IsAnonymousIdentitySegment_True_ForIdentityAnonymousSegments(string segment)
    {
        GatewayRouteTable.IsAnonymousIdentitySegment(segment).Should().BeTrue();
    }

    [Theory]
    [InlineData("me")]
    [InlineData("account")]
    [InlineData("mfa")]
    [InlineData("my-person")]
    [InlineData("persons")]
    public void IsAnonymousIdentitySegment_False_ForAuthenticatedSegments(string segment)
    {
        GatewayRouteTable.IsAnonymousIdentitySegment(segment).Should().BeFalse();
    }
}
