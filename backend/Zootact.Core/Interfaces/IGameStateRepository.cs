using Zootact.Core.Domain;

namespace Zootact.Core.Interfaces;

public record PlayerDisconnectInfo(DateTimeOffset DisconnectedAt, DateTimeOffset ExpiresAt);

/// <summary>
/// Interface for game state persistence in Redis.
/// </summary>
public interface IGameStateRepository
{
    /// <summary>
    /// Gets the game state for a match.
    /// </summary>
    /// <param name="matchId">Match ID.</param>
    /// <returns>Game state or null if not found.</returns>
    Task<GameState?> GetGameStateAsync(Guid matchId);
    
    /// <summary>
    /// Saves or updates a game state.
    /// </summary>
    /// <param name="gameState">Game state to save.</param>
    Task SaveGameStateAsync(GameState gameState);
    
    /// <summary>
    /// Deletes a game state (match cleanup).
    /// </summary>
    /// <param name="matchId">Match ID.</param>
    Task DeleteGameStateAsync(Guid matchId);
    
    /// <summary>
    /// Gets a player's active match ID.
    /// </summary>
    /// <param name="userId">User ID.</param>
    /// <returns>Match ID or null.</returns>
    Task<Guid?> GetPlayerActiveMatchAsync(Guid userId);
    
    /// <summary>
    /// Sets a player's active match.
    /// </summary>
    /// <param name="userId">User ID.</param>
    /// <param name="matchId">Match ID.</param>
    Task SetPlayerActiveMatchAsync(Guid userId, Guid matchId);
    
    /// <summary>
    /// Clears a player's active match.
    /// </summary>
    /// <param name="userId">User ID.</param>
    Task ClearPlayerActiveMatchAsync(Guid userId);
    
    /// <summary>
    /// Records player disconnect with timestamp.
    /// </summary>
    /// <param name="matchId">Match ID.</param>
    /// <param name="userId">User ID.</param>
    /// <param name="timeout">Disconnect timeout.</param>
    Task SetPlayerDisconnectedAsync(Guid matchId, Guid userId, TimeSpan timeout);
    
    /// <summary>
    /// Clears player disconnect (on reconnect).
    /// </summary>
    /// <param name="matchId">Match ID.</param>
    /// <param name="userId">User ID.</param>
    Task ClearPlayerDisconnectedAsync(Guid matchId, Guid userId);
    
    /// <summary>
    /// Checks if a player is disconnected from a match.
    /// </summary>
    /// <param name="matchId">Match ID.</param>
    /// <param name="userId">User ID.</param>
    /// <returns>True if disconnected.</returns>
    Task<bool> IsPlayerDisconnectedAsync(Guid matchId, Guid userId);

    /// <summary>
    /// Gets the disconnect timing info for a player in a match.
    /// </summary>
    Task<PlayerDisconnectInfo?> GetPlayerDisconnectInfoAsync(Guid matchId, Guid userId);
    
    /// <summary>
    /// Stores a player's SignalR connection ID.
    /// </summary>
    /// <param name="userId">User ID.</param>
    /// <param name="connectionId">SignalR connection ID.</param>
    Task SetPlayerConnectionAsync(Guid userId, string connectionId);
    
    /// <summary>
    /// Gets a player's SignalR connection ID.
    /// </summary>
    /// <param name="userId">User ID.</param>
    /// <returns>Connection ID or null.</returns>
    Task<string?> GetPlayerConnectionAsync(Guid userId);
    
    /// <summary>
    /// Clears a player's SignalR connection ID.
    /// </summary>
    /// <param name="userId">User ID.</param>
    Task ClearPlayerConnectionAsync(Guid userId);
}
