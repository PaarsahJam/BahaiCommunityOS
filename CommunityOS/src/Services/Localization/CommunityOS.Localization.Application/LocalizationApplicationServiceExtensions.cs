using CommunityOS.Authorization.Application.Authorization;
using CommunityOS.Localization.Application.Validators;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace CommunityOS.Localization.Application;

public static class LocalizationApplicationServiceExtensions
{
    public static IServiceCollection AddLocalizationApplication(this IServiceCollection services)
    {
        services.AddMediatR(cfg =>
            cfg.RegisterServicesFromAssembly(typeof(LocalizationApplicationServiceExtensions).Assembly));

        services.AddValidatorsFromAssemblyContaining<CreateResourceEntryValidator>();

        services.AddTransient(typeof(IPipelineBehavior<,>),
            typeof(ValidationPipelineBehavior<,>));

        services.AddScoped<AuthorizationGuard>();

        return services;
    }
}
