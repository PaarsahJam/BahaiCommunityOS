using System.Reflection;
using CommunityOS.Authorization.Application.Authorization;
using CommunityOS.Authorization.Application.Interfaces;
using CommunityOS.Authorization.Application.Pipeline;
using CommunityOS.Authorization.Application.Validators;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace CommunityOS.Authorization.Application;

public static class AuthorizationApplicationServiceExtensions
{
    private static readonly Assembly ApplicationAssembly = typeof(AuthorizationApplicationServiceExtensions).Assembly;

    public static IServiceCollection AddAuthorizationApplication(this IServiceCollection services)
    {
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(ApplicationAssembly));

        services.AddValidatorsFromAssembly(ApplicationAssembly);
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationPipelineBehavior<,>));

        services.AddScoped<IOrganizationContextProvider, DefaultOrganizationContextProvider>();
        services.AddScoped<IAuthorizationEvaluator, AuthorizationEvaluator>();
        services.AddScoped<AuthorizationGuard>();

        return services;
    }
}
