using System.Text.Json;
using StackExchange.Redis;
using Zootact.Core.Domain;
using Zootact.Core.Interfaces;

namespace Zootact.Infrastructure.Repositories;

/// <summary>
/// Redis implementation of game state repository.
/// Stores live game state for real-time gameplay.
/// </summary>
public sealed class RedisGameStateRepository(IConnectionMultiplexer redis) : IGameStateRepository
{
    private IDatabase Db => redis.GetDatabase();
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    
    // Key patterns
    private static string GameKey(Guid matchId) => $"game:{matchId}";
    private static string PlayerActiveMatchKey(Guid userId) => $"player:{userId}:active_match";
    private static string PlayerConnectionKey(Guid userId) => $"player:{userId}:connection";
    private static string DisconnectKey(Guid matchId, Guid userId) => $"game:{matchId}:disconnect:{userId}";
    
    // TTL for game state (4 hours for abandoned games)
    private static readonly TimeSpan GameStateTtl = TimeSpan.FromHours(4);
    
    /// <inheritdoc />
    public async Task<GameState?> GetGameStateAsync(Guid matchId)
    {
        var key = GameKey(matchId);
        var hash = await Db.HashGetAllAsync(key);
        
        if (hash.Length == 0)
            return null;
        
        var dict = hash.ToDictionary(
            x => x.Name.ToString(),
            x => x.Value.ToString());
        
        return DeserializeGameState(dict);
    }
    
    /// <inheritdoc />
    public async Task SaveGameStateAsync(GameState gameState)
    {
        var key = GameKey(gameState.MatchId);
        var hash = SerializeGameState(gameState);
        
        await Db.HashSetAsync(key, hash);
        await Db.KeyExpireAsync(key, GameStateTtl);
    }
    
    /// <inheritdoc />
    public async Task DeleteGameStateAsync(Guid matchId)
    {
        var key = GameKey(matchId);
        await Db.KeyDeleteAsync(key);
    }
    
    /// <inheritdoc />
    public async Task<Guid?> GetPlayerActiveMatchAsync(Guid userId)
    {
        var key = PlayerActiveMatchKey(userId);
        var value = await Db.StringGetAsync(key);
        
        if (value.IsNullOrEmpty)
            return null;
        
        return Guid.TryParse(value.ToString(), out var matchId) ? matchId : null;
    }
    
    /// <inheritdoc />
    public async Task SetPlayerActiveMatchAsync(Guid userId, Guid matchId)
    {
        var key = PlayerActiveMatchKey(userId);
        await Db.StringSetAsync(key, matchId.ToString());
    }
    
    /// <inheritdoc />
    public async Task ClearPlayerActiveMatchAsync(Guid userId)
    {
        var key = PlayerActiveMatchKey(userId);
        await Db.KeyDeleteAsync(key);
    }
    
    /// <inheritdoc />
    public async Task SetPlayerDisconnectedAsync(Guid matchId, Guid userId, TimeSpan timeout)
    {
        var key = DisconnectKey(matchId, userId);
        var payload = JsonSerializer.Serialize(new PlayerDisconnectInfo(
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow.Add(timeout)), JsonOptions);
        await Db.StringSetAsync(key, payload, timeout.Add(TimeSpan.FromMinutes(5)));
    }
    
    /// <inheritdoc />
    public async Task ClearPlayerDisconnectedAsync(Guid matchId, Guid userId)
    {
        var key = DisconnectKey(matchId, userId);
        await Db.KeyDeleteAsync(key);
    }
    
    /// <inheritdoc />
    public async Task<bool> IsPlayerDisconnectedAsync(Guid matchId, Guid userId)
    {
        var key = DisconnectKey(matchId, userId);
        return await Db.KeyExistsAsync(key);
    }

    /// <inheritdoc />
    public async Task<PlayerDisconnectInfo?> GetPlayerDisconnectInfoAsync(Guid matchId, Guid userId)
    {
        var key = DisconnectKey(matchId, userId);
        var value = await Db.StringGetAsync(key);
        if (value.IsNullOrEmpty)
        {
            return null;
        }

        return DeserializeDisconnectInfo(value.ToString());
    }
    
    /// <inheritdoc />
    public async Task SetPlayerConnectionAsync(Guid userId, string connectionId)
    {
        var key = PlayerConnectionKey(userId);
        await Db.StringSetAsync(key, connectionId);
    }
    
    /// <inheritdoc />
    public async Task<string?> GetPlayerConnectionAsync(Guid userId)
    {
        var key = PlayerConnectionKey(userId);
        var value = await Db.StringGetAsync(key);
        return value.IsNullOrEmpty ? null : value.ToString();
    }
    
    /// <inheritdoc />
    public async Task ClearPlayerConnectionAsync(Guid userId)
    {
        var key = PlayerConnectionKey(userId);
        await Db.KeyDeleteAsync(key);
    }
    
    /// <summary>
    /// Serializes game state to Redis hash entries.
    /// </summary>
    private static HashEntry[] SerializeGameState(GameState state)
    {
        return
        [
            new HashEntry("match_id", state.MatchId.ToString()),
            new HashEntry("blue_player_id", state.BluePlayerId.ToString()),
            new HashEntry("red_player_id", state.RedPlayerId.ToString()),
            new HashEntry("current_turn", state.CurrentTurn.ToString()),
            new HashEntry("board", state.Board.ToJson()),
            new HashEntry("move_count", state.MoveCount),
            new HashEntry("blue_time_remaining_ms", state.TimeControl.BlueTimeRemainingMs),
            new HashEntry("red_time_remaining_ms", state.TimeControl.RedTimeRemainingMs),
            new HashEntry("last_move_timestamp", state.TimeControl.LastMoveTimestamp.ToString("O")),
            new HashEntry("time_control_preset", state.TimeControl.Preset.ToString()),
            new HashEntry("initial_time_ms", state.TimeControl.InitialTimeMs),
            new HashEntry("increment_ms", state.TimeControl.IncrementMs),
            new HashEntry("moves_since_capture", state.MovesSinceCapture),
            new HashEntry("position_history", JsonSerializer.Serialize(state.PositionHistory)),
            new HashEntry("move_history", JsonSerializer.Serialize(state.MoveHistory)),
            new HashEntry("status", state.Status.ToString()),
            new HashEntry("result", state.Result.ToString()),
            new HashEntry("result_reason", state.ResultReason ?? string.Empty),
            new HashEntry("blue_blur_count", state.BlueBlurCount),
            new HashEntry("red_blur_count", state.RedBlurCount),
            new HashEntry("created_at", state.CreatedAt.ToString("O"))
        ];
    }
    
    /// <summary>
    /// Deserializes game state from Redis hash dictionary.
    /// </summary>
    private static GameState DeserializeGameState(Dictionary<string, string> dict)
    {
        var preset = Enum.Parse<TimeControlPreset>(dict["time_control_preset"]);
        var timeControl = new TimeControl(
            preset,
            int.Parse(dict["initial_time_ms"]),
            int.Parse(dict["increment_ms"]),
            long.Parse(dict["blue_time_remaining_ms"]),
            long.Parse(dict["red_time_remaining_ms"]),
            DateTimeOffset.Parse(dict["last_move_timestamp"]));
        
        var state = new GameState
        {
            MatchId = Guid.Parse(dict["match_id"]),
            BluePlayerId = Guid.Parse(dict["blue_player_id"]),
            RedPlayerId = Guid.Parse(dict["red_player_id"]),
            Board = Board.FromJson(dict["board"]),
            TimeControl = timeControl,
            CreatedAt = DateTimeOffset.Parse(dict["created_at"])
        };
        
        state.CurrentTurn = Enum.Parse<Player>(dict["current_turn"]);
        state.MoveCount = int.Parse(dict["move_count"]);
        state.MovesSinceCapture = int.Parse(dict["moves_since_capture"]);
        state.Status = Enum.Parse<MatchStatus>(dict["status"]);
        state.Result = Enum.Parse<GameResult>(dict["result"]);
        state.ResultReason = dict.TryGetValue("result_reason", out var resultReason) && !string.IsNullOrWhiteSpace(resultReason)
            ? resultReason
            : null;
        state.BlueBlurCount = dict.TryGetValue("blue_blur_count", out var blueBlurCount)
            ? int.Parse(blueBlurCount)
            : 0;
        state.RedBlurCount = dict.TryGetValue("red_blur_count", out var redBlurCount)
            ? int.Parse(redBlurCount)
            : 0;
        
        var positionHistory = JsonSerializer.Deserialize<List<long>>(dict["position_history"]);
        if (positionHistory is not null)
            state.PositionHistory.AddRange(positionHistory);
        
        var moveHistory = JsonSerializer.Deserialize<List<string>>(dict["move_history"]);
        if (moveHistory is not null)
            state.MoveHistory.AddRange(moveHistory);
        
        return state;
    }

    private static PlayerDisconnectInfo? DeserializeDisconnectInfo(string rawValue)
    {
        try
        {
            if (rawValue.StartsWith("{", StringComparison.Ordinal))
            {
                return JsonSerializer.Deserialize<PlayerDisconnectInfo>(rawValue, JsonOptions);
            }

            if (DateTimeOffset.TryParse(rawValue, out var expiresAt))
            {
                return new PlayerDisconnectInfo(expiresAt.AddSeconds(-60), expiresAt);
            }
        }
        catch
        {
            // Ignore malformed payloads and treat them as absent.
        }

        return null;
    }
}
