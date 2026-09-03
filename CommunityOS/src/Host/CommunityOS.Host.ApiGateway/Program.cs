using CommunityOS.Host.ApiGateway.Extensions;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console(formatProvider: System.Globalization.CultureInfo.InvariantCulture)
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.Host.UseSerilog((ctx, lc) =>
        lc.ReadFrom.Configuration(ctx.Configuration));

    builder.Services.AddGatewayServices(builder.Configuration);

    var app = builder.Build();

    app.MapGatewayPipeline();
    await app.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "CommunityOS API Gateway terminated unexpectedly.");
}
finally
{
    await Log.CloseAndFlushAsync();
}

public partial class Program;
