using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using Zootact.Core.Domain;
using Zootact.Core.Interfaces;
using Zootact.Infrastructure.Data;
using Zootact.Infrastructure.Data.Entities;

namespace Zootact.Infrastructure.Services;

/// <summary>
/// Matchmaking service using Redis sorted sets for ELO-based matching.
/// </summary>
public sealed class MatchmakingService(
    IConnectionMultiplexer redis,
    ZootactDbContext dbContext,
    IGameStateRepository gameStateRepository,
    IPrivateLobbyRepository privateLobbyRepository,
    ILogger<MatchmakingService> logger) : IMatchmakingService
{
    private IDatabase Db => redis.GetDatabase();

    // ELO range for matching (players within +/-100 Forest Points)
    private const int EloRange = 100;
    private static readonly TimeSpan QueueLockTtl = TimeSpan.FromSeconds(3);

    /// <inheritdoc />
    public async Task<Guid?> JoinQueueAsync(Guid userId, TimeControlPreset preset)
    {
        var queueLockKey = GetQueueLockKey(preset);
        var lockToken = Guid.NewGuid().ToString("N");
        var lockAcquired = false;

        for (var attempt = 0; attempt < 10 && !lockAcquired; attempt++)
        {
            lockAcquired = await Db.LockTakeAsync(queueLockKey, lockToken, QueueLockTtl);
            if (!lockAcquired)
            {
                await Task.Delay(50);
            }
        }

        if (!lockAcquired)
        {
            logger.LogWarning("Failed to acquire matchmaking lock for {Preset}", preset);
            return null;
        }

        try
        {
            var activeLobby = await privateLobbyRepository.GetPlayerActiveLobbyAsync(userId);
            if (activeLobby is not null)
            {
                throw new InvalidOperationException("Leave your private lobby before joining matchmaking.");
            }

            var activeMatch = await gameStateRepository.GetPlayerActiveMatchAsync(userId);
            if (activeMatch is not null)
            {
                logger.LogWarning("User {UserId} already in active match {MatchId}", userId, activeMatch);
                return null;
            }

            var user = await dbContext.Users.FindAsync(userId);
            if (user is null)
            {
                logger.LogError("User {UserId} not found", userId);
                return null;
            }

            var queueKey = GetQueueKey(preset);
            var playerElo = user.ForestPoints;
            var userIdString = userId.ToString();

            // Remove any stale copy of this player before searching so we don't match against ourselves.
            await Db.SortedSetRemoveAsync(queueKey, userIdString);

            var minElo = playerElo - EloRange;
            var maxElo = playerElo + EloRange;

            var opponents = await Db.SortedSetRangeByScoreAsync(
                queueKey,
                minElo,
                maxElo,
                take: 10);

            if (opponents.Length == 0)
            {
                opponents = await Db.SortedSetRangeByRankAsync(queueKey, 0, 9);
            }

            foreach (var opponent in opponents)
            {
                var opponentIdStr = opponent.ToString();
                if (!Guid.TryParse(opponentIdStr, out var opponentId) || opponentId == userId)
                {
                    continue;
                }

                var removed = await Db.SortedSetRemoveAsync(queueKey, opponentIdStr);
                if (!removed)
                {
                    continue;
                }

                var isBlue = Random.Shared.Next(2) == 0;
                var blueId = isBlue ? userId : opponentId;
                var redId = isBlue ? opponentId : userId;

                var matchId = await CreateMatchAsync(blueId, redId, preset);

                logger.LogInformation(
                    "Match created: {MatchId} (Blue: {BlueId}, Red: {RedId})",
                    matchId,
                    blueId,
                    redId);

                return matchId;
            }

            await Db.SortedSetAddAsync(queueKey, userIdString, playerElo);

            logger.LogInformation(
                "User {UserId} joined {Preset} queue (ELO: {Elo})",
                userId,
                preset,
                playerElo);

            return null;
        }
        finally
        {
            await Db.LockReleaseAsync(queueLockKey, lockToken);
        }
    }

    /// <inheritdoc />
    public async Task LeaveQueueAsync(Guid userId)
    {
        foreach (TimeControlPreset preset in Enum.GetValues<TimeControlPreset>())
        {
            var queueKey = GetQueueKey(preset);
            await Db.SortedSetRemoveAsync(queueKey, userId.ToString());
        }

        logger.LogInformation("User {UserId} left matchmaking queue", userId);
    }

    /// <inheritdoc />
    public async Task<int> GetQueuePositionAsync(Guid userId, TimeControlPreset preset)
    {
        var queueKey = GetQueueKey(preset);
        var rank = await Db.SortedSetRankAsync(queueKey, userId.ToString());

        return rank.HasValue ? (int)rank.Value + 1 : 0;
    }

    /// <inheritdoc />
    public async Task<Guid> CreateMatchAsync(Guid bluePlayerId, Guid redPlayerId, TimeControlPreset preset, MatchMode matchMode = MatchMode.Rated)
    {
        var blueUser = await dbContext.Users.FindAsync(bluePlayerId);
        var redUser = await dbContext.Users.FindAsync(redPlayerId);

        if (blueUser is null || redUser is null)
        {
            throw new InvalidOperationException("One or both players not found.");
        }

        var matchId = Guid.NewGuid();
        var gameState = GameState.Create(matchId, bluePlayerId, redPlayerId, preset);

        await gameStateRepository.SaveGameStateAsync(gameState);
        await gameStateRepository.SetPlayerActiveMatchAsync(bluePlayerId, matchId);
        await gameStateRepository.SetPlayerActiveMatchAsync(redPlayerId, matchId);

        var timeControl = TimeControl.FromPreset(preset);
        var match = new MatchEntity
        {
            Id = matchId,
            BluePlayerId = bluePlayerId,
            RedPlayerId = redPlayerId,
            TimeControl = MatchTypeMetadata.EncodeTimeControl(preset, matchMode),
            InitialTimeMs = timeControl.InitialTimeMs,
            IncrementMs = timeControl.IncrementMs,
            Status = "InProgress",
            BlueEloBefore = blueUser.ForestPoints,
            RedEloBefore = redUser.ForestPoints,
            StartedAt = DateTimeOffset.UtcNow
        };

        dbContext.Matches.Add(match);
        await dbContext.SaveChangesAsync();

        logger.LogInformation("Match record created in DB: {MatchId}", matchId);

        return matchId;
    }

    private static string GetQueueKey(TimeControlPreset preset) => $"matchmaking:{preset}";
    private static string GetQueueLockKey(TimeControlPreset preset) => $"matchmaking:{preset}:lock";
}
