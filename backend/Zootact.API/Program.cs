using System.Runtime.Loader;
using FirebaseAdmin;
using Google.Apis.Auth.OAuth2;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Events;
using Zootact.API.Hubs;
using Zootact.API.Middleware;
using Zootact.API.Services;
using Zootact.Core.Interfaces;
using Zootact.Infrastructure.Data;
using Zootact.Infrastructure;

var builder = WebApplication.CreateBuilder(args);
ConfigureUrls(builder);

var enableFileLogging = builder.Environment.IsDevelopment() || builder.Configuration.GetValue<bool>("Logging:EnableFileSink");
var logsDirectory = Path.Combine(builder.Environment.ContentRootPath, "logs");
if (enableFileLogging)
{
    Directory.CreateDirectory(logsDirectory);
}

builder.Host.UseSerilog((context, services, configuration) =>
{
    configuration
        .ReadFrom.Configuration(context.Configuration)
        .MinimumLevel.Override("Microsoft.AspNetCore.Hosting.Diagnostics", LogEventLevel.Warning)
        .Enrich.FromLogContext()
        .WriteTo.Console();

    if (enableFileLogging)
    {
        configuration.WriteTo.File(
            Path.Combine(logsDirectory, "zootact-api-.log"),
            rollingInterval: RollingInterval.Day,
            retainedFileCountLimit: 14,
            shared: true);
    }
});
builder.Logging.AddFilter("Microsoft.AspNetCore.Hosting.Diagnostics", LogLevel.Warning);

var frontendOrigins = FrontendOriginResolver.Resolve(builder.Configuration);
var postgresConnection = builder.Configuration.GetConnectionString("PostgreSQL");
var redisConnection = builder.Configuration.GetConnectionString("Redis");
var firebaseAdminCredentials = FirebaseAdminCredentialLoader.Load(builder.Configuration, builder.Environment.ContentRootPath);

if (frontendOrigins.Length == 0)
    throw new InvalidOperationException("Missing required configuration: Frontend:AllowedOrigins or Frontend:Url");
if (string.IsNullOrWhiteSpace(postgresConnection))
    throw new InvalidOperationException("Missing required connection string: PostgreSQL");
if (string.IsNullOrWhiteSpace(redisConnection))
    throw new InvalidOperationException("Missing required connection string: Redis");

FirebaseApp.Create(new AppOptions
{
    Credential = GoogleCredential.FromJson(firebaseAdminCredentials.Json)
});

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});

builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddGameLogic();
builder.Services.AddScoped<IMatchNotificationService, SignalRMatchNotificationService>();
builder.Services.AddScoped<IPrivateLobbyNotificationService, SignalRPrivateLobbyNotificationService>();
builder.Services.AddScoped<IHealthStatusService, HealthStatusService>();

builder.Services.AddSignalR()
    .AddStackExchangeRedis(redisConnection, options =>
    {
        options.Configuration.ChannelPrefix = StackExchange.Redis.RedisChannel.Literal("Zootact");
    });

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy.WithOrigins(frontendOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});

var app = builder.Build();
RegisterLifecycleLogging(app);

await ApplyDatabaseMigrationsAsync(app);

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseForwardedHeaders();
app.UseHttpsRedirection();
app.UseCors("AllowFrontend");
app.UseSerilogRequestLogging(options =>
{
    options.MessageTemplate = "HTTP {RequestMethod} {SanitizedPath} responded {StatusCode} in {Elapsed:0.0000} ms";
    options.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
    {
        diagnosticContext.Set("SanitizedPath", RequestLoggingSanitizer.SanitizeRequestPath(httpContext.Request));
    };
});
app.UseFirebaseAuth();
app.UseAuthorization();

app.MapControllers();
app.MapHub<GameHub>("/game-hub");

app.Run();

static void ConfigureUrls(WebApplicationBuilder builder)
{
    var explicitUrls = builder.Configuration["ASPNETCORE_URLS"] ?? Environment.GetEnvironmentVariable("ASPNETCORE_URLS");
    if (!string.IsNullOrWhiteSpace(explicitUrls))
    {
        return;
    }

    var port = builder.Configuration["PORT"] ?? Environment.GetEnvironmentVariable("PORT");
    if (int.TryParse(port, out var parsedPort) && parsedPort > 0)
    {
        builder.WebHost.UseUrls($"http://0.0.0.0:{parsedPort}");
    }
}

static async Task ApplyDatabaseMigrationsAsync(WebApplication app)
{
    await using var scope = app.Services.CreateAsyncScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<ZootactDbContext>();
    var migrationIds = dbContext.Database.GetMigrations().ToArray();

    await EfMigrationBootstrapper.EnsureLegacyMigrationBaselineAsync(dbContext.Database, migrationIds);
    await dbContext.Database.MigrateAsync();
}

static void RegisterLifecycleLogging(WebApplication app)
{
    var logger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("HostLifecycle");

    AppDomain.CurrentDomain.ProcessExit += (_, _) =>
    {
        logger.LogInformation("ProcessExit fired for PID {ProcessId}.", Environment.ProcessId);
    };

    AppDomain.CurrentDomain.UnhandledException += (_, eventArgs) =>
    {
        logger.LogCritical(
            eventArgs.ExceptionObject as Exception,
            "Unhandled exception triggered process termination. IsTerminating={IsTerminating}",
            eventArgs.IsTerminating);
    };

    TaskScheduler.UnobservedTaskException += (_, eventArgs) =>
    {
        logger.LogError(eventArgs.Exception, "Unobserved task exception captured by host lifecycle hooks.");
    };

    AssemblyLoadContext.Default.Unloading += _ =>
    {
        logger.LogInformation("AssemblyLoadContext unloading signaled for PID {ProcessId}.", Environment.ProcessId);
    };

    app.Lifetime.ApplicationStarted.Register(() =>
    {
        logger.LogInformation(
            "ApplicationStarted. PID={ProcessId}, Environment={Environment}, ContentRoot={ContentRoot}, Urls={Urls}",
            Environment.ProcessId,
            app.Environment.EnvironmentName,
            app.Environment.ContentRootPath,
            string.Join(", ", app.Urls));
    });

    app.Lifetime.ApplicationStopping.Register(() =>
    {
        logger.LogWarning("ApplicationStopping fired for PID {ProcessId}.", Environment.ProcessId);
    });

    app.Lifetime.ApplicationStopped.Register(() =>
    {
        logger.LogWarning("ApplicationStopped fired for PID {ProcessId}.", Environment.ProcessId);
    });
}
