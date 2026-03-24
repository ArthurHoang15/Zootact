using Xunit;
using Zootact.Core.Domain;
using Zootact.Core.GameLogic;

namespace Zootact.Tests.GameLogic;

/// <summary>
/// Tests for MoveValidator - the core game rules.
/// </summary>
public class MoveValidatorTests
{
    private readonly MoveValidator _validator = new();
    
    #region Basic Movement Tests
    
    [Fact]
    public void ValidateMove_AdjacentMove_IsValid()
    {
        // Arrange
        var board = Board.CreateInitialBoard();
        var from = new Position(6, 0); // Blue Elephant
        var to = new Position(5, 0);   // Move up
        
        // Act
        var result = _validator.ValidateMove(board, from, to, Player.Blue);
        
        // Assert
        Assert.True(result.IsValid);
    }
    
    [Fact]
    public void ValidateMove_DiagonalMove_IsInvalid()
    {
        // Arrange
        var board = Board.CreateInitialBoard();
        var from = new Position(6, 0); // Blue Elephant
        var to = new Position(5, 1);   // Diagonal move
        
        // Act
        var result = _validator.ValidateMove(board, from, to, Player.Blue);
        
        // Assert
        Assert.False(result.IsValid);
    }
    
    [Fact]
    public void ValidateMove_MovingOpponentPiece_IsInvalid()
    {
        // Arrange
        var board = Board.CreateInitialBoard();
        var from = new Position(2, 6); // Red Elephant
        var to = new Position(3, 6);   // Move down
        
        // Act
        var result = _validator.ValidateMove(board, from, to, Player.Blue);
        
        // Assert
        Assert.False(result.IsValid);
        Assert.Equal("NotYourPiece", result.ErrorCode);
    }
    
    #endregion
    
    #region River Tests - Rat
    
    [Fact]
    public void ValidateMove_RatEntersRiver_IsValid()
    {
        // Arrange
        var board = new Board();
        var ratPosition = new Position(3, 0); // Adjacent to river
        board[ratPosition] = new Piece(PieceType.Rat, Player.Blue, ratPosition);
        
        var to = new Position(3, 1); // River cell
        
        // Act
        var result = _validator.ValidateMove(board, ratPosition, to, Player.Blue);
        
        // Assert
        Assert.True(result.IsValid);
    }
    
    [Fact]
    public void ValidateMove_NonRatEntersRiver_IsInvalid()
    {
        // Arrange
        var board = new Board();
        var catPosition = new Position(3, 0);
        board[catPosition] = new Piece(PieceType.Cat, Player.Blue, catPosition);
        
        var to = new Position(3, 1); // River cell
        
        // Act
        var result = _validator.ValidateMove(board, catPosition, to, Player.Blue);
        
        // Assert
        Assert.False(result.IsValid);
        Assert.Equal("CannotEnterRiver", result.ErrorCode);
    }
    
    [Fact]
    public void ValidateMove_RatSwimsInRiver_IsValid()
    {
        // Arrange
        var board = new Board();
        var ratPosition = new Position(3, 1); // In river
        board[ratPosition] = new Piece(PieceType.Rat, Player.Blue, ratPosition);
        
        var to = new Position(4, 1); // Another river cell
        
        // Act
        var result = _validator.ValidateMove(board, ratPosition, to, Player.Blue);
        
        // Assert
        Assert.True(result.IsValid);
    }
    
    #endregion
    
    #region Rat-Elephant Special Rules
    
    [Fact]
    public void ValidateMove_RatCapturesElephantFromLand_IsValid()
    {
        // Arrange
        var board = new Board();
        var ratPosition = new Position(4, 0);
        var elephantPosition = new Position(4, 1); // Not in river for this test
        
        // Move elephant to a land position
        board[ratPosition] = new Piece(PieceType.Rat, Player.Blue, ratPosition);
        board[elephantPosition] = new Piece(PieceType.Elephant, Player.Red, elephantPosition);
        
        // Since (4,1) is actually a river, let's use different positions
        var landRatPos = new Position(5, 0);
        var landElephantPos = new Position(6, 0);
        board[5, 0] = new Piece(PieceType.Rat, Player.Blue, landRatPos);
        board[6, 0] = new Piece(PieceType.Elephant, Player.Red, landElephantPos);
        
        // Act
        var result = _validator.ValidateMove(board, landRatPos, landElephantPos, Player.Blue);
        
        // Assert
        Assert.True(result.IsValid);
    }
    
    [Fact]
    public void ValidateMove_RatCapturesElephantFromRiver_IsInvalid()
    {
        // Arrange
        var board = new Board();
        var ratPosition = new Position(3, 1); // In river
        var elephantPosition = new Position(3, 0); // On land
        
        board[ratPosition] = new Piece(PieceType.Rat, Player.Blue, ratPosition);
        board[elephantPosition] = new Piece(PieceType.Elephant, Player.Red, elephantPosition);
        
        // Act
        var result = _validator.ValidateMove(board, ratPosition, elephantPosition, Player.Blue);
        
        // Assert
        Assert.False(result.IsValid);
        Assert.Equal("RatFromRiver", result.ErrorCode);
    }
    
    [Fact]
    public void ValidateMove_ElephantCapturesRat_IsInvalid()
    {
        // Arrange
        var board = new Board();
        var elephantPosition = new Position(4, 0);
        var ratPosition = new Position(5, 0);
        
        board[elephantPosition] = new Piece(PieceType.Elephant, Player.Blue, elephantPosition);
        board[ratPosition] = new Piece(PieceType.Rat, Player.Red, ratPosition);
        
        // Act
        var result = _validator.ValidateMove(board, elephantPosition, ratPosition, Player.Blue);
        
        // Assert
        Assert.False(result.IsValid);
        Assert.Equal("ElephantVsRat", result.ErrorCode);
    }
    
    #endregion
    
    #region Lion/Tiger Jump Tests
    
    [Fact]
    public void ValidateMove_LionJumpsRiverVertically_IsValid()
    {
        // Arrange
        var board = new Board();
        var lionPosition = new Position(2, 1); // Above river
        var targetPosition = new Position(6, 1); // Below river
        
        board[lionPosition] = new Piece(PieceType.Lion, Player.Blue, lionPosition);
        
        // Act
        var result = _validator.ValidateMove(board, lionPosition, targetPosition, Player.Blue);
        
        // Assert
        Assert.True(result.IsValid);
    }
    
    [Fact]
    public void ValidateMove_TigerJumpsRiverHorizontally_IsValid()
    {
        // Arrange
        var board = new Board();
        var tigerPosition = new Position(4, 0); // Left of river
        var targetPosition = new Position(4, 3); // Right of river (middle land)
        
        board[tigerPosition] = new Piece(PieceType.Tiger, Player.Blue, tigerPosition);
        
        // Act
        var result = _validator.ValidateMove(board, tigerPosition, targetPosition, Player.Blue);
        
        // Assert
        Assert.True(result.IsValid);
    }
    
    [Fact]
    public void ValidateMove_LionJumpBlockedByRat_IsInvalid()
    {
        // Arrange
        var board = new Board();
        var lionPosition = new Position(2, 1); // Above river
        var targetPosition = new Position(6, 1); // Below river
        var ratPosition = new Position(4, 1); // In river path
        
        board[lionPosition] = new Piece(PieceType.Lion, Player.Blue, lionPosition);
        board[ratPosition] = new Piece(PieceType.Rat, Player.Red, ratPosition);
        
        // Act
        var result = _validator.ValidateMove(board, lionPosition, targetPosition, Player.Blue);
        
        // Assert
        Assert.False(result.IsValid);
        Assert.Equal("JumpBlocked", result.ErrorCode);
    }
    
    [Fact]
    public void ValidateMove_CatCannotJumpRiver_IsInvalid()
    {
        // Arrange
        var board = new Board();
        var catPosition = new Position(2, 1);
        var targetPosition = new Position(6, 1);
        
        board[catPosition] = new Piece(PieceType.Cat, Player.Blue, catPosition);
        
        // Act
        var result = _validator.ValidateMove(board, catPosition, targetPosition, Player.Blue);
        
        // Assert
        Assert.False(result.IsValid);
    }
    
    #endregion
    
    #region Trap Tests
    
    [Fact]
    public void ValidateMove_WeakPieceCapturesEnemyInTrap_IsValid()
    {
        // Arrange - Blue Rat captures Red Elephant in Red's trap (Blue attacks into Red territory)
        var board = new Board();
        var ratPosition = new Position(1, 2); // Adjacent to Red trap
        var trapPosition = new Position(0, 2); // Red's trap
        
        board[ratPosition] = new Piece(PieceType.Rat, Player.Blue, ratPosition);
        board[trapPosition] = new Piece(PieceType.Elephant, Player.Red, trapPosition);
        
        // Act - Rat (rank 1) captures Elephant (rank 8 reduced to 0 in trap)
        var result = _validator.ValidateMove(board, ratPosition, trapPosition, Player.Blue);
        
        // Assert
        Assert.True(result.IsValid);
    }
    
    #endregion
    
    #region Den Entry Tests
    
    [Fact]
    public void ValidateMove_EnterOwnDen_IsInvalid()
    {
        // Arrange
        var board = new Board();
        var piecePosition = new Position(7, 3); // Adjacent to Blue den
        board[piecePosition] = new Piece(PieceType.Lion, Player.Blue, piecePosition);
        
        var blueDen = BoardConstants.BlueDen; // (8, 3)
        
        // Act
        var result = _validator.ValidateMove(board, piecePosition, blueDen, Player.Blue);
        
        // Assert
        Assert.False(result.IsValid);
        Assert.Equal("OwnDenEntry", result.ErrorCode);
    }
    
    [Fact]
    public void ValidateMove_EnterOpponentDen_IsValid()
    {
        // Arrange
        var board = new Board();
        var piecePosition = new Position(1, 3); // Adjacent to Red den
        board[piecePosition] = new Piece(PieceType.Lion, Player.Blue, piecePosition);
        
        var redDen = BoardConstants.RedDen; // (0, 3)
        
        // Act
        var result = _validator.ValidateMove(board, piecePosition, redDen, Player.Blue);
        
        // Assert
        Assert.True(result.IsValid);
    }
    
    #endregion
    
    #region Rank Capture Tests
    
    [Fact]
    public void ValidateMove_HigherRankCapturesLower_IsValid()
    {
        // Arrange
        var board = new Board();
        var lionPosition = new Position(4, 0);
        var catPosition = new Position(5, 0);
        
        board[lionPosition] = new Piece(PieceType.Lion, Player.Blue, lionPosition);
        board[catPosition] = new Piece(PieceType.Cat, Player.Red, catPosition);
        
        // Act
        var result = _validator.ValidateMove(board, lionPosition, catPosition, Player.Blue);
        
        // Assert
        Assert.True(result.IsValid);
    }
    
    [Fact]
    public void ValidateMove_LowerRankCapturesHigher_IsInvalid()
    {
        // Arrange
        var board = new Board();
        var catPosition = new Position(4, 0);
        var lionPosition = new Position(5, 0);
        
        board[catPosition] = new Piece(PieceType.Cat, Player.Blue, catPosition);
        board[lionPosition] = new Piece(PieceType.Lion, Player.Red, lionPosition);
        
        // Act
        var result = _validator.ValidateMove(board, catPosition, lionPosition, Player.Blue);
        
        // Assert
        Assert.False(result.IsValid);
        Assert.Equal("RankTooLow", result.ErrorCode);
    }
    
    [Fact]
    public void ValidateMove_SameRankCapture_IsValid()
    {
        // Arrange
        var board = new Board();
        var lion1Position = new Position(4, 0);
        var lion2Position = new Position(5, 0);
        
        board[lion1Position] = new Piece(PieceType.Lion, Player.Blue, lion1Position);
        board[lion2Position] = new Piece(PieceType.Lion, Player.Red, lion2Position);
        
        // Act
        var result = _validator.ValidateMove(board, lion1Position, lion2Position, Player.Blue);
        
        // Assert
        Assert.True(result.IsValid);
    }
    
    #endregion
}
