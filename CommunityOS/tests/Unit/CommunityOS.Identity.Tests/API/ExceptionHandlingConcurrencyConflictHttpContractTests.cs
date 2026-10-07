using CommunityOS.Identity.API.Controllers;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Reflection;
using System.Text.Json;

namespace CommunityOS.Identity.Tests.API;

public sealed class ExceptionHandlingConcurrencyConflictHttpContractTests
{
    [Fact]
    public async Task DbUpdateConcurrencyException_MapsTo409Conflict_Not500()
    {
        var (status, contentType, _) = await RunMiddlewareAsync(_ =>
            throw new DbUpdateConcurrencyException("Database operation expected to affect 1 row(s)."));

        status.Should().Be(StatusCodes.Status409Conflict);
        status.Should().NotBe(StatusCodes.Status500InternalServerError);
        contentType.Should().Be("application/problem+json");
    }

    [Fact]
    public async Task DbUpdateConcurrencyException_BodyCarriesConflictTitleAndStatus()
    {
        var (_, _, body) = await RunMiddlewareAsync(_ =>
            throw new DbUpdateConcurrencyException("Database operation expected to affect 1 row(s)."));

        using var document = JsonDocument.Parse(body);
        document.RootElement.GetProperty("title").GetString().Should().Be("Concurrency conflict.");
        document.RootElement.GetProperty("status").GetInt32()
            .Should().Be(StatusCodes.Status409Conflict);
    }

    [Fact]
    public async Task UnmappedException_StillReturns500DefaultArmUnchanged()
    {
        var (status, _, _) = await RunMiddlewareAsync(_ =>
            throw new InvalidOperationException("boom"));

        status.Should().Be(StatusCodes.Status500InternalServerError);
    }

    [Fact]
    public async Task KnownConflictExceptions_Remain409Unchanged()
    {
        var (status, _, _) = await RunMiddlewareAsync(_ =>
            throw new CommunityOS.Identity.Domain.Exceptions.DuplicateEmailException(
                "user@example.com"));

        status.Should().Be(StatusCodes.Status409Conflict);
    }

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
