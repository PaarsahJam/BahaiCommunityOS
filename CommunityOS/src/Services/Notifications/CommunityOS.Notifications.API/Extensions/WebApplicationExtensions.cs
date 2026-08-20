using CommunityOS.Notifications.API.Middleware;
using CommunityOS.Notifications.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CommunityOS.Notifications.API.Extensions;

internal static class WebApplicationExtensions
{
    internal static async Task<WebApplication> ConfigurePipelineAsync(this WebApplication app)
    {
        app.UseMiddleware<ExceptionHandlingMiddleware>();

        if (app.Environment.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI();
            await app.MigrateDbAsync();
        }

        app.UseHttpsRedirection();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapControllers();

        return app;
    }

    private static async Task MigrateDbAsync(this WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<NotificationsDbContext>();
        await db.Database.MigrateAsync();

        // Seed the ratified baseline notification-type catalog (ADR-025) after
        // the schema exists so a fresh database can dispatch notifications
        // immediately.
        await NotificationsCatalogSeeder.SeedBaselineTypesAsync(db);
    }
}