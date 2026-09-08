using CommunityOS.Identity.API.Controllers;
using CommunityOS.Identity.Application.Commands;
using CommunityOS.Identity.Domain.Exceptions;
using FluentAssertions;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using NSubstitute;
using System.Reflection;
using System.Security.Claims;

namespace CommunityOS.Identity.Tests.API;

public sealed class MfaEnrollCompleteHttpContractTests
{
    private const string SubClaimType = "sub";

    private static readonly Guid AccountId = Guid.NewGuid();
    private static readonly Guid MfaMethodId = Guid.NewGuid();
    private const string VerificationCode = "123456";

    [Fact]
    public async Task CompleteEnrollment_Returns204NoContent_ForOwnMethod()
    {
        var mediator = Substitute.For<IMediator>();

        var controller = new MfaController(mediator)
        {
            ControllerContext = Controller(HttpContextFor(AccountId))
        };

        var result = await controller.CompleteEnrollment(
            new CompleteMfaEnrollmentRequest(MfaMethodId, VerificationCode), CancellationToken.None);

        result.Should().BeOfType<NoContentResult>();
    }

    [Fact]
    public async Task CompleteEnrollment_ResolvesActorExclusivelyFromJwtSubject()
    {
        var mediator = Substitute.For<IMediator>();

        var controller = new MfaController(mediator)
        {
            ControllerContext = Controller(HttpContextFor(AccountId))
        };

        await controller.CompleteEnrollment(
            new CompleteMfaEnrollmentRequest(MfaMethodId, VerificationCode), CancellationToken.None);

        await mediator.Received(1).Send(
            Arg.Is<CompleteMfaEnrollmentCommand>(c =>
                c.UserAccountId == AccountId
                && c.MfaMethodId == MfaMethodId
                && c.Code == VerificationCode),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public void Endpoint_IsAuthenticated_AndAcceptsNoClientSuppliedIdentity()
    {
        typeof(MfaController).Should().BeDecoratedWith<AuthorizeAttribute>();
        typeof(MfaController).Should().BeDecoratedWith<RouteAttribute>(
            a => a.Template == "api/v{version:apiVersion}/mfa");

        var method = typeof(MfaController)
            .GetMethods(BindingFlags.Instance | BindingFlags.Public)
            .Single(m => m.Name == nameof(MfaController.CompleteEnrollment));

        method.Should().BeDecoratedWith<HttpPostAttribute>(
            a => a.Template == "enroll/complete");

        var parameters = method.GetParameters();
        parameters.Select(p => p.Name).Should().Equal("request", "ct");

        typeof(CompleteMfaEnrollmentRequest).GetProperties().Select(p => p.Name)
            .Should().BeEquivalentTo(new[]
            {
                nameof(CompleteMfaEnrollmentRequest.MfaMethodId),
                nameof(CompleteMfaEnrollmentRequest.Code)
            });
    }

    [Fact]
    public void CompleteMfaEnrollmentCommand_CarriesOnlyActorAndBodyIdentifiers()
    {
        typeof(CompleteMfaEnrollmentCommand).GetProperties().Select(p => p.Name)
            .Should().BeEquivalentTo(new[]
            {
                nameof(CompleteMfaEnrollmentCommand.UserAccountId),
                nameof(CompleteMfaEnrollmentCommand.MfaMethodId),
                nameof(CompleteMfaEnrollmentCommand.Code)
            });
    }

    [Fact]
    public async Task MfaMethodNotFoundException_MapsTo404Not500()
    {
        var (status, contentType, _) = await RunMiddlewareAsync(_ =>
            throw new MfaMethodNotFoundException(MfaMethodId));

        status.Should().Be(StatusCodes.Status404NotFound);
        status.Should().NotBe(StatusCodes.Status500InternalServerError);
        contentType.Should().Be("application/problem+json");
    }

    [Fact]
    public async Task InvalidMfaCodeException_MapsTo401Not500()
    {
        var (status, contentType, _) = await RunMiddlewareAsync(_ =>
            throw new InvalidMfaCodeException());

        status.Should().Be(StatusCodes.Status401Unauthorized);
        contentType.Should().Be("application/problem+json");
    }

    [Fact]
    public async Task UnknownAndNonOwnedMethods_DoNotProduceDistinguishableStatuses()
    {
        var (unknownStatus, _, _) = await RunMiddlewareAsync(_ =>
            throw new MfaMethodNotFoundException(Guid.NewGuid()));
        var (nonOwnedStatus, _, _) = await RunMiddlewareAsync(_ =>
            throw new MfaMethodNotFoundException(MfaMethodId));

        unknownStatus.Should().Be(StatusCodes.Status404NotFound);
        nonOwnedStatus.Should().Be(StatusCodes.Status404NotFound);
    }

    [Fact]
    public async Task ExistingNotFoundAndUnauthorizedMappings_RemainUnchanged()
    {
        var (accountStatus, _, _) = await RunMiddlewareAsync(_ =>
            throw new UserAccountNotFoundException(AccountId));
        accountStatus.Should().Be(StatusCodes.Status404NotFound);

        var (sessionStatus, _, _) = await RunMiddlewareAsync(_ =>
            throw new SessionNotFoundException(Guid.NewGuid()));
        sessionStatus.Should().Be(StatusCodes.Status404NotFound);
    }

    [Fact]
    public async Task UnknownException_Remains500()
    {
        var (status, _, _) = await RunMiddlewareAsync(_ =>
            throw new InvalidOperationException("boom"));

        status.Should().Be(StatusCodes.Status500InternalServerError);
    }

    private static ControllerContext Controller(HttpContext httpContext) =>
        new() { HttpContext = httpContext };

    private static DefaultHttpContext HttpContextFor(Guid accountId) =>
        new()
        {
            User = new ClaimsPrincipal(
                new ClaimsIdentity(new[] { new Claim(SubClaimType, accountId.ToString()) }, "test"))
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
        // The Identity exception-handling middleware is internal; it is
        // constructed reflectively so the real status-mapping switch (the
        // exact code the HTTP pipeline runs) is exercised without opening the
        // production assembly to tests.
        var assembly = typeof(MfaController).Assembly;
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