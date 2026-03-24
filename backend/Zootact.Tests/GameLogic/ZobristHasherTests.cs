using Xunit;
using Zootact.Core.Domain;
using Zootact.Core.GameLogic;

namespace Zootact.Tests.GameLogic;

/// <summary>
/// Tests for ZobristHasher - position hashing for repetition detection.
/// </summary>
public class ZobristHasherTests
{
    private readonly ZobristHasher _hasher = new();
    
    [Fact]
    public void ComputeHash_SamePosition_SameHash()
    {
        // Arrange
        var board1 = Board.CreateInitialBoard();
        var board2 = Board.CreateInitialBoard();
        
        // Act
        var hash1 = _hasher.ComputeHash(board1, Player.Blue);
        var hash2 = _hasher.ComputeHash(board2, Player.Blue);
        
        // Assert
        Assert.Equal(hash1, hash2);
    }
    
    [Fact]
    public void ComputeHash_DifferentPosition_DifferentHash()
    {
        // Arrange
        var board1 = Board.CreateInitialBoard();
        var board2 = Board.CreateInitialBoard();
        
        // Make a move on board2
        var piece = board2[6, 0]!; // Blue Elephant
        board2[6, 0] = null;
        board2[5, 0] = piece.MoveTo(new Position(5, 0));
        
        // Act
        var hash1 = _hasher.ComputeHash(board1, Player.Blue);
        var hash2 = _hasher.ComputeHash(board2, Player.Blue);
        
        // Assert
        Assert.NotEqual(hash1, hash2);
    }
    
    [Fact]
    public void ComputeHash_SamePositionDifferentTurn_DifferentHash()
    {
        // Arrange
        var board = Board.CreateInitialBoard();
        
        // Act
        var hashBlue = _hasher.ComputeHash(board, Player.Blue);
        var hashRed = _hasher.ComputeHash(board, Player.Red);
        
        // Assert
        Assert.NotEqual(hashBlue, hashRed);
    }
    
    [Fact]
    public void UpdateHash_ReturnsCorrectHash()
    {
        // Arrange
        var board = Board.CreateInitialBoard();
        var initialHash = _hasher.ComputeHash(board, Player.Blue);
        
        // Move Blue Elephant from (6,0) to (5,0)
        var piece = board[6, 0]!;
        var from = new Position(6, 0);
        var to = new Position(5, 0);
        
        board[6, 0] = null;
        board[5, 0] = piece.MoveTo(to);
        
        // Act
        var updatedHash = _hasher.UpdateHash(initialHash, piece, from, to, null);
        var computedHash = _hasher.ComputeHash(board, Player.Red); // Turn switches after move
        
        // Assert
        Assert.Equal(computedHash, updatedHash);
    }
    
    [Fact]
    public void UpdateHash_WithCapture_ReturnsCorrectHash()
    {
        // Arrange
        var board = new Board();
        var blueLion = new Piece(PieceType.Lion, Player.Blue, new Position(4, 0));
        var redCat = new Piece(PieceType.Cat, Player.Red, new Position(5, 0));
        
        board[4, 0] = blueLion;
        board[5, 0] = redCat;
        
        var initialHash = _hasher.ComputeHash(board, Player.Blue);
        
        // Lion captures Cat
        var from = new Position(4, 0);
        var to = new Position(5, 0);
        
        board[4, 0] = null;
        board[5, 0] = blueLion.MoveTo(to);
        
        // Act
        var updatedHash = _hasher.UpdateHash(initialHash, blueLion, from, to, redCat);
        var computedHash = _hasher.ComputeHash(board, Player.Red);
        
        // Assert
        Assert.Equal(computedHash, updatedHash);
    }
}
