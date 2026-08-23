using CommunityOS.Authorization.Application.Authorization;
using CommunityOS.Correspondence.Application.Validators;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace CommunityOS.Correspondence.Application;

public static class CorrespondenceApplicationServiceExtensions
{
    public static IServiceCollection AddCorrespondenceApplication(this IServiceCollection services)
    {
        services.AddMediatR(cfg =>
            cfg.RegisterServicesFromAssembly(typeof(CorrespondenceApplicationServiceExtensions).Assembly));

        services.AddValidatorsFromAssemblyContaining<CreateLetterValidator>();

        services.AddTransient(typeof(IPipelineBehavior<,>),
            typeof(ValidationPipelineBehavior<,>));

        services.AddScoped<AuthorizationGuard>();

        return services;
    }
}
