namespace Zootact.Infrastructure.Data.Entities;

/// <summary>
/// User statistics entity for PostgreSQL persistence.
/// </summary>
public sealed class UserStatsEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    
    public Guid UserId { get; set; }
    
    public int TotalGames { get; set; }
    public int Wins { get; set; }
    public int Losses { get; set; }
    public int Draws { get; set; }
    
    public int WinStreakCurrent { get; set; }
    public int WinStreakBest { get; set; }
    
    public decimal? AvgMoveTimeMs { get; set; }
    public long TotalPlayTimeMs { get; set; }
    
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    
    // Navigation property
    public UserEntity User { get; set; } = null!;
}
