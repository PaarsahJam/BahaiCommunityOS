using CommunityOS.Identity.API.Controllers;
using CommunityOS.Identity.Application.Commands;
using FluentAssertions;
using FluentValidation;
using FluentValidation.Results;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using NSubstitute;
using System.IdentityModel.Tokens.Jwt;
using System.Reflection;
using System.Security.Claims;

namespace CommunityOS.Identity.Tests.API;

public sealed class MeSessionRevokeOthersHttpContractTests
{
    private static readonly Guid AccountId = Guid.NewGuid();
    private static readonly Guid FamilyId = Guid.NewGuid();

    [Fact]
    public async Task RevokeOthers_Returns204NoContent()
    {
        var mediator = Substitute.For<IMediator>();

        var controller = new MeController(mediator)
        {
            ControllerContext = Controller(HttpContextFor(AccountId, FamilyId))
        };

        var result = await controller.RevokeOthers(CancellationToken.None);

        result.Should().BeOfType<NoContentResult>();
    }

    [Fact]
    public async Task RevokeOthers_ResolvesActorExclusivelyFromJwtSubject()
    {
        var mediator = Substitute.For<IMediator>();

        var controller = new MeController(mediator)
        {
            ControllerContext = Controller(HttpContextFor(AccountId, FamilyId))
        };

        await controller.RevokeOthers(CancellationToken.None);

        await mediator.Received(1).Send(
            Arg.Is<RevokeOthersCommand>(c =>
                c.UserAccountId == AccountId && c.CurrentTokenFamilyId == FamilyId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RevokeOthers_ResolvesCurrentFamilyExclusivelyFromSignedSid()
    {
        var mediator = Substitute.For<IMediator>();
        var serverFamilyId = Guid.NewGuid();

        var controller = new MeController(mediator)
        {
            ControllerContext = Controller(HttpContextFor(AccountId, serverFamilyId))
        };

        await controller.RevokeOthers(CancellationToken.None);

        await mediator.Received(1).Send(
            Arg.Is<RevokeOthersCommand>(c =>
                c.CurrentTokenFamilyId == serverFamilyId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public void Endpoint_PostsToRevokeOthersRoute_AndIsAuthenticated_WithNoRequestBodyOrIdentityParameters()
    {
        typeof(MeController).Should().BeDecoratedWith<AuthorizeAttribute>();
        typeof(MeController).Should().BeDecoratedWith<RouteAttribute>(
            a => a.Template == "api/v{version:apiVersion}/me");

        var method = typeof(MeController)
            .GetMethods(BindingFlags.Instance | BindingFlags.Public)
            .Single(m => m.Name == nameof(MeController.RevokeOthers));

        method.Should().BeDecoratedWith<HttpPostAttribute>(
            a => a.Template == "sessions/revoke-others");

        var parameters = method.GetParameters();
        parameters.Should().HaveCount(1);
        parameters[0].ParameterType.Should().Be<CancellationToken>();
        parameters[0].GetCustomAttributes(true).Should().BeEmpty();
    }

    [Fact]
    public async Task RevokeOthers_MissingSid_ThrowsValidationError_AndSendsNoCommand()
    {
        var mediator = Substitute.For<IMediator>();

        var controller = new MeController(mediator)
        {
            ControllerContext = Controller(HttpContextForSubOnly(AccountId))
        };

        var act = async () => await controller.RevokeOthers(CancellationToken.None);

        await act.Should().ThrowAsync<ValidationException>();
        await mediator.DidNotReceiveWithAnyArgs().Send(default!, default);
    }

    [Fact]
    public async Task RevokeOthers_MalformedSid_ThrowsValidationError_AndSendsNoCommand()
    {
        var mediator = Substitute.For<IMediator>();

        var controller = new MeController(mediator)
        {
            ControllerContext = Controller(
                HttpContextFor(AccountId, FamilyId, sidOverride: "not-a-guid"))
        };

        var act = async () => await controller.RevokeOthers(CancellationToken.None);

        await act.Should().ThrowAsync<ValidationException>();
        await mediator.DidNotReceiveWithAnyArgs().Send(default!, default);
    }

    [Fact]
    public async Task MissingSidValidationFailure_MapsTo400ProblemDetails()
    {
        var (status, contentType, _) = await RunMiddlewareAsync(_ =>
            throw new ValidationException([
                new ValidationFailure(
                    nameof(RevokeOthersCommand.CurrentTokenFamilyId),
                    "The current session family claim (sid) is required.")
            ]));

        status.Should().Be(StatusCodes.Status400BadRequest);
        status.Should().NotBe(StatusCodes.Status500InternalServerError);
        contentType.Should().Be("application/problem+json");
    }

    private static ControllerContext Controller(HttpContext httpContext) =>
        new() { HttpContext = httpContext };

    private static DefaultHttpContext HttpContextForSubOnly(Guid accountId) =>
        new()
        {
            User = new ClaimsPrincipal(
                new ClaimsIdentity(new[] { new Claim("sub", accountId.ToString()) }, "test"))
        };

    private static DefaultHttpContext HttpContextFor(
        Guid accountId, Guid familyId, string? sidOverride = null) =>
        new()
        {
            User = new ClaimsPrincipal(
                new ClaimsIdentity(new[]
                {
                    new Claim("sub", accountId.ToString()),
                    new Claim(JwtRegisteredClaimNames.Sid,
                        sidOverride ?? familyId.ToString())
                }, "test"))
        };

    private static async Task<(int Status, string ContentType, string Body)> RunMiddlewareAsync(
        RequestDelegate inner)
    {
        var middleware = Middleware(inner);
        var context = new DefaultHttpContext();
        await using var buffer = new MemoryStream();
        context.Response.Body = buffer;

        await middleware.InvokeAsync(context);

        buffer.Position = 0;
        var body = await new StreamReader(buffer).ReadToEndAsync();
        return (context.Response.StatusCode, context.Response.ContentType ?? string.Empty, body);
    }

    private static ExceptionMiddlewareHost Middleware(RequestDelegate inner)
    {
        var assembly = typeof(MeController).Assembly;
        var middlewareType = assembly.GetType(
            "CommunityOS.Identity.API.Middleware.ExceptionHandlingMiddleware", throwOnError: true)!;
        var logger = Activator.CreateInstance(typeof(EmptyLogger<>).MakeGenericType(middlewareType))!;

        var instance = Activator.CreateInstance(
            middlewareType,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            binder: null,
            args: new[] { inner, logger },
            culture: null)!;

        return new ExceptionMiddlewareHost(instance);
    }

    private sealed class ExceptionMiddlewareHost(object instance)
    {
        private readonly object _instance = instance;
        private readonly MethodInfo _invokeAsync =
            instance.GetType().GetMethod("InvokeAsync", BindingFlags.Instance | BindingFlags.Public)!;

        public Task InvokeAsync(HttpContext context) =>
            (Task)_invokeAsync.Invoke(_instance, new object[] { context })!;
    }

    private sealed class EmptyLogger<T> : ILogger<T>
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => false;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
        }
    }
}