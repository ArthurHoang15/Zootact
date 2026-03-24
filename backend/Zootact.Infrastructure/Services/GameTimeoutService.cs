using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using Zootact.Core.Domain;
using Zootact.Core.Interfaces;

namespace Zootact.Infrastructure.Services;

/// <summary>
/// Background service that finalizes games when a player's main clock expires.
/// This keeps timeout resolution authoritative even when neither client submits a move.
/// </summary>
public sealed class GameTimeoutService(
    IServiceProvider serviceProvider,
    ILogger<GameTimeoutService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Game Timeout Service started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CheckActiveGamesAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error in Game Timeout Service");
            }

            await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
        }

        logger.LogInformation("Game Timeout Service stopped");
    }

    private async Task CheckActiveGamesAsync(CancellationToken cancellationToken)
    {
        using var scope = serviceProvider.CreateScope();
        var redis = scope.ServiceProvider.GetRequiredService<IConnectionMultiplexer>();
        var gameStateRepo = scope.ServiceProvider.GetRequiredService<IGameStateRepository>();
        var lifecycleService = scope.ServiceProvider.GetRequiredService<IMatchLifecycleService>();
        var notificationService = scope.ServiceProvider.GetRequiredService<IMatchNotificationService>();

        var server = redis.GetServer(redis.GetEndPoints().First());
        var keys = server.Keys(pattern: "game:*", pageSize: 100);
        var now = DateTimeOffset.UtcNow;

        foreach (var key in keys)
        {
            if (cancellationToken.IsCancellationRequested)
                break;

            if (!TryParseGameKey(key.ToString(), out var matchId))
                continue;

            try
            {
                var gameState = await gameStateRepo.GetGameStateAsync(matchId);
                if (gameState is null || gameState.Result != GameResult.InProgress)
                    continue;

                var blueDisconnected = await gameStateRepo.IsPlayerDisconnectedAsync(matchId, gameState.BluePlayerId);
                var redDisconnected = await gameStateRepo.IsPlayerDisconnectedAsync(matchId, gameState.RedPlayerId);
                if (blueDisconnected || redDisconnected)
                    continue;

                var updatedTimeControl = gameState.TimeControl.AdvanceTo(gameState.CurrentTurn, now);
                if (!updatedTimeControl.IsTimeout(gameState.CurrentTurn))
                    continue;

                gameState.TimeControl = updatedTimeControl;
                gameState.Result = gameState.CurrentTurn == Player.Blue ? GameResult.RedWins : GameResult.BlueWins;
                gameState.ResultReason = WinReason.Timeout.ToString();
                gameState.Status = MatchStatus.Completed;

                await gameStateRepo.SaveGameStateAsync(gameState);

                var finalizedMatch = await lifecycleService.FinalizeMatchAsync(matchId);
                if (finalizedMatch is not null)
                {
                    await notificationService.SendGameEndedAsync(finalizedMatch);
                    logger.LogInformation("Processed clock timeout for match {MatchId}", matchId);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error checking game timeout for {MatchId}", matchId);
            }
        }
    }

    private static bool TryParseGameKey(string key, out Guid matchId)
    {
        matchId = Guid.Empty;

        var parts = key.Split(':', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length == 2 && parts[0] == "game" && Guid.TryParse(parts[1], out matchId);
    }
}
