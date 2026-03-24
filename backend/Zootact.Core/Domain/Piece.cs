namespace Zootact.Core.Domain;

/// <summary>
/// Represents a game piece with its type, owner, and current position.
/// </summary>
/// <param name="Type">The type of animal piece.</param>
/// <param name="Owner">The player who owns this piece.</param>
/// <param name="Position">Current position on the board.</param>
public record Piece(PieceType Type, Player Owner, Position Position)
{
    /// <summary>
    /// Gets the rank of this piece (1-8). Used for capture comparisons.
    /// </summary>
    public int Rank => (int)Type;
    
    /// <summary>
    /// Creates a copy of this piece at a new position.
    /// </summary>
    public Piece MoveTo(Position newPosition) => this with { Position = newPosition };
}
