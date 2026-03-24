using Zootact.Core.Domain;

namespace Zootact.Core.GameLogic;

/// <summary>
/// Interface for move validation.
/// </summary>
public interface IMoveValidator
{
    /// <summary>
    /// Validates if a move is legal.
    /// </summary>
    /// <param name="board">Current board state.</param>
    /// <param name="from">Starting position.</param>
    /// <param name="to">Target position.</param>
    /// <param name="player">Player making the move.</param>
    /// <returns>Validation result with error message if invalid.</returns>
    MoveValidationResult ValidateMove(Board board, Position from, Position to, Player player);
    
    /// <summary>
    /// Gets all valid moves for a piece at a position.
    /// </summary>
    IEnumerable<Position> GetValidMoves(Board board, Position from, Player player);
    
    /// <summary>
    /// Gets all valid moves for a player.
    /// </summary>
    IEnumerable<(Position From, Position To)> GetAllValidMoves(Board board, Player player);
    
    /// <summary>
    /// Checks if a move is a capture.
    /// </summary>
    bool IsCapture(Board board, Position from, Position to, Player player);
}

/// <summary>
/// Result of move validation.
/// </summary>
/// <param name="IsValid">Whether the move is valid.</param>
/// <param name="ErrorCode">Error code if invalid.</param>
/// <param name="ErrorMessage">Human-readable error message.</param>
public record MoveValidationResult(bool IsValid, string? ErrorCode = null, string? ErrorMessage = null)
{
    public static MoveValidationResult Valid() => new(true);
    public static MoveValidationResult Invalid(string errorCode, string message) => new(false, errorCode, message);
}
