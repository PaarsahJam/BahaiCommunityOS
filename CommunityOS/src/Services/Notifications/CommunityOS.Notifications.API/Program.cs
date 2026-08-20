using CommunityOS.Notifications.API.Extensions;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console(formatProvider: System.Globalization.CultureInfo.InvariantCulture)
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.Host.UseSerilog((ctx, lc) =>
        lc.ReadFrom.Configuration(ctx.Configuration));

    builder.Services.AddNotificationsServices(builder.Configuration);

    var app = builder.Build();

    await app.ConfigurePipelineAsync();
    await app.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Notifications service terminated unexpectedly.");
}
finally
{
    await Log.CloseAndFlushAsync();
}