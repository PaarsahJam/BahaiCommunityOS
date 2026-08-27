using CommunityOS.Finance.Domain.Repositories;
using CommunityOS.Finance.Infrastructure.Integration;
using CommunityOS.Finance.Infrastructure.Integration.Organization;
using CommunityOS.Finance.Infrastructure.Persistence;
using CommunityOS.Finance.Infrastructure.Repositories;
using CommunityOS.Authorization.HttpClient.Integration;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CommunityOS.Finance.Infrastructure;

public static class FinanceInfrastructureServiceExtensions
{
    public static IServiceCollection AddFinanceInfrastructure(
        this IServiceCollection services, IConfiguration config)
    {
        services.AddDbContext<FinanceDbContext>(opts =>
            opts.UseNpgsql(
                config.GetConnectionString("FinanceDb"),
                npgsql => npgsql.MigrationsAssembly(
                    typeof(FinanceInfrastructureServiceExtensions).Assembly.FullName)));

        // Persistence
        services.AddScoped<IFundRepository, FundRepository>();
        services.AddScoped<IFinancialTransactionRepository, FinancialTransactionRepository>();
        services.AddScoped<IFinanceOrganizationUnitReferenceRepository, FinanceOrganizationUnitReferenceRepository>();

        // Organization read-model (ADR-016)
        services.AddScoped<OrganizationIntegrationEventConsumer>();

        // Domain event -> integration event forwarding onto the message bus.
        // Registered as an open generic so MediatR 12.4.1 (which dispatches by
        // the runtime type of the notification) resolves the closed publisher
        // for each concrete domain event.
        services.AddScoped(typeof(INotificationHandler<>), typeof(FinanceIntegrationEventPublisher<>));

        // Authorization integration: the Finance service never reads the
        // Authorization database. Every decision is delegated to the
        // Authorization service's check endpoint over HTTP (ADR-018/019).
        services.AddAuthorizationHttpClient(config, "communityos-finance", includeGuard: true);

        return services;
    }
}