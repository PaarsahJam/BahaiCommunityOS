using Serilog;
using CommunityOS.AI.API.Extensions;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console(formatProvider: System.Globalization.CultureInfo.InvariantCulture)
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.Host.UseSerilog((ctx, lc) =>
        lc.ReadFrom.Configuration(ctx.Configuration));

    builder.Services.AddAiServices(builder.Configuration);

    var app = builder.Build();

    app.ConfigurePipelineAsync();
    await app.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "AI Platform service terminated unexpectedly.");
}
finally
{
    await Log.CloseAndFlushAsync();
}
