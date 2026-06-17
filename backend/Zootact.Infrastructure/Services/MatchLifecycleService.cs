using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Zootact.Core.Domain;
using Zootact.Core.DTOs;
using Zootact.Core.Interfaces;
using Zootact.Infrastructure.Data;
using Zootact.Infrastructure.Data.Entities;

namespace Zootact.Infrastructure.Services;

public sealed class MatchLifecycleService(
    ZootactDbContext dbContext,
    IGameStateRepository gameStateRepository,
    AiServiceClient aiServiceClient,
    IServiceScopeFactory serviceScopeFactory,
    IOptions<AiServiceOptions> aiOptions,
    ILogger<MatchLifecycleService> logger) : IMatchLifecycleService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly AiServiceOptions _aiOptions = aiOptions.Value;

    public async Task RecordMoveAsync(Guid matchId, Guid playerId, Move move, int moveNumber, long timeSpentMs, long positionHash)
    {
        var exists = await dbContext.GameMoves.AnyAsync(m => m.MatchId == matchId && m.MoveNumber == moveNumber);
        if (exists)
        {
            return;
        }

        dbContext.GameMoves.Add(new GameMoveEntity
        {
            MatchId = matchId,
            PlayerId = playerId,
            MoveNumber = moveNumber,
            FromPosition = $"{move.From.Row},{move.From.Col}",
            ToPosition = $"{move.To.Row},{move.To.Col}",
            PieceType = move.PieceType.ToString(),
            CapturedPiece = move.CapturedPiece?.ToString(),
            TimeSpentMs = (int)Math.Min(int.MaxValue, Math.Max(0, timeSpentMs)),
            PositionHash = positionHash,
            CreatedAt = DateTimeOffset.UtcNow
        });

        await dbContext.SaveChangesAsync();
    }

    public async Task<FinalizedMatchDto?> FinalizeMatchAsync(Guid matchId)
    {
        var gameState = await gameStateRepository.GetGameStateAsync(matchId);
        if (gameState is null || gameState.Result == GameResult.InProgress)
        {
            return null;
        }

        var claimed = await dbContext.Matches
            .Where(m => m.Id == matchId && m.Status == "InProgress")
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(m => m.Status, "Finalizing"));

        if (claimed == 0)
        {
            var existing = await dbContext.Matches
                .Include(m => m.BluePlayer)
                .Include(m => m.RedPlayer)
                .Include(m => m.Analysis)
                .FirstOrDefaultAsync(m => m.Id == matchId);

            if (existing is not null && existing.Status == "Completed" && existing.BlueEloAfter.HasValue && existing.RedEloAfter.HasValue)
            {
                return BuildFinalizedResult(existing);
            }

            return null;
        }

        var match = await dbContext.Matches
            .Include(m => m.BluePlayer)
            .Include(m => m.RedPlayer)
            .Include(m => m.Analysis)
            .FirstAsync(m => m.Id == matchId);

        ApplyMatchResult(match, gameState);
        await UpdatePlayerStatsAsync(match, gameState);
        await dbContext.SaveChangesAsync();

        if (_aiOptions.Enabled)
        {
            await EnsureAnalysisRecordAsync(match.Id);
            await dbContext.SaveChangesAsync();
            QueueAnalysisGeneration(match.Id, gameState);
        }

        await gameStateRepository.ClearPlayerActiveMatchAsync(gameState.BluePlayerId);
        await gameStateRepository.ClearPlayerActiveMatchAsync(gameState.RedPlayerId);
        await gameStateRepository.DeleteGameStateAsync(gameState.MatchId);

        return BuildFinalizedResult(match);
    }

    public async Task<MatchAnalysisResponse?> GetMatchAnalysisAsync(Guid matchId, Guid requesterId)
    {
        var match = await dbContext.Matches
            .Include(m => m.Analysis)
            .FirstOrDefaultAsync(m => m.Id == matchId && (m.BluePlayerId == requesterId || m.RedPlayerId == requesterId));

        if (match is null)
        {
            return null;
        }

        if (!_aiOptions.Enabled)
        {
            return new MatchAnalysisResponse
            {
                MatchId = match.Id.ToString(),
                Status = "Disabled",
                Moves = [],
                Summary = null,
                AntiCheat = []
            };
        }

        if (match.Analysis is null)
        {
            return new MatchAnalysisResponse
            {
                MatchId = match.Id.ToString(),
                Status = "Pending",
                Moves = [],
                Summary = null,
                AntiCheat = []
            };
        }

        var moves = new List<MoveAnalysisItemDto>();
        GameAnalysisSummaryDto? summary = null;
        var antiCheat = new List<AntiCheatPlayerSummaryDto>();

        if (!string.IsNullOrWhiteSpace(match.Analysis.AnalysisJson))
        {
            var analysisNode = JsonNode.Parse(match.Analysis.AnalysisJson);
            if (analysisNode?["moves"] is JsonArray movesArray)
            {
                foreach (var item in movesArray)
                {
                    var dto = item?.Deserialize<MoveAnalysisItemDto>(JsonOptions);
                    if (dto is not null)
                    {
                        moves.Add(dto);
                    }
                }
            }

            summary = analysisNode?["summary"]?.Deserialize<GameAnalysisSummaryDto>(JsonOptions);
        }

        if (!string.IsNullOrWhiteSpace(match.Analysis.AntiCheatJson))
        {
            var antiCheatArray = JsonNode.Parse(match.Analysis.AntiCheatJson)?.AsArray();
            if (antiCheatArray is not null)
            {
                foreach (var item in antiCheatArray)
                {
                    var dto = item?.Deserialize<AntiCheatPlayerSummaryDto>(JsonOptions);
                    if (dto is not null)
                    {
                        antiCheat.Add(dto);
                    }
                }
            }
        }

        return new MatchAnalysisResponse
        {
            MatchId = match.Id.ToString(),
            Status = match.Analysis.Status,
            Moves = moves,
            Summary = summary,
            AntiCheat = antiCheat
        };
    }

    private void ApplyMatchResult(MatchEntity match, GameState gameState)
    {
        var matchMode = MatchTypeMetadata.Parse(match.TimeControl);

        match.Status = "Completed";
        match.Result = gameState.Result.ToString();
        match.ResultReason = gameState.ResultReason;
        match.EndedAt = DateTimeOffset.UtcNow;

        var (newBlueElo, newRedElo) = matchMode == MatchMode.Friendly
            ? (match.BlueEloBefore, match.RedEloBefore)
            : gameState.Result switch
            {
                GameResult.BlueWins => EloCalculator.ForBlueWin(match.BlueEloBefore, match.RedEloBefore),
                GameResult.RedWins => EloCalculator.ForBlueLoss(match.BlueEloBefore, match.RedEloBefore),
                GameResult.Draw => EloCalculator.ForDraw(match.BlueEloBefore, match.RedEloBefore),
                _ => (match.BlueEloBefore, match.RedEloBefore)
            };

        match.BlueEloAfter = newBlueElo;
        match.RedEloAfter = newRedElo;
        match.WinnerId = gameState.Result switch
        {
            GameResult.BlueWins => gameState.BluePlayerId,
            GameResult.RedWins => gameState.RedPlayerId,
            _ => null
        };

        if (matchMode == MatchMode.Rated)
        {
            match.BluePlayer.ForestPoints = newBlueElo;
            match.RedPlayer.ForestPoints = newRedElo;
        }
    }

    private async Task UpdatePlayerStatsAsync(MatchEntity match, GameState gameState)
    {
        if (MatchTypeMetadata.Parse(match.TimeControl) == MatchMode.Friendly)
        {
            return;
        }

        var blueStats = await dbContext.UserStats.FirstOrDefaultAsync(s => s.UserId == match.BluePlayerId)
            ?? dbContext.UserStats.Add(new UserStatsEntity { UserId = match.BluePlayerId }).Entity;
        var redStats = await dbContext.UserStats.FirstOrDefaultAsync(s => s.UserId == match.RedPlayerId)
            ?? dbContext.UserStats.Add(new UserStatsEntity { UserId = match.RedPlayerId }).Entity;

        UpdateStats(blueStats, gameState.Result == GameResult.BlueWins, gameState.Result == GameResult.Draw,
            await GetAverageMoveTimeAsync(match.Id, match.BluePlayerId));
        UpdateStats(redStats, gameState.Result == GameResult.RedWins, gameState.Result == GameResult.Draw,
            await GetAverageMoveTimeAsync(match.Id, match.RedPlayerId));
    }

    private async Task<decimal?> GetAverageMoveTimeAsync(Guid matchId, Guid playerId)
    {
        var moveTimes = await dbContext.GameMoves
            .Where(m => m.MatchId == matchId && m.PlayerId == playerId)
            .Select(m => m.TimeSpentMs)
            .ToListAsync();

        if (moveTimes.Count == 0)
        {
            return null;
        }

        return (decimal)moveTimes.Average();
    }

    private static void UpdateStats(UserStatsEntity stats, bool won, bool draw, decimal? avgMoveTime)
    {
        stats.TotalGames++;
        stats.AvgMoveTimeMs = avgMoveTime;
        stats.UpdatedAt = DateTimeOffset.UtcNow;

        if (won)
        {
            stats.Wins++;
            stats.WinStreakCurrent++;
            stats.WinStreakBest = Math.Max(stats.WinStreakBest, stats.WinStreakCurrent);
            return;
        }

        if (draw)
        {
            stats.Draws++;
            stats.WinStreakCurrent = 0;
            return;
        }

        stats.Losses++;
        stats.WinStreakCurrent = 0;
    }

    private async Task EnsureAnalysisRecordAsync(Guid matchId)
    {
        var existing = await dbContext.MatchAnalyses.FirstOrDefaultAsync(a => a.MatchId == matchId);
        if (existing is null)
        {
            dbContext.MatchAnalyses.Add(new MatchAnalysisEntity
            {
                MatchId = matchId,
                Status = "Pending",
                CreatedAt = DateTimeOffset.UtcNow
            });
        }
    }

    private async Task GenerateAnalysisAsync(MatchEntity match, GameState gameState)
    {
        var analysisEntity = await dbContext.MatchAnalyses.FirstAsync(a => a.MatchId == match.Id);
        var moves = await dbContext.GameMoves
            .Where(m => m.MatchId == match.Id)
            .OrderBy(m => m.MoveNumber)
            .ToListAsync();

        if (moves.Count == 0)
        {
            analysisEntity.Status = "Completed";
            analysisEntity.AnalysisJson = """{"moves":[],"summary":null}""";
            analysisEntity.AntiCheatJson = "[]";
            analysisEntity.UpdatedAt = DateTimeOffset.UtcNow;
            await dbContext.SaveChangesAsync();
            return;
        }

        try
        {
            var analysisRequest = new
            {
                moves = moves.Select(m => new
                {
                    move_number = m.MoveNumber,
                    player = m.PlayerId == match.BluePlayerId ? "Blue" : "Red",
                    from = m.FromPosition,
                    to = m.ToPosition,
                    piece = m.PieceType
                }).ToList()
            };

            var analysisJson = await aiServiceClient.AnalyzeGameAsync(analysisRequest) ?? """{"moves":[],"summary":null}""";

            var antiCheatResults = new List<AntiCheatPlayerSummaryDto>();
            foreach (var participant in new[]
                     {
                         new { UserId = match.BluePlayerId, BlurCount = gameState.BlueBlurCount },
                         new { UserId = match.RedPlayerId, BlurCount = gameState.RedBlurCount }
                     })
            {
                var moveTimes = moves
                    .Where(m => m.PlayerId == participant.UserId)
                    .Select(m => m.TimeSpentMs)
                    .ToList();

                var antiCheatJson = await aiServiceClient.AnalyzeMoveTimesAsync(new
                {
                    user_id = participant.UserId.ToString(),
                    match_id = match.Id.ToString(),
                    move_times_ms = moveTimes
                });

                var antiCheatNode = antiCheatJson is null ? null : JsonNode.Parse(antiCheatJson);
                antiCheatResults.Add(new AntiCheatPlayerSummaryDto
                {
                    UserId = participant.UserId.ToString(),
                    MoveCount = antiCheatNode?["move_count"]?.GetValue<int>() ?? moveTimes.Count,
                    IsSuspicious = antiCheatNode?["is_suspicious"]?.GetValue<bool>() ?? false,
                    SuspicionLevel = antiCheatNode?["suspicion_level"]?.GetValue<string>() ?? "none",
                    ConfidenceScore = antiCheatNode?["confidence_score"]?.GetValue<double>() ?? 0,
                    SuspicionReasons = antiCheatNode?["suspicion_reasons"]?.Deserialize<List<string>>(JsonOptions) ?? [],
                    BlurCount = participant.BlurCount
                });
            }

            analysisEntity.Status = "Completed";
            analysisEntity.AnalysisJson = analysisJson;
            analysisEntity.AntiCheatJson = JsonSerializer.Serialize(antiCheatResults, JsonOptions);
            analysisEntity.UpdatedAt = DateTimeOffset.UtcNow;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to generate post-game analysis for match {MatchId}", match.Id);
            analysisEntity.Status = "Failed";
            analysisEntity.UpdatedAt = DateTimeOffset.UtcNow;
        }

        await dbContext.SaveChangesAsync();
    }

    private void QueueAnalysisGeneration(Guid matchId, GameState gameState)
    {
        var blurCounts = new Dictionary<Guid, int>
        {
            [gameState.BluePlayerId] = gameState.BlueBlurCount,
            [gameState.RedPlayerId] = gameState.RedBlurCount
        };

        _ = Task.Run(async () =>
        {
            try
            {
                await GenerateAnalysisInBackgroundAsync(matchId, blurCounts);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Background analysis generation failed for match {MatchId}", matchId);
            }
        });
    }

    private async Task GenerateAnalysisInBackgroundAsync(Guid matchId, IReadOnlyDictionary<Guid, int> blurCounts)
    {
        using var scope = serviceScopeFactory.CreateScope();
        var scopedDbContext = scope.ServiceProvider.GetRequiredService<ZootactDbContext>();
        var scopedAiServiceClient = scope.ServiceProvider.GetRequiredService<AiServiceClient>();

        var match = await scopedDbContext.Matches
            .Include(m => m.Analysis)
            .FirstOrDefaultAsync(m => m.Id == matchId);

        if (match is null)
        {
            return;
        }

        var analysisEntity = match.Analysis ?? await scopedDbContext.MatchAnalyses.FirstOrDefaultAsync(a => a.MatchId == matchId);
        if (analysisEntity is null || analysisEntity.Status == "Completed")
        {
            return;
        }

        var moves = await scopedDbContext.GameMoves
            .Where(m => m.MatchId == matchId)
            .OrderBy(m => m.MoveNumber)
            .ToListAsync();

        if (moves.Count == 0)
        {
            analysisEntity.Status = "Completed";
            analysisEntity.AnalysisJson = """{"moves":[],"summary":null}""";
            analysisEntity.AntiCheatJson = "[]";
            analysisEntity.UpdatedAt = DateTimeOffset.UtcNow;
            await scopedDbContext.SaveChangesAsync();
            return;
        }

        try
        {
            var analysisRequest = new
            {
                moves = moves.Select(m => new
                {
                    move_number = m.MoveNumber,
                    player = m.PlayerId == match.BluePlayerId ? "Blue" : "Red",
                    from = m.FromPosition,
                    to = m.ToPosition,
                    piece = m.PieceType
                }).ToList()
            };

            var analysisJson = await scopedAiServiceClient.AnalyzeGameAsync(analysisRequest) ?? """{"moves":[],"summary":null}""";

            var antiCheatResults = new List<AntiCheatPlayerSummaryDto>();
            foreach (var participant in new[]
                     {
                         new { UserId = match.BluePlayerId, BlurCount = blurCounts.TryGetValue(match.BluePlayerId, out var blueBlurCount) ? blueBlurCount : 0 },
                         new { UserId = match.RedPlayerId, BlurCount = blurCounts.TryGetValue(match.RedPlayerId, out var redBlurCount) ? redBlurCount : 0 }
                     })
            {
                var moveTimes = moves
                    .Where(m => m.PlayerId == participant.UserId)
                    .Select(m => m.TimeSpentMs)
                    .ToList();

                var antiCheatJson = await scopedAiServiceClient.AnalyzeMoveTimesAsync(new
                {
                    user_id = participant.UserId.ToString(),
                    match_id = match.Id.ToString(),
                    move_times_ms = moveTimes
                });

                var antiCheatNode = antiCheatJson is null ? null : JsonNode.Parse(antiCheatJson);
                antiCheatResults.Add(new AntiCheatPlayerSummaryDto
                {
                    UserId = participant.UserId.ToString(),
                    MoveCount = antiCheatNode?["move_count"]?.GetValue<int>() ?? moveTimes.Count,
                    IsSuspicious = antiCheatNode?["is_suspicious"]?.GetValue<bool>() ?? false,
                    SuspicionLevel = antiCheatNode?["suspicion_level"]?.GetValue<string>() ?? "none",
                    ConfidenceScore = antiCheatNode?["confidence_score"]?.GetValue<double>() ?? 0,
                    SuspicionReasons = antiCheatNode?["suspicion_reasons"]?.Deserialize<List<string>>(JsonOptions) ?? [],
                    BlurCount = participant.BlurCount
                });
            }

            analysisEntity.Status = "Completed";
            analysisEntity.AnalysisJson = analysisJson;
            analysisEntity.AntiCheatJson = JsonSerializer.Serialize(antiCheatResults, JsonOptions);
            analysisEntity.UpdatedAt = DateTimeOffset.UtcNow;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to generate post-game analysis for match {MatchId}", matchId);
            analysisEntity.Status = "Failed";
            analysisEntity.UpdatedAt = DateTimeOffset.UtcNow;
        }

        await scopedDbContext.SaveChangesAsync();
    }

    private static FinalizedMatchDto BuildFinalizedResult(MatchEntity match)
    {
        var reason = match.ResultReason ?? "Unknown";
        var blueAfter = match.BlueEloAfter ?? match.BlueEloBefore;
        var redAfter = match.RedEloAfter ?? match.RedEloBefore;

        return new FinalizedMatchDto
        {
            MatchId = match.Id,
            Result = match.Result ?? GameResult.InProgress.ToString(),
            Reason = reason,
            Blue = new PlayerMatchSummaryDto
            {
                UserId = match.BluePlayerId,
                Result = match.Result ?? GameResult.InProgress.ToString(),
                Reason = reason,
                NewElo = blueAfter,
                EloChange = blueAfter - match.BlueEloBefore
            },
            Red = new PlayerMatchSummaryDto
            {
                UserId = match.RedPlayerId,
                Result = match.Result ?? GameResult.InProgress.ToString(),
                Reason = reason,
                NewElo = redAfter,
                EloChange = redAfter - match.RedEloBefore
            }
        };
    }
}
