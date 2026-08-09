using MassTransit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CommunityOS.EventBus;

/// <summary>
/// Registers the MassTransit message bus backed by RabbitMQ. Integration
/// events published by services flow through the bus to their consumers.
/// </summary>
public static class EventBusServiceExtensions
{
    public static IServiceCollection AddCommunityOSEventBus(
        this IServiceCollection services, IConfiguration config)
    {
        var host = config["RabbitMq:Host"] ?? "localhost";
        var port = ushort.TryParse(config["RabbitMq:Port"], out var parsed) ? parsed : (ushort)5672;
        var username = config["RabbitMq:Username"] ?? "guest";
        var password = config["RabbitMq:Password"] ?? "guest";

        services.AddMassTransit(bus =>
        {
            bus.SetKebabCaseEndpointNameFormatter();

            bus.UsingRabbitMq((ctx, cfg) =>
            {
                cfg.Host(host, hostConfig =>
                {
                    hostConfig.Username(username);
                    hostConfig.Password(password);
                });

                cfg.ConfigureEndpoints(ctx);
            });
        });

        services.AddOptions<MassTransitHostOptions>()
            .Configure(options => options.WaitUntilStarted = true);

        return services;
    }
}
