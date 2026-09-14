using CommunityOS.Identity.API.Controllers;
using CommunityOS.Identity.Application.DTOs;
using CommunityOS.Identity.Application.Queries;
using FluentAssertions;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using System.Security.Claims;

namespace CommunityOS.Identity.Tests.API;

public sealed class MeSessionListHttpContractTests
{
    private const string SubClaimType = "sub";
    private const string SidClaimType = "sid";

    private static readonly Guid AccountId = Guid.NewGuid();

    [Fact]
    public async Task List_ReturnsTheQueryResult()
    {
        var dto = new SessionDto(
            Guid.NewGuid(), Guid.NewGuid(), "Back office terminal", "Windows",
            DateTime.UtcNow, DateTime.UtcNow.AddDays(30), DateTime.UtcNow,
            IsActive: true, IsCurrent: true);
        var mediator = Substitute.For<IMediator>();
        mediator.Send(new ListSessionsQuery(AccountId), CancellationToken.None)
            .Returns(new List<SessionDto> { dto });

        var controller = new MeController(mediator)
        {
            ControllerContext = Controller(HttpContextFor(AccountId))
        };

        var result = await controller.ListSessions(CancellationToken.None);

        result.Result.Should().BeOfType<OkObjectResult>()
            .Which.Value.Should().BeEquivalentTo(new[] { dto });
    }

    [Fact]
    public async Task List_ResolvesActorExclusivelyFromJwtSubject()
    {
        var mediator = Substitute.For<IMediator>();
        mediator.Send(Arg.Any<ListSessionsQuery>(), Arg.Any<CancellationToken>())
            .Returns(Array.Empty<SessionDto>());

        var controller = new MeController(mediator)
        {
            ControllerContext = Controller(HttpContextFor(AccountId))
        };

        await controller.ListSessions(CancellationToken.None);

        await mediator.Received(1).Send(
            Arg.Is<ListSessionsQuery>(q => q.UserAccountId == AccountId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task List_ReadsCurrentSessionCorrelation_FromSignedSidClaim()
    {
        var familyId = Guid.NewGuid();
        var mediator = Substitute.For<IMediator>();
        mediator.Send(Arg.Any<ListSessionsQuery>(), Arg.Any<CancellationToken>())
            .Returns(Array.Empty<SessionDto>());

        var controller = new MeController(mediator)
        {
            ControllerContext = Controller(HttpContextFor(AccountId, familyId))
        };

        await controller.ListSessions(CancellationToken.None);

        await mediator.Received(1).Send(
            Arg.Is<ListSessionsQuery>(q =>
                q.UserAccountId == AccountId && q.SessionFamilyId == familyId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task List_WithAbsentOrMalformedSid_LeavesCorrelationNull()
    {
        var mediator = Substitute.For<IMediator>();
        mediator.Send(Arg.Any<ListSessionsQuery>(), Arg.Any<CancellationToken>())
            .Returns(Array.Empty<SessionDto>());

        // No sid claim on this principal.
        var controller = new MeController(mediator)
        {
            ControllerContext = Controller(HttpContextFor(AccountId))
        };

        await controller.ListSessions(CancellationToken.None);

        await mediator.Received(1).Send(
            Arg.Is<ListSessionsQuery>(q => q.SessionFamilyId == null),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task List_WithMalformedSidClaim_LeavesCorrelationNull()
    {
        var mediator = Substitute.For<IMediator>();
        mediator.Send(Arg.Any<ListSessionsQuery>(), Arg.Any<CancellationToken>())
            .Returns(Array.Empty<SessionDto>());

        var controller = new MeController(mediator)
        {
            ControllerContext = Controller(HttpContextFor(AccountId, "not-a-guid"))
        };

        await controller.ListSessions(CancellationToken.None);

        await mediator.Received(1).Send(
            Arg.Is<ListSessionsQuery>(q => q.SessionFamilyId == null),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public void Endpoint_IsAuthenticated_RouteUnchanged_AndTakesNoParameters()
    {
        typeof(MeController).Should().BeDecoratedWith<AuthorizeAttribute>();
        typeof(MeController).Should().BeDecoratedWith<RouteAttribute>(
            a => a.Template == "api/v{version:apiVersion}/me");

        var method = typeof(MeController)
            .GetMethods(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public)
            .Single(m => m.Name == nameof(MeController.ListSessions));

        method.Should().BeDecoratedWith<HttpGetAttribute>(
            a => a.Template == "sessions");

        method.GetParameters().Should().ContainSingle(p => p.ParameterType == typeof(CancellationToken));
    }

    private static ControllerContext Controller(HttpContext httpContext) =>
        new() { HttpContext = httpContext };

    private static DefaultHttpContext HttpContextFor(Guid accountId, Guid? sessionFamilyId = null) =>
        new()
        {
            User = new ClaimsPrincipal(
                new ClaimsIdentity(
                    SidClaim(sessionFamilyId)
                        .Prepend(new Claim(SubClaimType, accountId.ToString())),
                    "test"))
        };

    private static DefaultHttpContext HttpContextFor(Guid accountId, string malformedSid) =>
        new()
        {
            User = new ClaimsPrincipal(
                new ClaimsIdentity(
                    new[] { new Claim(SidClaimType, malformedSid), new Claim(SubClaimType, accountId.ToString()) },
                    "test"))
        };

    private static IEnumerable<Claim> SidClaim(Guid? sessionFamilyId) =>
        sessionFamilyId.HasValue
            ? [new Claim(SidClaimType, sessionFamilyId.Value.ToString())]
            : [];
}