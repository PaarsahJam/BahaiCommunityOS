using CommunityOS.Identity.Application.Authentication;
using CommunityOS.Identity.Application.Validators;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace CommunityOS.Identity.Application;

public static class IdentityApplicationServiceExtensions
{
    public static IServiceCollection AddIdentityApplication(this IServiceCollection services)
    {
        services.AddMediatR(cfg =>
            cfg.RegisterServicesFromAssembly(typeof(IdentityApplicationServiceExtensions).Assembly));

        services.AddValidatorsFromAssemblyContaining<RegisterCommandValidator>();

        services.AddTransient(typeof(IPipelineBehavior<,>),
            typeof(ValidationPipelineBehavior<,>));

        services.AddScoped<IAccountAuthenticator, AccountAuthenticator>();

        return services;
    }
}
