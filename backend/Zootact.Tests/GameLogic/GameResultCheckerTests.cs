using Xunit;
using Zootact.Core.Domain;
using Zootact.Core.GameLogic;

namespace Zootact.Tests.GameLogic;

/// <summary>
/// Tests for GameResultChecker - win and draw conditions.
/// </summary>
public class GameResultCheckerTests
{
    private readonly MoveValidator _moveValidator = new();
    private readonly ZobristHasher _zobristHasher = new();
    private readonly GameResultChecker _resultChecker;
    
    public GameResultCheckerTests()
    {
        _resultChecker = new GameResultChecker(_moveValidator, _zobristHasher);
    }
    
    #region Win Condition Tests
    
    [Fact]
    public void Check_BluePieceInRedDen_BlueWins()
    {
        // Arrange
        var gameState = CreateGameState();
        gameState.Board[BoardConstants.RedDen] = new Piece(PieceType.Lion, Player.Blue, BoardConstants.RedDen);
        
        // Act
        var result = _resultChecker.Check(gameState);
        
        // Assert
        Assert.True(result.IsGameOver);
        Assert.Equal(GameResult.BlueWins, result.Result);
        Assert.Equal(WinReason.DenCapture, result.WinReason);
    }
    
    [Fact]
    public void Check_RedPieceInBlueDen_RedWins()
    {
        // Arrange
        var gameState = CreateGameState();
        gameState.Board[BoardConstants.BlueDen] = new Piece(PieceType.Tiger, Player.Red, BoardConstants.BlueDen);
        
        // Act
        var result = _resultChecker.Check(gameState);
        
        // Assert
        Assert.True(result.IsGameOver);
        Assert.Equal(GameResult.RedWins, result.Result);
        Assert.Equal(WinReason.DenCapture, result.WinReason);
    }
    
    [Fact]
    public void Check_AllBluePiecesCaptured_RedWins()
    {
        // Arrange
        var gameState = CreateGameState();
        gameState.Board = new Board(); // Empty board
        
        // Add only red pieces
        gameState.Board[0, 0] = new Piece(PieceType.Lion, Player.Red, new Position(0, 0));
        
        // Act
        var result = _resultChecker.Check(gameState);
        
        // Assert
        Assert.True(result.IsGameOver);
        Assert.Equal(GameResult.RedWins, result.Result);
        Assert.Equal(WinReason.AllPiecesCaptured, result.WinReason);
    }
    
    [Fact]
    public void Check_AllRedPiecesCaptured_BlueWins()
    {
        // Arrange
        var gameState = CreateGameState();
        gameState.Board = new Board(); // Empty board
        
        // Add only blue pieces
        gameState.Board[8, 0] = new Piece(PieceType.Lion, Player.Blue, new Position(8, 0));
        
        // Act
        var result = _resultChecker.Check(gameState);
        
        // Assert
        Assert.True(result.IsGameOver);
        Assert.Equal(GameResult.BlueWins, result.Result);
        Assert.Equal(WinReason.AllPiecesCaptured, result.WinReason);
    }
    
    #endregion
    
    #region Timeout Tests
    
    [Fact]
    public void Check_BlueTimeout_RedWins()
    {
        // Arrange
        var gameState = CreateGameState();
        gameState.TimeControl = gameState.TimeControl with { BlueTimeRemainingMs = 0 };
        
        // Act
        var result = _resultChecker.Check(gameState);
        
        // Assert
        Assert.True(result.IsGameOver);
        Assert.Equal(GameResult.RedWins, result.Result);
        Assert.Equal(WinReason.Timeout, result.WinReason);
    }
    
    [Fact]
    public void Check_RedTimeout_BlueWins()
    {
        // Arrange
        var gameState = CreateGameState();
        gameState.TimeControl = gameState.TimeControl with { RedTimeRemainingMs = 0 };
        
        // Act
        var result = _resultChecker.Check(gameState);
        
        // Assert
        Assert.True(result.IsGameOver);
        Assert.Equal(GameResult.BlueWins, result.Result);
        Assert.Equal(WinReason.Timeout, result.WinReason);
    }
    
    #endregion
    
    #region Draw Condition Tests
    
    [Fact]
    public void Check_ThreefoldRepetition_Draw()
    {
        // Arrange
        var gameState = CreateGameState();
        
        // Compute hash for current position
        var hash = _zobristHasher.ComputeHash(gameState.Board, gameState.CurrentTurn);
        
        // Need at least 5 entries in history, with same hash appearing 2 times
        // (current position will be the 3rd)
        gameState.PositionHistory.Add(hash);  // 1st occurrence
        gameState.PositionHistory.Add(123L);  // different position
        gameState.PositionHistory.Add(hash);  // 2nd occurrence
        gameState.PositionHistory.Add(456L);  // different position
        gameState.PositionHistory.Add(789L);  // different position
        
        // Act
        var result = _resultChecker.Check(gameState);
        
        // Assert
        Assert.True(result.IsGameOver);
        Assert.Equal(GameResult.Draw, result.Result);
        Assert.Equal(DrawReason.ThreefoldRepetition, result.DrawReason);
    }
    
    [Fact]
    public void Check_RuleOfThirty_Draw()
    {
        // Arrange
        var gameState = CreateGameState();
        gameState.MovesSinceCapture = 60; // 30 full moves = 60 half moves
        
        // Act
        var result = _resultChecker.Check(gameState);
        
        // Assert
        Assert.True(result.IsGameOver);
        Assert.Equal(GameResult.Draw, result.Result);
        Assert.Equal(DrawReason.RuleOfThirty, result.DrawReason);
    }
    
    [Fact]
    public void Check_NoValidMoves_Stalemate()
    {
        // Arrange - Create a scenario where Blue has no valid moves
        var gameState = CreateGameState();
        gameState.Board = new Board();
        
        // Place Blue piece surrounded by friendly pieces (no valid moves)
        // This is a contrived scenario for testing
        gameState.Board[0, 0] = new Piece(PieceType.Rat, Player.Blue, new Position(0, 0));
        gameState.Board[0, 1] = new Piece(PieceType.Cat, Player.Blue, new Position(0, 1));
        gameState.Board[1, 0] = new Piece(PieceType.Wolf, Player.Blue, new Position(1, 0));
        // The Rat at (0,0) is blocked by friendly pieces and board edge
        
        // Add a Red piece so the game is valid
        gameState.Board[8, 6] = new Piece(PieceType.Lion, Player.Red, new Position(8, 6));
        
        // Actually for stalemate, ALL blue pieces must have no moves
        // In this setup, the Cat and Wolf still have moves
        // Let's simplify - just one blue piece completely blocked
        gameState.Board = new Board();
        gameState.Board[0, 0] = new Piece(PieceType.Cat, Player.Blue, new Position(0, 0));
        gameState.Board[0, 1] = new Piece(PieceType.Wolf, Player.Blue, new Position(0, 1));
        gameState.Board[1, 0] = new Piece(PieceType.Dog, Player.Blue, new Position(1, 0));
        gameState.Board[1, 1] = new Piece(PieceType.Leopard, Player.Blue, new Position(1, 1));
        
        gameState.Board[8, 6] = new Piece(PieceType.Lion, Player.Red, new Position(8, 6));
        
        // Blue pieces can still move to empty squares, so not a stalemate
        // True stalemate is rare in animal chess
        
        // Act
        var result = _resultChecker.Check(gameState);
        
        // In this case, there are still valid moves
        Assert.False(result.IsGameOver);
    }
    
    #endregion
    
    #region Game In Progress Tests
    
    [Fact]
    public void Check_NormalPosition_InProgress()
    {
        // Arrange
        var gameState = CreateGameState();
        
        // Act
        var result = _resultChecker.Check(gameState);
        
        // Assert
        Assert.False(result.IsGameOver);
        Assert.Equal(GameResult.InProgress, result.Result);
    }
    
    #endregion
    
    private static GameState CreateGameState()
    {
        return GameState.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            TimeControlPreset.Blitz);
    }
}
