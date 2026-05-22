using Nobia.Backend.ApplicationInsights.AspNetCore;
using Nobia.Backend.Logging;
using PriceConfiguratorBackoffice;
using PriceConfiguratorBackoffice.Infrastructure;
using Serilog;
using System.Reflection;

var contentRoot = Directory.GetCurrentDirectory();
var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    Args = args,
    ContentRootPath = contentRoot,
    WebRootPath = Path.Combine(contentRoot, "wwwroot"),
});

builder.Configuration
    .AddJsonFile("appsettings.local.json", optional: true, reloadOnChange: true)
    .AddEnvironmentVariables()
    .AddUserSecrets(Assembly.GetExecutingAssembly(), optional: true);

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

builder.Services.ConfigureServices(builder.Configuration);
builder.WebHost.UseStaticWebAssets();

builder.Host.UseSerilog((context, services, logger) =>
    logger
        .ConfigureLogging()
        .ConfigureApplicationInsights(services, builder.Configuration));

var app = builder.Build();

app.ConfigureApplication(app.Environment);

// Do not block Kestrel on Cosmos — Docker emulator can delay or hang EnsureCreated for minutes.
_ = CosmosDatabaseInitializer.EnsureCreatedAsync(app.Services, app.Environment, app.Logger)
    .ContinueWith(
        task =>
        {
            if (task.IsFaulted && task.Exception is not null)
            {
                app.Logger.LogError(task.Exception.GetBaseException(), "Cosmos EnsureCreated failed on startup.");
            }
        },
        CancellationToken.None,
        TaskContinuationOptions.None,
        TaskScheduler.Default);

app.Run();
