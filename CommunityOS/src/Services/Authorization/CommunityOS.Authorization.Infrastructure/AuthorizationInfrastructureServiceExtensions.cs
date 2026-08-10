using CommunityOS.Authorization.Application.Authorization;
using CommunityOS.Authorization.Domain.Repositories;
using CommunityOS.Authorization.Infrastructure.Integration;
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

        return services;
    }
}
