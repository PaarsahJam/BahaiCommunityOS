using CommunityOS.Authorization.Domain.Exceptions;
using CommunityOS.Community.API.Controllers;
using CommunityOS.Community.Application.DTOs;
using CommunityOS.Community.Application.Queries;
using CommunityOS.Community.Domain.Exceptions;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using NSubstitute;
using System.Reflection;
using System.Security.Claims;
using System.Text.Json;

namespace CommunityOS.Community.Tests.API;

public class MyPersonHttpContractTests
{
    private const string SubClaimType = "sub";

    private static readonly Guid AccountId = Guid.NewGuid();
    private static readonly Guid PersonId = Guid.NewGuid();

    private static readonly PersonDto LinkedPerson = new(
        PersonId, "Nur", "Nur Saltanat", null, "active", true, DateTime.UtcNow);

    // Case A — authenticated linked account -> 200 OK + PersonDto.

    [Fact]
    public async Task Get_returns_200_ok_with_linked_person_dto()
    {
        var sender = Substitute.For<ISender>();
        sender.Send(Arg.Any<GetMyPersonQuery>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(LinkedPerson));

        var controller = new MyPersonController(sender)
        {
            ControllerContext = Controller(HttpContextFor(AccountId))
        };

        var result = await controller.Get(CancellationToken.None);

        result.Should().BeOfType<OkObjectResult>();
        ((OkObjectResult)result).Value.Should().BeSameAs(LinkedPerson);
    }

    // Case C — the endpoint resolves exclusively from the JWT subject.

    [Fact]
    public async Task Get_resolves_exclusively_from_jwt_subject()
    {
        var sender = Substitute.For<ISender>();
        sender.Send(Arg.Any<GetMyPersonQuery>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(LinkedPerson));

        var controller = new MyPersonController(sender)
        {
            ControllerContext = Controller(HttpContextFor(AccountId))
        };

        await controller.Get(CancellationToken.None);

        await sender.Received(1).Send(
            Arg.Is<GetMyPersonQuery>(q => q.IdentityAccountId == AccountId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public void Endpoint_accepts_no_caller_supplied_person_or_account_identifier()
    {
        typeof(MyPersonController).Should().BeDecoratedWith<AuthorizeAttribute>();
        typeof(MyPersonController).Should().BeDecoratedWith<RouteAttribute>(
            a => a.Template == "api/v{version:apiVersion}/my-person");

        var method = typeof(MyPersonController)
            .GetMethods(BindingFlags.Instance | BindingFlags.Public)
            .Single(m => m.Name == nameof(MyPersonController.Get));

        method.Should().BeDecoratedWith<HttpGetAttribute>();
        var parameters = method.GetParameters();
        parameters.Should().ContainSingle();
        parameters[0].ParameterType.Should().Be<CancellationToken>();
        parameters[0].GetCustomAttributes(true).Should().BeEmpty();
    }

    [Fact]
    public void MyPersonQuery_carries_only_the_identity_account_id()
    {
        // No PersonId / UserAccountId field can be smuggled into the query.
        typeof(GetMyPersonQuery).GetProperties()
            .Should().ContainSingle()
            .Which.Name.Should().Be(nameof(GetMyPersonQuery.IdentityAccountId));
    }

    // Case B — unlinked account -> HTTP 404 problem+json, never 500.

    [Fact]
    public async Task Unlinked_account_maps_to_404_not_found_not_500()
    {
        var (status, contentType, body) = await RunMiddlewareAsync(_ =>
            throw new PersonNotLinkedToAccountException(AccountId));

        status.Should().Be(StatusCodes.Status404NotFound);
        status.Should().NotBe(StatusCodes.Status500InternalServerError);

        contentType.Should().Be("application/problem+json");

        var json = JsonDocument.Parse(body);
        json.RootElement.GetProperty("status").GetInt32().Should().Be(404);
        json.RootElement.GetProperty("title").GetString()
            .Should().Be($"No person is linked to Identity account '{AccountId}'.");
    }

    [Fact]
    public async Task Existing_not_found_mappings_are_unchanged()
    {
        var (status, _, _) = await RunMiddlewareAsync(_ =>
            throw new PersonNotFoundException(PersonId));

        status.Should().Be(StatusCodes.Status404NotFound);
    }

    [Fact]
    public async Task Forbidden_mapping_is_unchanged()
    {
        var (status, _, _) = await RunMiddlewareAsync(_ =>
            throw new AuthorizationForbiddenException("community.person.read"));

        status.Should().Be(StatusCodes.Status403Forbidden);
    }

    [Fact]
    public async Task Unauthenticated_mapping_is_unchanged()
    {
        var (status, _, _) = await RunMiddlewareAsync(_ =>
            throw new UnauthorizedAccessException());

        status.Should().Be(StatusCodes.Status401Unauthorized);
    }

    [Fact]
    public async Task Unknown_exception_remains_500()
    {
        var (status, _, _) = await RunMiddlewareAsync(_ =>
            throw new InvalidOperationException("boom"));

        status.Should().Be(StatusCodes.Status500InternalServerError);
    }

    [Fact]
    public async Task Successful_response_passes_through_untransformed()
    {
        RequestDelegate next = context =>
        {
            context.Response.StatusCode = StatusCodes.Status200OK;
            context.Response.ContentType = "application/json";
            return context.Response.WriteAsync("{\"ok\":true}");
        };

        var middleware = Middleware(next);
        var context = new DefaultHttpContext();
        await using var buffer = new MemoryStream();
        context.Response.Body = buffer;

        await middleware.InvokeAsync(context);

        buffer.Position = 0;
        var body = await new StreamReader(buffer).ReadToEndAsync();

        context.Response.StatusCode.Should().Be(StatusCodes.Status200OK);
        context.Response.ContentType.Should().Be("application/json");
        body.Should().Be("{\"ok\":true}");
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
        // The Community exception-handling middleware is internal; it is
        // constructed reflectively so the real status-mapping switch (the
        // exact code the HTTP pipeline runs) is exercised without opening the
        // production assembly to tests.
        var assembly = typeof(CommunityOS.Community.API.Security.CommunityJwtValidation).Assembly;
        var middlewareType = assembly.GetType(
            "CommunityOS.Community.API.Middleware.ExceptionHandlingMiddleware", throwOnError: true)!;
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