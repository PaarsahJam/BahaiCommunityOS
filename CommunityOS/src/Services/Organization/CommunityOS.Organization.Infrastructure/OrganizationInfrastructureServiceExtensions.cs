using CommunityOS.Authorization.Application.Authorization;
using CommunityOS.Authorization.Application.Interfaces;
using CommunityOS.Organization.Domain.Repositories;
using CommunityOS.Organization.Infrastructure.Integration;
using CommunityOS.Organization.Infrastructure.Integration.Authorization;
using CommunityOS.Organization.Infrastructure.Persistence;
using CommunityOS.Organization.Infrastructure.Repositories;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CommunityOS.Organization.Infrastructure;

public static class OrganizationInfrastructureServiceExtensions
{
    public static IServiceCollection AddOrganizationInfrastructure(
        this IServiceCollection services, IConfiguration config)
    {
        services.AddDbContext<OrganizationDbContext>(opts =>
            opts.UseNpgsql(
                config.GetConnectionString("OrganizationDb"),
                npgsql => npgsql.MigrationsAssembly(
                    typeof(OrganizationInfrastructureServiceExtensions).Assembly.FullName)));

        // Repositories
        services.AddScoped<IOrganizationRepository, OrganizationRepository>();
        services.AddScoped<IOrganizationUnitRepository, OrganizationUnitRepository>();
        services.AddScoped<IAppointmentRepository, AppointmentRepository>();
        services.AddScoped<ICommitteeRepository, CommitteeRepository>();
        services.AddScoped<IInstitutionRepository, InstitutionRepository>();
        services.AddScoped<IDelegationFactRepository, DelegationFactRepository>();

        // Domain event -> integration event forwarding onto the message bus.
        // Registered as an open generic so MediatR 12.4.1 (which dispatches by
        // the runtime type of the notification) resolves the closed publisher for
        // each concrete domain event.
        services.AddScoped(typeof(INotificationHandler<>), typeof(OrganizationIntegrationEventPublisher<>));

        // Authorization integration: the Organization service never reads the
        // Authorization database. The guard is bound to an HTTP evaluator that
        // calls the Authorization service's check API (ADR-018).
        services.ConfigureAuthorizationService(config);
        services.AddHttpClient<HttpAuthorizationEvaluator>((sp, client) =>
        {
            var opts = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<AuthorizationServiceOptions>>().Value;
            client.BaseAddress = new Uri(opts.BaseUrl);
        });
        services.AddScoped<IAuthorizationEvaluator>(sp => sp.GetRequiredService<HttpAuthorizationEvaluator>());
        services.AddScoped<IOrganizationContextProvider, DefaultOrganizationContextProvider>();
        services.AddScoped<AuthorizationGuard>();

        return services;
    }
}