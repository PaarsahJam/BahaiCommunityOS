using MassTransit;
using Microsoft.EntityFrameworkCore;
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
        this IServiceCollection services,
        IConfiguration config,
        Action<IRegistrationConfigurator>? configureConsumers = null) =>
        RegisterBus(services, config, configureConsumers, outboxDbContextType: null, inboxOnly: false);

    /// <summary>
    /// Registers the bus together with the MassTransit transactional outbox
    /// (ADR-015, amended at Prompt 08A-R2). Records is the earliest service with
    /// a guaranteed-delivery consumer, so the outbox is enabled at its
    /// integration gate and is never deferred to a later service.
    /// <para>
    /// The outbox commits integration events atomically with the business write:
    /// Records command handlers publish domain events <b>before</b>
    /// SaveChanges, the bus-outbox captures the forwarded integration events
    /// into the DbContext, and the single transaction commits both the record
    /// change and the outbox rows. The bus outbox delivery service then
    /// publishes them to RabbitMQ. Consumers additionally use the receive-endpoint
    /// outbox (inbox/outbox) for exactly-once consumption.
    /// </para>
    /// <para>
    /// <typeparamref name="TDbContext"/> must model the outbox entities
    /// (<c>AddInboxStateEntity</c>/<c>AddOutboxMessageEntity</c>/
    /// <c>AddOutboxStateEntity</c>) and use the same connection as the domain
    /// data so the commit is truly atomic.
    /// </para>
    /// </summary>
    public static IServiceCollection AddCommunityOSEventBusWithOutbox<TDbContext>(
        this IServiceCollection services,
        IConfiguration config,
        Action<IRegistrationConfigurator>? configureConsumers = null)
        where TDbContext : DbContext =>
        RegisterBus(services, config, configureConsumers, typeof(TDbContext), inboxOnly: false);

    public static IServiceCollection AddCommunityOSEventBusWithInbox<TDbContext>(
        this IServiceCollection services,
        IConfiguration config,
        Action<IRegistrationConfigurator>? configureConsumers = null)
        where TDbContext : DbContext
    {
        return RegisterBus(services, config, configureConsumers, typeof(TDbContext), inboxOnly: true);
    }

    private static IServiceCollection RegisterBus(
        IServiceCollection services,
        IConfiguration config,
        Action<IRegistrationConfigurator>? configureConsumers,
        Type? outboxDbContextType,
        bool inboxOnly)
    {
        var host = config["RabbitMq:Host"] ?? "localhost";
        var port = ushort.TryParse(config["RabbitMq:Port"], out var parsed) ? parsed : (ushort)5672;
        var username = config["RabbitMq:Username"] ?? "guest";
        var password = config["RabbitMq:Password"] ?? "guest";

        services.AddMassTransit(bus =>
        {
            configureConsumers?.Invoke(bus);

            if (inboxOnly && outboxDbContextType is not null)
                ConfigureInbox(bus, outboxDbContextType);
            else if (outboxDbContextType is not null)
                ConfigureOutbox(bus, outboxDbContextType);

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

    private static void ConfigureInbox(IBusRegistrationConfigurator bus, Type dbContextType)
    {
        var configure = typeof(EventBusServiceExtensions)
            .GetMethod(nameof(ConfigureInboxCore), System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!
            .MakeGenericMethod(dbContextType);
        configure.Invoke(null, [bus]);
    }

    private static void ConfigureInboxCore<TDbContext>(IBusRegistrationConfigurator bus)
        where TDbContext : DbContext
    {
        bus.AddEntityFrameworkOutbox<TDbContext>(o => o.UsePostgres());
        bus.AddConfigureEndpointsCallback((ctx, name, cfg) =>
            cfg.UseEntityFrameworkOutbox<TDbContext>(ctx));
    }

    private static void ConfigureOutbox(IBusRegistrationConfigurator bus, Type dbContextType)
    {
        var configure = typeof(EventBusServiceExtensions)
            .GetMethod(nameof(ConfigureOutboxCore), System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!
            .MakeGenericMethod(dbContextType);
        configure.Invoke(null, [bus]);
    }

    private static void ConfigureOutboxCore<TDbContext>(IBusRegistrationConfigurator bus)
        where TDbContext : DbContext
    {
        // Transactional outbox (bus outbox): integration events are stored in
        // the DbContext and delivered after the business transaction commits
        // (ADR-015). UsePostgres selects the PostgreSQL row-lock provider.
        bus.AddEntityFrameworkOutbox<TDbContext>(o =>
        {
            o.UsePostgres();
            o.UseBusOutbox();
        });

        // Receive-endpoint outbox: consumers treat every message as
        // exactly-once (inbox) and publish their own events through the same
        // outbox.
        bus.AddConfigureEndpointsCallback((ctx, name, cfg) =>
            cfg.UseEntityFrameworkOutbox<TDbContext>(ctx));
    }
}
