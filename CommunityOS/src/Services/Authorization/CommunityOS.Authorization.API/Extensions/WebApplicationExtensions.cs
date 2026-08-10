using CommunityOS.Authorization.API.Middleware;
using CommunityOS.Authorization.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CommunityOS.Authorization.API.Extensions;

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
        app.MapHealthChecks("/health");

        return app;
    }

    private static async Task MigrateDbAsync(this WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AuthorizationDbContext>();
        await db.Database.MigrateAsync();

        var config = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        await AuthorizationSeeder.SeedDevelopmentRolesAsync(db, config);
    }
}
