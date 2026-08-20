using CommunityOS.Workflow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace CommunityOS.Workflow.Infrastructure;

/// <summary>
/// Enables the EF Core tools to create and apply migrations without booting
/// the full web host (which requires a reachable database).
/// </summary>
public sealed class WorkflowDbContextFactory : IDesignTimeDbContextFactory<WorkflowDbContext>
{
    public WorkflowDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<WorkflowDbContext>()
            .UseNpgsql(
                "Host=localhost;Port=5432;Database=communityos_workflow;Username=communityos;Password=communityos",
                npgsql => npgsql.MigrationsAssembly("CommunityOS.Workflow.Infrastructure"))
            .Options;

        return new WorkflowDbContext(options);
    }
}