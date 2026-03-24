namespace Zootact.Infrastructure.Data.Entities;

/// <summary>
/// User entity for PostgreSQL persistence.
/// Uses Firebase UID as primary authentication identifier.
/// </summary>
public sealed class UserEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    
    /// <summary>
    /// Firebase User UID - primary authentication identifier.
    /// </summary>
    public required string FirebaseUid { get; set; }
    
    public required string Username { get; set; }
    
    public required string Email { get; set; }
    
    public string? AvatarUrl { get; set; }
    
    /// <summary>
    /// Forest Points (Elo rating). Default is 1200.
    /// </summary>
    public int ForestPoints { get; set; } = 1200;
    
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    
    public DateTimeOffset? LastLoginAt { get; set; }
    
    public bool IsBanned { get; set; }
    
    // Navigation properties
    public ICollection<MatchEntity> BlueMatches { get; set; } = [];
    public ICollection<MatchEntity> RedMatches { get; set; } = [];
    public ICollection<GameMoveEntity> Moves { get; set; } = [];
    public UserStatsEntity? Stats { get; set; }
}
