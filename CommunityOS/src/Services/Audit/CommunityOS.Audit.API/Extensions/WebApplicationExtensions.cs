using CommunityOS.Audit.API.Middleware;
using CommunityOS.Audit.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CommunityOS.Audit.API.Extensions;

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
        var db = scope.ServiceProvider.GetRequiredService<AuditDbContext>();
        await db.Database.MigrateAsync();
    }
}
