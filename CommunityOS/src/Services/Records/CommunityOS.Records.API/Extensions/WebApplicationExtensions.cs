using CommunityOS.Records.API.Middleware;
using CommunityOS.Records.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CommunityOS.Records.API.Extensions;

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
        var db = scope.ServiceProvider.GetRequiredService<RecordsDbContext>();
        await db.Database.MigrateAsync();

        // Seed the ratified baseline category catalog (ADR-023) after the
        // schema exists so a fresh database can create records immediately.
        await RecordsCatalogSeeder.SeedBaselineCategoriesAsync(db);
    }
}