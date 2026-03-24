using Zootact.Core.Domain;

namespace Zootact.Core.GameLogic;

/// <summary>
/// Checks for game-ending conditions: wins, draws, stalemates.
/// </summary>
public sealed class GameResultChecker(IMoveValidator moveValidator, ZobristHasher zobristHasher)
{
    /// <summary>
    /// Checks the current game state for win/draw conditions.
    /// </summary>
    /// <param name="gameState">The current game state.</param>
    /// <returns>Result check with game result and reason.</returns>
    public GameResultCheck Check(GameState gameState)
    {
        // 1. Check if a player has entered the opponent's den
        var denWin = CheckDenCapture(gameState.Board);
        if (denWin is not null)
            return denWin;
        
        // 2. Check if all pieces of one player are captured
        var eliminationWin = CheckElimination(gameState.Board);
        if (eliminationWin is not null)
            return eliminationWin;
        
        // 3. Check for timeout
        var timeoutWin = CheckTimeout(gameState);
        if (timeoutWin is not null)
            return timeoutWin;
        
        // 4. Check for threefold repetition
        var repetitionDraw = CheckThreefoldRepetition(gameState);
        if (repetitionDraw is not null)
            return repetitionDraw;
        
        // 5. Check for Rule of 30 (60 half-moves without capture)
        var ruleOf30Draw = CheckRuleOfThirty(gameState);
        if (ruleOf30Draw is not null)
            return ruleOf30Draw;
        
        // 6. Check for stalemate (no valid moves)
        var stalemate = CheckStalemate(gameState);
        if (stalemate is not null)
            return stalemate;
        
        // Game continues
        return new GameResultCheck(GameResult.InProgress);
    }
    
    /// <summary>
    /// Checks if any player has entered the opponent's den.
    /// </summary>
    private GameResultCheck? CheckDenCapture(Board board)
    {
        // Check Blue den - if Red piece is there, Red wins
        var blueDen = BoardConstants.BlueDen;
        var pieceInBlueDen = board[blueDen];
        if (pieceInBlueDen is not null && pieceInBlueDen.Owner == Player.Red)
        {
            return new GameResultCheck(GameResult.RedWins, WinReason: WinReason.DenCapture);
        }
        
        // Check Red den - if Blue piece is there, Blue wins
        var redDen = BoardConstants.RedDen;
        var pieceInRedDen = board[redDen];
        if (pieceInRedDen is not null && pieceInRedDen.Owner == Player.Blue)
        {
            return new GameResultCheck(GameResult.BlueWins, WinReason: WinReason.DenCapture);
        }
        
        return null;
    }
    
    /// <summary>
    /// Checks if all pieces of one player have been captured.
    /// </summary>
    private GameResultCheck? CheckElimination(Board board)
    {
        var bluePieces = board.CountPlayerPieces(Player.Blue);
        var redPieces = board.CountPlayerPieces(Player.Red);
        
        if (bluePieces == 0)
            return new GameResultCheck(GameResult.RedWins, WinReason: WinReason.AllPiecesCaptured);
        
        if (redPieces == 0)
            return new GameResultCheck(GameResult.BlueWins, WinReason: WinReason.AllPiecesCaptured);
        
        return null;
    }
    
    /// <summary>
    /// Checks if either player has timed out.
    /// </summary>
    private GameResultCheck? CheckTimeout(GameState gameState)
    {
        if (gameState.TimeControl.IsTimeout(Player.Blue))
            return new GameResultCheck(GameResult.RedWins, WinReason: WinReason.Timeout);
        
        if (gameState.TimeControl.IsTimeout(Player.Red))
            return new GameResultCheck(GameResult.BlueWins, WinReason: WinReason.Timeout);
        
        return null;
    }
    
    /// <summary>
    /// Checks for threefold repetition (same position appearing 3 times).
    /// </summary>
    private GameResultCheck? CheckThreefoldRepetition(GameState gameState)
    {
        if (gameState.PositionHistory.Count < 5) // Need at least 5 positions for repetition
            return null;
        
        // Count occurrences of the current position
        var currentHash = zobristHasher.ComputeHash(gameState.Board, gameState.CurrentTurn);
        var count = gameState.PositionHistory.Count(h => h == currentHash);
        
        // Include the current position (which may not be in history yet)
        // The position is added after a move, so if we're checking before adding,
        // we need to count occurrences + 1
        if (count >= 2) // Already appeared twice + current = 3
        {
            return new GameResultCheck(GameResult.Draw, DrawReason: DrawReason.ThreefoldRepetition);
        }
        
        return null;
    }
    
    /// <summary>
    /// Checks for Rule of 30 draw (30 full moves = 60 half-moves without capture).
    /// </summary>
    private GameResultCheck? CheckRuleOfThirty(GameState gameState)
    {
        if (gameState.MovesSinceCapture >= 60) // 30 full moves = 60 plies
        {
            return new GameResultCheck(GameResult.Draw, DrawReason: DrawReason.RuleOfThirty);
        }
        
        return null;
    }
    
    /// <summary>
    /// Checks if the current player has no valid moves (stalemate).
    /// </summary>
    private GameResultCheck? CheckStalemate(GameState gameState)
    {
        var validMoves = moveValidator.GetAllValidMoves(gameState.Board, gameState.CurrentTurn);
        
        if (!validMoves.Any())
        {
            // No valid moves - stalemate is a draw
            return new GameResultCheck(GameResult.Draw, DrawReason: DrawReason.Stalemate);
        }
        
        return null;
    }
}

/// <summary>
/// Result of a game state check.
/// </summary>
/// <param name="Result">The game result.</param>
/// <param name="WinReason">Reason for the win, if applicable.</param>
/// <param name="DrawReason">Reason for the draw, if applicable.</param>
public record GameResultCheck(
    GameResult Result,
    WinReason? WinReason = null,
    DrawReason? DrawReason = null)
{
    /// <summary>
    /// Whether the game is still in progress.
    /// </summary>
    public bool IsGameOver => Result != GameResult.InProgress;
    
    /// <summary>
    /// Gets the winner (Blue/Red) or null if in progress or draw.
    /// </summary>
    public Player? Winner => Result switch
    {
        GameResult.BlueWins => Player.Blue,
        GameResult.RedWins => Player.Red,
        _ => null
    };
}
