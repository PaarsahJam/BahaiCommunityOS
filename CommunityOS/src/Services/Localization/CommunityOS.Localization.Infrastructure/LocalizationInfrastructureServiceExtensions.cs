using CommunityOS.Authorization.Application.Interfaces;
using CommunityOS.Localization.Application;
using CommunityOS.Localization.Domain.Events;
using CommunityOS.Localization.Infrastructure.Integration;
using CommunityOS.Localization.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace CommunityOS.Localization.Infrastructure;

public static class LocalizationInfrastructureServiceExtensions
{
    public static IServiceCollection AddLocalizationInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        services.AddDbContext<LocalizationDbContext>(o => o.UseNpgsql(
            config.GetConnectionString("LocalizationDb"),
            x =>
            {
                x.MigrationsAssembly(typeof(LocalizationDbContext).Assembly.FullName);
                x.MigrationsHistoryTable("__ef_migrations_history", "localization");
            }));

        services.AddScoped<ILocalizationReader, LocalizationReader>();
        services.AddScoped<ILocalizationJournal, LocalizationJournal>();

        // Domain-event → integration-contract forwarding (transactional outbox).
        services.AddScoped<LocalizationIntegrationEventPublisher>();
        services.AddScoped<
            MediatR.INotificationHandler<CatalogChangedDomainEvent>,
            LocalizationIntegrationEventPublisher>();

        services.Configure<LocalizationOptions>(config.GetSection(LocalizationOptions.SectionName));
        services.Configure<LocalizationAuthorizationOptions>(config.GetSection("AuthorizationService"));
        services.AddHttpClient<HttpAuthorizationEvaluator>((sp, client) =>
            client.BaseAddress = new Uri(sp.GetRequiredService<IOptions<LocalizationAuthorizationOptions>>().Value.BaseUrl));
        services.AddScoped<IAuthorizationEvaluator>(sp => sp.GetRequiredService<HttpAuthorizationEvaluator>());

        // Machine-translation seam ships disabled (ADR-029 decision 19).
        services.AddSingleton<IMachineTranslationSuggestionSource,
            DisabledMachineTranslationSuggestionSource>();

        return services;
    }
}
