using CommunityOS.Authorization.Application.Authorization;
using CommunityOS.Authorization.Application.Interfaces;
using CommunityOS.Authorization.Domain.Repositories;
using CommunityOS.Authorization.Infrastructure.Integration;
using CommunityOS.Authorization.Infrastructure.Integration.Organization;
using CommunityOS.Authorization.Infrastructure.Persistence;
using CommunityOS.Authorization.Infrastructure.Repositories;
using CommunityOS.SharedKernel.Domain.Events;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CommunityOS.Authorization.Infrastructure;

public static class AuthorizationInfrastructureServiceExtensions
{
    public static IServiceCollection AddAuthorizationInfrastructure(
        this IServiceCollection services, IConfiguration config)
    {
        services.AddDbContext<AuthorizationDbContext>(opts =>
            opts.UseNpgsql(
                config.GetConnectionString("AuthorizationDb"),
                npgsql => npgsql.MigrationsAssembly(
                    typeof(AuthorizationInfrastructureServiceExtensions).Assembly.FullName)));

        services.Configure<AuthorizationOptions>(
            config.GetSection(AuthorizationOptions.SectionName));

        // Repositories
        services.AddScoped<IRoleRepository, RoleRepository>();
        services.AddScoped<IRoleAssignmentRepository, RoleAssignmentRepository>();
        services.AddScoped<IAuthorizationRelationshipRepository, AuthorizationRelationshipRepository>();
        services.AddScoped<IDelegationRepository, DelegationRepository>();
        services.AddScoped<IBreakGlassRequestRepository, BreakGlassRequestRepository>();

        // Domain event -> integration event forwarding onto the message bus
        services.AddScoped<INotificationHandler<IDomainEvent>, AuthorizationIntegrationEventPublisher>();

        // Organization context integration: the Authorization service never reads
        // the Organization database. Organization-scoped grants are resolved by
        // calling the Organization service's covers endpoint over HTTP as a
        // service principal (ADR-018). This replaces the application-layer
        // default exact-match provider. Fail-closed on any error.
        services.ConfigureOrganizationService(config);
        services.AddHttpClient<HttpOrganizationContextProvider>((sp, client) =>
        {
            var opts = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<OrganizationServiceOptions>>().Value;
            client.BaseAddress = new Uri(opts.BaseUrl);
        });
        services.AddScoped<IOrganizationContextProvider>(sp => sp.GetRequiredService<HttpOrganizationContextProvider>());

        return services;
    }
}
