using CommunityOS.Workflow.API.Middleware;
using CommunityOS.Workflow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CommunityOS.Workflow.API.Extensions;

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
        var db = scope.ServiceProvider.GetRequiredService<WorkflowDbContext>();
        await db.Database.MigrateAsync();

        // Seed the ratified baseline task-definition catalog (ADR-024) after
        // the schema exists so a fresh database can create tasks immediately.
        await WorkflowCatalogSeeder.SeedBaselineDefinitionsAsync(db);
    }
}