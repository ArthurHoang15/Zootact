using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using Zootact.Core.Domain;
using Zootact.Core.Interfaces;

namespace Zootact.Infrastructure.Services;

/// <summary>
/// Background service that monitors disconnected players and auto-forfeits after timeout.
/// Scans Redis for disconnect keys every 10 seconds.
/// </summary>
public sealed class DisconnectTimerService(
    IServiceProvider serviceProvider,
    ILogger<DisconnectTimerService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Disconnect Timer Service started");
        
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CheckDisconnectedPlayersAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error in Disconnect Timer Service");
            }
            
            // Run every 10 seconds
            await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
        }
        
        logger.LogInformation("Disconnect Timer Service stopped");
    }
    
    private async Task CheckDisconnectedPlayersAsync(CancellationToken cancellationToken)
    {
        using var scope = serviceProvider.CreateScope();
        var redis = scope.ServiceProvider.GetRequiredService<IConnectionMultiplexer>();
        var gameStateRepo = scope.ServiceProvider.GetRequiredService<IGameStateRepository>();
        var lifecycleService = scope.ServiceProvider.GetRequiredService<IMatchLifecycleService>();
        var notificationService = scope.ServiceProvider.GetRequiredService<IMatchNotificationService>();
        var db = redis.GetDatabase();
        
        // Scan for disconnect keys: game:{matchId}:disconnect:{userId}
        var server = redis.GetServer(redis.GetEndPoints().First());
        var keys = server.Keys(pattern: "game:*:disconnect:*", pageSize: 100);
        
        foreach (var key in keys)
        {
            if (cancellationToken.IsCancellationRequested)
                break;
            
            try
            {
                var rawValue = await db.StringGetAsync(key);
                if (rawValue.IsNullOrEmpty)
                    continue;

                if (!TryParseDisconnectKey(key.ToString(), out var matchId, out var userId))
                    continue;

                var disconnectInfo = gameStateRepo is IGameStateRepository repository
                    ? await repository.GetPlayerDisconnectInfoAsync(matchId, userId)
                    : null;

                if (disconnectInfo is null || disconnectInfo.ExpiresAt > DateTimeOffset.UtcNow)
                    continue;

                var gameState = await gameStateRepo.GetGameStateAsync(matchId);
                if (gameState is null || gameState.Result != GameResult.InProgress)
                {
                    await db.KeyDeleteAsync(key);
                    continue;
                }

                gameState.Result = userId == gameState.BluePlayerId ? GameResult.RedWins : GameResult.BlueWins;
                gameState.ResultReason = WinReason.Abandonment.ToString();
                gameState.Status = MatchStatus.Completed;

                await gameStateRepo.SaveGameStateAsync(gameState);
                var finalizedMatch = await lifecycleService.FinalizeMatchAsync(matchId);
                await db.KeyDeleteAsync(key);

                if (finalizedMatch is not null)
                {
                    await notificationService.SendGameEndedAsync(finalizedMatch);
                    logger.LogInformation("Processed disconnect forfeit for match {MatchId}", matchId);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error checking disconnect key {Key}", key.ToString());
            }
        }
    }

    private static bool TryParseDisconnectKey(string key, out Guid matchId, out Guid userId)
    {
        matchId = Guid.Empty;
        userId = Guid.Empty;

        var parts = key.Split(':', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 4)
            return false;

        return Guid.TryParse(parts[1], out matchId) && Guid.TryParse(parts[3], out userId);
    }
}
