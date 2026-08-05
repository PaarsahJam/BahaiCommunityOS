using CommunityOS.Community.API.Extensions;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.Host.UseSerilog((ctx, lc) =>
        lc.ReadFrom.Configuration(ctx.Configuration));

    builder.Services.AddCommunityServices(builder.Configuration);

    var app = builder.Build();

    await app.ConfigurePipelineAsync();
    await app.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Community service terminated unexpectedly.");
}
finally
{
    await Log.CloseAndFlushAsync();
}
