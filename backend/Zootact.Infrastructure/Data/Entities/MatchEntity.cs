namespace Zootact.Infrastructure.Data.Entities;

/// <summary>
/// Match entity for PostgreSQL persistence.
/// </summary>
public sealed class MatchEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    
    public Guid BluePlayerId { get; set; }
    public Guid RedPlayerId { get; set; }
    
    /// <summary>
    /// Time control preset: "Blitz", "Rapid", "Classical".
    /// </summary>
    public required string TimeControl { get; set; }
    
    public int InitialTimeMs { get; set; }
    public int IncrementMs { get; set; }
    
    /// <summary>
    /// Match status: "InProgress", "Completed", "Abandoned".
    /// </summary>
    public string Status { get; set; } = "InProgress";
    
    /// <summary>
    /// Result: "BlueWins", "RedWins", "Draw", or null if in progress.
    /// </summary>
    public string? Result { get; set; }
    
    /// <summary>
    /// Result reason: "DenCapture", "Timeout", "Resignation", "Repetition", etc.
    /// </summary>
    public string? ResultReason { get; set; }
    
    public Guid? WinnerId { get; set; }
    
    public int BlueEloBefore { get; set; }
    public int RedEloBefore { get; set; }
    public int? BlueEloAfter { get; set; }
    public int? RedEloAfter { get; set; }
    
    public DateTimeOffset StartedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? EndedAt { get; set; }
    
    // Navigation properties
    public UserEntity BluePlayer { get; set; } = null!;
    public UserEntity RedPlayer { get; set; } = null!;
    public UserEntity? Winner { get; set; }
    public ICollection<GameMoveEntity> Moves { get; set; } = [];
    public MatchAnalysisEntity? Analysis { get; set; }
}
