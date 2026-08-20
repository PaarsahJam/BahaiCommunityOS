using CommunityOS.Authorization.Application.Authorization;
using CommunityOS.Notifications.Application.Services;
using CommunityOS.Notifications.Application.Validators;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace CommunityOS.Notifications.Application;

public static class NotificationsApplicationServiceExtensions
{
    public static IServiceCollection AddNotificationsApplication(this IServiceCollection services)
    {
        services.AddMediatR(cfg =>
            cfg.RegisterServicesFromAssembly(typeof(NotificationsApplicationServiceExtensions).Assembly));

        services.AddValidatorsFromAssemblyContaining<CreateNotificationCommandValidator>();

        services.AddTransient(typeof(IPipelineBehavior<,>),
            typeof(ValidationPipelineBehavior<,>));

        services.AddScoped<AuthorizationGuard>();

        services.AddScoped<NotificationDispatchService>();

        return services;
    }
}