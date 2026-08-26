using CommunityOS.Authorization.HttpClient.Integration;
using CommunityOS.Correspondence.Application;
using CommunityOS.Correspondence.Infrastructure.Integration;
using CommunityOS.Correspondence.Infrastructure.Persistence;
using CommunityOS.Correspondence.Infrastructure.Retention;
using CommunityOS.EventBus;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CommunityOS.Correspondence.Infrastructure;

public static class CorrespondenceInfrastructureServiceExtensions
{
    public static IServiceCollection AddCorrespondenceInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        services.AddDbContext<CorrespondenceDbContext>(o => o.UseNpgsql(
            config.GetConnectionString("CorrespondenceDb"),
            x =>
            {
                x.MigrationsAssembly(typeof(CorrespondenceDbContext).Assembly.FullName);
                x.MigrationsHistoryTable("__ef_migrations_history", "correspondence");
            }));

        services.AddScoped<ILetterReader, LetterReader>();
        services.AddScoped<ILetterJournal, LetterJournal>();
        services.AddSingleton<IRetentionPolicy, CorrespondenceRetentionPolicy>();

        // Domain-event → integration-contract forwarding (transactional outbox).
        services.AddScoped<CorrespondenceIntegrationEventPublisher>();
        services.AddScoped<
            MediatR.INotificationHandler<Domain.Events.LetterSubmittedDomainEvent>,
            CorrespondenceIntegrationEventPublisher>();
        services.AddScoped<
            MediatR.INotificationHandler<Domain.Events.LetterDispatchedDomainEvent>,
            CorrespondenceIntegrationEventPublisher>();
        services.AddScoped<
            MediatR.INotificationHandler<Domain.Events.LetterDeliveryConfirmedDomainEvent>,
            CorrespondenceIntegrationEventPublisher>();
        services.AddScoped<
            MediatR.INotificationHandler<Domain.Events.LetterDeliveryFailedDomainEvent>,
            CorrespondenceIntegrationEventPublisher>();
        services.AddScoped<
            MediatR.INotificationHandler<Domain.Events.LetterCancelledDomainEvent>,
            CorrespondenceIntegrationEventPublisher>();

        services.Configure<CorrespondenceOptions>(config.GetSection(CorrespondenceOptions.SectionName));
        services.AddAuthorizationHttpClient(config, "communityos-correspondence");

        return services;
    }
}
