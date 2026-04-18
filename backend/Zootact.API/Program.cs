using System.Runtime.Loader;
using FirebaseAdmin;
using Google.Apis.Auth.OAuth2;
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
var logsDirectory = Path.Combine(builder.Environment.ContentRootPath, "logs");
Directory.CreateDirectory(logsDirectory);

builder.Host.UseSerilog((context, services, configuration) =>
{
    configuration
        .ReadFrom.Configuration(context.Configuration)
        .MinimumLevel.Override("Microsoft.AspNetCore.Hosting.Diagnostics", LogEventLevel.Warning)
        .Enrich.FromLogContext()
        .WriteTo.Console()
        .WriteTo.File(
            Path.Combine(logsDirectory, "zootact-api-.log"),
            rollingInterval: RollingInterval.Day,
            retainedFileCountLimit: 14,
            shared: true);
});
builder.Logging.AddFilter("Microsoft.AspNetCore.Hosting.Diagnostics", LogLevel.Warning);

var frontendUrl = builder.Configuration["Frontend:Url"];
var postgresConnection = builder.Configuration.GetConnectionString("PostgreSQL");
var redisConnection = builder.Configuration.GetConnectionString("Redis");
var firebaseConfigPath = builder.Configuration["Firebase:ServiceAccountKeyPath"] ?? "Config/firebase-adminsdk.json";

if (string.IsNullOrWhiteSpace(frontendUrl))
    throw new InvalidOperationException("Missing required configuration: Frontend:Url");
if (string.IsNullOrWhiteSpace(postgresConnection))
    throw new InvalidOperationException("Missing required connection string: PostgreSQL");
if (string.IsNullOrWhiteSpace(redisConnection))
    throw new InvalidOperationException("Missing required connection string: Redis");
if (!File.Exists(firebaseConfigPath))
    throw new InvalidOperationException($"Firebase service account file not found: {firebaseConfigPath}");

FirebaseApp.Create(new AppOptions
{
    Credential = GoogleCredential.FromFile(firebaseConfigPath)
});

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddGameLogic();
builder.Services.AddScoped<IMatchNotificationService, SignalRMatchNotificationService>();
builder.Services.AddScoped<IPrivateLobbyNotificationService, SignalRPrivateLobbyNotificationService>();
builder.Services.AddScoped<IHealthStatusService, HealthStatusService>();

builder.Services.AddSignalR()
    .AddStackExchangeRedis(redisConnection, options =>
    {
        options.Configuration.ChannelPrefix = "Zootact";
    });

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy.WithOrigins(frontendUrl)
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});

var app = builder.Build();
RegisterLifecycleLogging(app);

await ApplyDatabaseSchemaPatchesAsync(app);

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

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

static async Task ApplyDatabaseSchemaPatchesAsync(WebApplication app)
{
    await using var scope = app.Services.CreateAsyncScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<ZootactDbContext>();

    await dbContext.Database.ExecuteSqlRawAsync("""
        DO $$
        DECLARE
            current_email_length integer;
            current_avatar_length integer;
        BEGIN
            SELECT character_maximum_length
            INTO current_email_length
            FROM information_schema.columns
            WHERE table_schema = 'public' AND table_name = 'users' AND column_name = 'email';

            SELECT character_maximum_length
            INTO current_avatar_length
            FROM information_schema.columns
            WHERE table_schema = 'public' AND table_name = 'users' AND column_name = 'avatar_url';

            IF COALESCE(current_email_length, 0) < 512 OR COALESCE(current_avatar_length, 0) < 2048 THEN
                DROP VIEW IF EXISTS v_leaderboard;

                ALTER TABLE users
                    ALTER COLUMN email TYPE VARCHAR(512),
                    ALTER COLUMN avatar_url TYPE VARCHAR(2048);

                CREATE OR REPLACE VIEW v_leaderboard AS
                SELECT
                    u.id,
                    u.username,
                    u.avatar_url,
                    u.forest_points,
                    s.total_games,
                    s.wins,
                    s.losses,
                    s.draws,
                    CASE
                        WHEN s.total_games > 0 THEN ROUND((s.wins::DECIMAL / s.total_games) * 100, 2)
                        ELSE 0
                    END AS win_rate,
                    s.win_streak_best,
                    s.win_streak_current
                FROM users u
                LEFT JOIN user_stats s ON u.id = s.user_id
                WHERE u.is_banned = FALSE
                ORDER BY u.forest_points DESC
                LIMIT 100;
            END IF;
        END $$;
        """);
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
