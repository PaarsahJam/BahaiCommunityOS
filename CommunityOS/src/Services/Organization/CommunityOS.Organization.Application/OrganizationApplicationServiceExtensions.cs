using System.Reflection;
using CommunityOS.Organization.Application.Pipeline;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace CommunityOS.Organization.Application;

public static class OrganizationApplicationServiceExtensions
{
    private static readonly Assembly ApplicationAssembly = typeof(OrganizationApplicationServiceExtensions).Assembly;

    public static IServiceCollection AddOrganizationApplication(this IServiceCollection services)
    {
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(ApplicationAssembly));

        services.AddValidatorsFromAssembly(ApplicationAssembly);
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationPipelineBehavior<,>));

        return services;
    }
}