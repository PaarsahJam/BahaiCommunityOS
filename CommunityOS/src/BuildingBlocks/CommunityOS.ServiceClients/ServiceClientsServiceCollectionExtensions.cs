using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace CommunityOS.ServiceClients;

/// <summary>
/// DI registration helpers for internal service-to-service typed HTTP clients
/// (ADR-031). The helper centralizes the repeated mechanics shared by every
/// internal consumer: binding the options section, wiring an
/// <see cref="HttpClient"/> with the target <see cref="ServiceClientOptions.BaseUrl"/>
/// as its base address, and failing closed when the base URL is not configured.
/// </summary>
public static class ServiceClientsServiceCollectionExtensions
{
    /// <summary>
    /// Registers a typed internal service-to-service HTTP client.
    /// The client's <see cref="ServiceClientOptions"/> type is bound from the
    /// requested configuration section and the underlying
    /// <see cref="HttpClient.BaseAddress"/> is set from the configured base URL.
    /// An unconfigured base URL fails closed with
    /// <see cref="InvalidOperationException"/> when the client is first resolved
    /// rather than producing a confusing <see cref="UriFormatException"/>.
    /// </summary>
    /// <typeparam name="TClient">The typed client implementation.</typeparam>
    /// <typeparam name="TOptions">The client's options type deriving from <see cref="ServiceClientOptions"/>.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <param name="config">The application configuration.</param>
    /// <param name="sectionName">Configuration section the options are bound from, e.g. <c>DocumentsService</c>.</param>
    /// <param name="configureOptions">
    /// Optional extra options configuration (for example validation) that runs
    /// after binding. Consuming services may supply their own guards here.
    /// </param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddServiceHttpClient<TClient, TOptions>(
        this IServiceCollection services,
        IConfiguration config,
        string sectionName,
        Action<OptionsBuilder<TOptions>>? configureOptions = null)
        where TClient : class
        where TOptions : ServiceClientOptions
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(config);
        ArgumentException.ThrowIfNullOrWhiteSpace(sectionName);

        var builder = services.AddOptions<TOptions>()
            .Bind(config.GetSection(sectionName));
        configureOptions?.Invoke(builder);

        services.AddHttpClient<TClient>((sp, client) =>
        {
            var opts = sp.GetRequiredService<IOptions<TOptions>>().Value;
            if (string.IsNullOrWhiteSpace(opts.BaseUrl))
                throw new InvalidOperationException(
                    $"{sectionName}:BaseUrl is not configured.");
            client.BaseAddress = new Uri(opts.BaseUrl);
        });

        return services;
    }
}