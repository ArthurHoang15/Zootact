using Microsoft.EntityFrameworkCore;
using StackExchange.Redis;
using Zootact.Infrastructure.Data;

namespace Zootact.API.Services;

public interface IHealthStatusService
{
    Task<HealthStatusResponse> GetLiveAsync();
    Task<HealthStatusResponse> GetReadyAsync(CancellationToken cancellationToken = default);
    Task<HealthStatusResponse> GetDependenciesAsync(CancellationToken cancellationToken = default);
}

public sealed record DependencyStatusResponse(
    string Name,
    bool Healthy,
    string Message);

public sealed record HealthStatusResponse(
    string Status,
    DateTimeOffset Timestamp,
    string Service,
    int ProcessId,
    IReadOnlyList<DependencyStatusResponse> Dependencies);

public sealed class HealthStatusService(
    ZootactDbContext dbContext,
    IConnectionMultiplexer redis,
    IHttpClientFactory httpClientFactory,
    IConfiguration configuration,
    ILogger<HealthStatusService> logger) : IHealthStatusService
{
    public Task<HealthStatusResponse> GetLiveAsync()
    {
        return Task.FromResult(new HealthStatusResponse(
            "healthy",
            DateTimeOffset.UtcNow,
            "zootact-api",
            Environment.ProcessId,
            []));
    }

    public async Task<HealthStatusResponse> GetReadyAsync(CancellationToken cancellationToken = default)
    {
        var database = await CheckDatabaseAsync(cancellationToken);
        var redisDependency = await CheckRedisAsync(cancellationToken);
        var dependencies = new[] { database, redisDependency };

        return new HealthStatusResponse(
            dependencies.All(dependency => dependency.Healthy) ? "healthy" : "unhealthy",
            DateTimeOffset.UtcNow,
            "zootact-api",
            Environment.ProcessId,
            dependencies);
    }

    public async Task<HealthStatusResponse> GetDependenciesAsync(CancellationToken cancellationToken = default)
    {
        var database = await CheckDatabaseAsync(cancellationToken);
        var redisDependency = await CheckRedisAsync(cancellationToken);
        var aiDependency = await CheckAiServiceAsync(cancellationToken);
        var coreHealthy = database.Healthy && redisDependency.Healthy;

        return new HealthStatusResponse(
            !coreHealthy ? "unhealthy" : aiDependency.Healthy ? "healthy" : "degraded",
            DateTimeOffset.UtcNow,
            "zootact-api",
            Environment.ProcessId,
            [database, redisDependency, aiDependency]);
    }

    private async Task<DependencyStatusResponse> CheckDatabaseAsync(CancellationToken cancellationToken)
    {
        try
        {
            var canConnect = await dbContext.Database.CanConnectAsync(cancellationToken);
            if (!canConnect)
            {
                logger.LogWarning("Database readiness check failed: CanConnectAsync returned false.");
                return new DependencyStatusResponse("postgres", false, "Database connection failed");
            }

            return new DependencyStatusResponse("postgres", true, "Database connection healthy");
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Database readiness check threw an exception.");
            return new DependencyStatusResponse("postgres", false, ex.Message);
        }
    }

    private async Task<DependencyStatusResponse> CheckRedisAsync(CancellationToken cancellationToken)
    {
        try
        {
            var latency = await redis.GetDatabase().PingAsync();
            cancellationToken.ThrowIfCancellationRequested();
            return new DependencyStatusResponse("redis", true, $"Redis ping {Math.Round(latency.TotalMilliseconds, 1)} ms");
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Redis readiness check threw an exception.");
            return new DependencyStatusResponse("redis", false, ex.Message);
        }
    }

    private async Task<DependencyStatusResponse> CheckAiServiceAsync(CancellationToken cancellationToken)
    {
        var baseUrl = configuration["AiService:BaseUrl"] ?? "http://localhost:8001";
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(2));

        try
        {
            var httpClient = httpClientFactory.CreateClient();
            using var response = await httpClient.GetAsync($"{baseUrl.TrimEnd('/')}/health", timeoutCts.Token);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogInformation("AI dependency health check returned status {StatusCode}.", response.StatusCode);
                return new DependencyStatusResponse("ai-service", false, $"AI service returned {(int)response.StatusCode}");
            }

            return new DependencyStatusResponse("ai-service", true, "AI service reachable");
        }
        catch (Exception ex)
        {
            logger.LogInformation(ex, "AI dependency health check failed.");
            return new DependencyStatusResponse("ai-service", false, ex.Message);
        }
    }
}
