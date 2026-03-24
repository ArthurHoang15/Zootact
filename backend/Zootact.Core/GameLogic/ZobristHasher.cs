using Zootact.Core.Domain;

namespace Zootact.Core.GameLogic;

/// <summary>
/// Implements Zobrist hashing for efficient board state comparison.
/// Used for detecting threefold repetition draw.
/// </summary>
public sealed class ZobristHasher
{
    // Random 64-bit keys for each (piece type, owner, position) combination
    // Layout: [Player (2)][PieceType (8)][Row (9)][Col (7)]
    private readonly long[,,,] _pieceKeys;
    
    // Key for the current player to move
    private readonly long _blueToMoveKey;
    
    private static readonly Random Random = new(42); // Fixed seed for consistency
    
    public ZobristHasher()
    {
        _pieceKeys = new long[2, 9, BoardConstants.Rows, BoardConstants.Cols];
        
        // Initialize random keys for all combinations
        for (var player = 0; player < 2; player++)
        {
            for (var pieceType = 1; pieceType <= 8; pieceType++)
            {
                for (var row = 0; row < BoardConstants.Rows; row++)
                {
                    for (var col = 0; col < BoardConstants.Cols; col++)
                    {
                        _pieceKeys[player, pieceType, row, col] = NextRandomLong();
                    }
                }
            }
        }
        
        _blueToMoveKey = NextRandomLong();
    }
    
    /// <summary>
    /// Computes the Zobrist hash for a board position.
    /// </summary>
    /// <param name="board">The board to hash.</param>
    /// <param name="currentPlayer">The player to move.</param>
    /// <returns>64-bit hash of the position.</returns>
    public long ComputeHash(Board board, Player currentPlayer)
    {
        long hash = 0;
        
        // XOR in all pieces
        for (var row = 0; row < BoardConstants.Rows; row++)
        {
            for (var col = 0; col < BoardConstants.Cols; col++)
            {
                var piece = board[row, col];
                if (piece is not null)
                {
                    var playerIndex = (int)piece.Owner;
                    var pieceTypeIndex = (int)piece.Type;
                    hash ^= _pieceKeys[playerIndex, pieceTypeIndex, row, col];
                }
            }
        }
        
        // XOR in the side to move
        if (currentPlayer == Player.Blue)
        {
            hash ^= _blueToMoveKey;
        }
        
        return hash;
    }
    
    /// <summary>
    /// Incrementally updates a hash after a move.
    /// More efficient than recomputing the entire hash.
    /// </summary>
    /// <param name="currentHash">The current hash value.</param>
    /// <param name="piece">The piece that moved.</param>
    /// <param name="from">Starting position.</param>
    /// <param name="to">Ending position.</param>
    /// <param name="capturedPiece">The captured piece, if any.</param>
    /// <returns>Updated hash value.</returns>
    public long UpdateHash(long currentHash, Piece piece, Position from, Position to, Piece? capturedPiece)
    {
        var playerIndex = (int)piece.Owner;
        var pieceTypeIndex = (int)piece.Type;
        
        // Remove piece from old position
        currentHash ^= _pieceKeys[playerIndex, pieceTypeIndex, from.Row, from.Col];
        
        // Add piece to new position
        currentHash ^= _pieceKeys[playerIndex, pieceTypeIndex, to.Row, to.Col];
        
        // Remove captured piece if any
        if (capturedPiece is not null)
        {
            var capturedPlayerIndex = (int)capturedPiece.Owner;
            var capturedTypeIndex = (int)capturedPiece.Type;
            currentHash ^= _pieceKeys[capturedPlayerIndex, capturedTypeIndex, to.Row, to.Col];
        }
        
        // Toggle side to move
        currentHash ^= _blueToMoveKey;
        
        return currentHash;
    }
    
    /// <summary>
    /// Generates a random 64-bit integer.
    /// </summary>
    private static long NextRandomLong()
    {
        var buffer = new byte[8];
        Random.NextBytes(buffer);
        return BitConverter.ToInt64(buffer, 0);
    }
}
