namespace Zootact.Core.Domain;

/// <summary>
/// Represents the complete state of a game at any point.
/// Used for real-time gameplay via Redis.
/// </summary>
public sealed class GameState
{
    /// <summary>
    /// Unique match identifier.
    /// </summary>
    public required Guid MatchId { get; init; }
    
    /// <summary>
    /// Blue player's user ID.
    /// </summary>
    public required Guid BluePlayerId { get; init; }
    
    /// <summary>
    /// Red player's user ID.
    /// </summary>
    public required Guid RedPlayerId { get; init; }
    
    /// <summary>
    /// Current board state.
    /// </summary>
    public required Board Board { get; set; }
    
    /// <summary>
    /// Player whose turn it is.
    /// </summary>
    public Player CurrentTurn { get; set; } = Player.Blue;
    
    /// <summary>
    /// Number of moves made (half-moves/plies).
    /// </summary>
    public int MoveCount { get; set; }
    
    /// <summary>
    /// Time control settings and remaining time.
    /// </summary>
    public required TimeControl TimeControl { get; set; }
    
    /// <summary>
    /// Number of moves since the last capture (for Rule of 30).
    /// </summary>
    public int MovesSinceCapture { get; set; }
    
    /// <summary>
    /// List of board position hashes for repetition detection.
    /// </summary>
    public List<long> PositionHistory { get; init; } = [];
    
    /// <summary>
    /// Move history as notation strings (for replay).
    /// </summary>
    public List<string> MoveHistory { get; init; } = [];
    
    /// <summary>
    /// Current game result.
    /// </summary>
    public GameResult Result { get; set; } = GameResult.InProgress;

    /// <summary>
    /// Reason for the current result, when the game has ended.
    /// Uses WinReason/DrawReason enum names for API consistency.
    /// </summary>
    public string? ResultReason { get; set; }
    
    /// <summary>
    /// When the match was created.
    /// </summary>
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
    
    /// <summary>
    /// Current game status.
    /// </summary>
    public MatchStatus Status { get; set; } = MatchStatus.InProgress;

    /// <summary>
    /// Number of times the Blue player blurred/unfocused the game window.
    /// </summary>
    public int BlueBlurCount { get; set; }

    /// <summary>
    /// Number of times the Red player blurred/unfocused the game window.
    /// </summary>
    public int RedBlurCount { get; set; }
    
    /// <summary>
    /// Gets the opponent of the current player.
    /// </summary>
    public Player GetOpponent() => 
        CurrentTurn == Player.Blue ? Player.Red : Player.Blue;
    
    /// <summary>
    /// Switches turn to the other player.
    /// </summary>
    public void SwitchTurn() =>
        CurrentTurn = GetOpponent();
    
    /// <summary>
    /// Gets the user ID for a player color.
    /// </summary>
    public Guid GetPlayerId(Player player) =>
        player == Player.Blue ? BluePlayerId : RedPlayerId;
    
    /// <summary>
    /// Gets the player color for a user ID.
    /// </summary>
    public Player? GetPlayerColor(Guid userId)
    {
        if (userId == BluePlayerId) return Player.Blue;
        if (userId == RedPlayerId) return Player.Red;
        return null;
    }
    
    /// <summary>
    /// Checks if a user is a participant in this game.
    /// </summary>
    public bool IsParticipant(Guid userId) =>
        userId == BluePlayerId || userId == RedPlayerId;
    
    /// <summary>
    /// Creates a new game state with initial board setup.
    /// </summary>
    public static GameState Create(
        Guid matchId,
        Guid bluePlayerId,
        Guid redPlayerId,
        TimeControlPreset preset)
    {
        return Create(matchId, bluePlayerId, redPlayerId, TimeControl.FromPreset(preset));
    }

    /// <summary>
    /// Creates a new game state with a supplied time control.
    /// </summary>
    public static GameState Create(
        Guid matchId,
        Guid bluePlayerId,
        Guid redPlayerId,
        TimeControl timeControl)
    {
        return new GameState
        {
            MatchId = matchId,
            BluePlayerId = bluePlayerId,
            RedPlayerId = redPlayerId,
            Board = Board.CreateInitialBoard(),
            TimeControl = timeControl,
            CreatedAt = DateTimeOffset.UtcNow
        };
    }
}
