using CommunityOS.Authorization.Application.Authorization;
using CommunityOS.Authorization.Application.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CommunityOS.Authorization.HttpClient.Integration;

/// <summary>
/// DI helpers for registering the shared <see cref="HttpAuthorizationEvaluator"/>
/// into a consuming service's container. Each service calls
/// <see cref="AddAuthorizationHttpClient(IServiceCollection, IConfiguration, string)"/> with its own <c>clientId</c> and
/// configuration, and the shared evaluator is wired up as the
/// <see cref="IAuthorizationEvaluator"/> implementation (ADR-018/019).
/// </summary>
public static class AuthorizationHttpClientServiceExtensions
{
    /// <summary>
    /// Registers the shared <see cref="HttpAuthorizationEvaluator"/> as the
    /// <see cref="IAuthorizationEvaluator"/> implementation, configures the
    /// underlying HTTP client, and binds
    /// <see cref="AuthorizationHttpEvaluatorOptions"/> from configuration.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="config">The application configuration.</param>
    /// <param name="clientId">
    /// The default <c>X-Client-Id</c> value sent to the Authorization service.
    /// Each consuming service must supply its own unique identifier.
    /// </param>
    public static IServiceCollection AddAuthorizationHttpClient(
        this IServiceCollection services,
        IConfiguration config,
        string clientId)
    {
        services.AddOptions<AuthorizationHttpEvaluatorOptions>()
            .Bind(config.GetSection(AuthorizationHttpEvaluatorOptions.SectionName))
            .Configure(o => o.ClientId = clientId)
            .Validate(o => !string.IsNullOrWhiteSpace(o.BaseUrl),
                "AuthorizationService:BaseUrl is required.");

        services.AddHttpClient<HttpAuthorizationEvaluator>((sp, client) =>
        {
            var opts = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<AuthorizationHttpEvaluatorOptions>>().Value;
            client.BaseAddress = new Uri(opts.BaseUrl);
        });

        services.AddScoped<IAuthorizationEvaluator>(sp =>
            sp.GetRequiredService<HttpAuthorizationEvaluator>());

        return services;
    }

    /// <summary>
    /// Registers the shared <see cref="HttpAuthorizationEvaluator"/> as the
    /// <see cref="IAuthorizationEvaluator"/> implementation and also registers
    /// <see cref="AuthorizationGuard"/> for convenience. Use this overload
    /// when the consuming service also needs the guard facade.
    /// </summary>
    public static IServiceCollection AddAuthorizationHttpClient(
        this IServiceCollection services,
        IConfiguration config,
        string clientId,
        bool includeGuard)
    {
        services.AddAuthorizationHttpClient(config, clientId);

        if (includeGuard)
            services.AddScoped<AuthorizationGuard>();

        return services;
    }
}
