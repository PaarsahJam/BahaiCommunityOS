using System.Text;
using CommunityOS.Identity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CommunityOS.Identity.Tests;

public sealed class ModelDiagnostics
{
    [Fact]
    public void Dump_AuthorizationCode_Relationships()
    {
        var services = new ServiceCollection();
        services.AddDbContext<IdentityDbContext>(o =>
            o.UseNpgsql("Host=localhost;Database=communityos_identity;Username=x;Password=x"));
        using var sp = services.BuildServiceProvider();
        using var ctx = sp.GetRequiredService<IdentityDbContext>();

        var info = new StringBuilder();
        foreach (var et in ctx.Model.GetEntityTypes().OrderBy(e => e.Name))
        {
            info.AppendLine("ENTITY " + et.Name);
            foreach (var nav in et.GetNavigations())
                info.AppendLine("  NAV " + nav.Name + " -> " + nav.TargetEntityType.Name);
            foreach (var fk in et.GetForeignKeys())
                info.AppendLine("  FK " + fk.PrincipalEntityType.Name + " via [" +
                    string.Join(",", fk.Properties.Select(p => p.Name)) + "]");
        }

        System.IO.File.WriteAllText(
            @"C:\Users\Sorouri\AppData\Local\Temp\opencode\model_dump.txt",
            info.ToString());
        Assert.Fail("See model_dump.txt");
    }
}
