namespace Zootact.Core.Domain;

/// <summary>
/// Represents a move in the game.
/// </summary>
/// <param name="From">Starting position.</param>
/// <param name="To">Target position.</param>
/// <param name="PieceType">Type of piece being moved.</param>
/// <param name="CapturedPiece">The captured piece type, if any.</param>
/// <param name="Timestamp">When the move was made.</param>
public record Move(
    Position From,
    Position To,
    PieceType PieceType,
    PieceType? CapturedPiece = null,
    DateTimeOffset Timestamp = default)
{
    /// <summary>
    /// Creates a move with the current timestamp.
    /// </summary>
    public static Move Create(Position from, Position to, PieceType pieceType, PieceType? captured = null) =>
        new(from, to, pieceType, captured, DateTimeOffset.UtcNow);
}
