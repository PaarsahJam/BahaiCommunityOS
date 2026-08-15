using CommunityOS.Documents.API.Extensions;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console(formatProvider: System.Globalization.CultureInfo.InvariantCulture)
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.Host.UseSerilog((ctx, lc) =>
        lc.ReadFrom.Configuration(ctx.Configuration));

    builder.Services.AddDocumentsServices(builder.Configuration);

    var app = builder.Build();

    await app.ConfigurePipelineAsync();
    await app.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Documents service terminated unexpectedly.");
}
finally
{
    await Log.CloseAndFlushAsync();
}