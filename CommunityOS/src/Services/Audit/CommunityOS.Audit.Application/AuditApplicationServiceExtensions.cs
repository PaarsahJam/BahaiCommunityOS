using CommunityOS.Authorization.Application.Authorization;
using CommunityOS.Audit.Application.Validators;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace CommunityOS.Audit.Application;

public static class AuditApplicationServiceExtensions
{
    public static IServiceCollection AddAuditApplication(this IServiceCollection services)
    {
        services.AddMediatR(cfg =>
            cfg.RegisterServicesFromAssembly(typeof(AuditApplicationServiceExtensions).Assembly));

        services.AddValidatorsFromAssemblyContaining<AuditQueryValidator>();

        services.AddTransient(typeof(IPipelineBehavior<,>),
            typeof(ValidationPipelineBehavior<,>));

        services.AddScoped<AuthorizationGuard>();

        return services;
    }
}
