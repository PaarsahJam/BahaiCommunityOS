using CommunityOS.Authorization.Application.Authorization;
using CommunityOS.Records.Application.Validators;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace CommunityOS.Records.Application;

public static class RecordsApplicationServiceExtensions
{
    public static IServiceCollection AddRecordsApplication(this IServiceCollection services)
    {
        services.AddMediatR(cfg =>
            cfg.RegisterServicesFromAssembly(typeof(RecordsApplicationServiceExtensions).Assembly));

        services.AddValidatorsFromAssemblyContaining<CreateRecordCommandValidator>();

        services.AddTransient(typeof(IPipelineBehavior<,>),
            typeof(ValidationPipelineBehavior<,>));

        services.AddScoped<AuthorizationGuard>();

        return services;
    }
}