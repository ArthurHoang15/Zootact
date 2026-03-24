namespace Zootact.Infrastructure.Data.Entities;

/// <summary>
/// Game move entity for PostgreSQL persistence.
/// Records each move made in a match for replay and analysis.
/// </summary>
public sealed class GameMoveEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    
    public Guid MatchId { get; set; }
    public Guid PlayerId { get; set; }
    
    /// <summary>
    /// Move number (1-indexed).
    /// </summary>
    public int MoveNumber { get; set; }
    
    /// <summary>
    /// Starting position as "row,col" string.
    /// </summary>
    public required string FromPosition { get; set; }
    
    /// <summary>
    /// Target position as "row,col" string.
    /// </summary>
    public required string ToPosition { get; set; }
    
    /// <summary>
    /// Type of piece moved.
    /// </summary>
    public required string PieceType { get; set; }
    
    /// <summary>
    /// Type of piece captured, if any.
    /// </summary>
    public string? CapturedPiece { get; set; }
    
    /// <summary>
    /// Time spent on this move in milliseconds.
    /// </summary>
    public int TimeSpentMs { get; set; }
    
    /// <summary>
    /// Zobrist hash of position after this move (for repetition detection).
    /// </summary>
    public long PositionHash { get; set; }
    
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    
    // Navigation properties
    public MatchEntity Match { get; set; } = null!;
    public UserEntity Player { get; set; } = null!;
}
