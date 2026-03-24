using Zootact.Core.Domain;

namespace Zootact.Core.Interfaces;

/// <summary>
/// Interface for matchmaking operations.
/// </summary>
public interface IMatchmakingService
{
    /// <summary>
    /// Adds a player to the matchmaking queue.
    /// Returns match ID if matched immediately, otherwise null.
    /// </summary>
    Task<Guid?> JoinQueueAsync(Guid userId, TimeControlPreset preset);
    
    /// <summary>
    /// Removes a player from the matchmaking queue.
    /// </summary>
    Task LeaveQueueAsync(Guid userId);
    
    /// <summary>
    /// Gets queue position for a player.
    /// </summary>
    Task<int> GetQueuePositionAsync(Guid userId, TimeControlPreset preset);
    
    /// <summary>
    /// Creates a new match between two players.
    /// </summary>
    Task<Guid> CreateMatchAsync(Guid bluePlayerId, Guid redPlayerId, TimeControlPreset preset);
}
