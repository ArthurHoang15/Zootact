using Zootact.Core.Domain;

namespace Zootact.Core.GameLogic;

/// <summary>
/// Main game engine that processes moves and manages game state transitions.
/// </summary>
public sealed class GameEngine(
    IMoveValidator moveValidator,
    ZobristHasher zobristHasher,
    GameResultChecker resultChecker)
{
    /// <summary>
    /// Attempts to make a move in the game.
    /// </summary>
    /// <param name="gameState">Current game state.</param>
    /// <param name="playerId">User ID of the player making the move.</param>
    /// <param name="from">Starting position.</param>
    /// <param name="to">Target position.</param>
    /// <returns>Result of the move attempt.</returns>
    public MoveResult MakeMove(GameState gameState, Guid playerId, Position from, Position to)
    {
        // 1. Verify it's this player's turn
        var playerColor = gameState.GetPlayerColor(playerId);
        if (playerColor is null)
            return MoveResult.Failure("NotInGame", "You are not a participant in this game.");
        
        if (playerColor != gameState.CurrentTurn)
            return MoveResult.Failure("NotYourTurn", "It is not your turn.");
        
        // 2. Check if game is still in progress
        if (gameState.Result != GameResult.InProgress)
            return MoveResult.Failure("GameEnded", "The game has already ended.");
        
        // 3. Calculate time elapsed since last move
        var now = DateTimeOffset.UtcNow;
        var elapsed = (long)(now - gameState.TimeControl.LastMoveTimestamp).TotalMilliseconds;
        
        // 4. Deduct time (before validation, as time is consumed regardless)
        var updatedTimeControl = gameState.TimeControl.DeductTime(playerColor.Value, elapsed);
        
        // 5. Check for timeout
        if (updatedTimeControl.IsTimeout(playerColor.Value))
        {
            gameState.TimeControl = updatedTimeControl;
            gameState.Result = playerColor == Player.Blue ? GameResult.RedWins : GameResult.BlueWins;
            gameState.ResultReason = WinReason.Timeout.ToString();
            gameState.Status = MatchStatus.Completed;
            return MoveResult.Timeout(gameState.Result);
        }
        
        gameState.TimeControl = updatedTimeControl;
        
        // 6. Validate the move
        var validation = moveValidator.ValidateMove(gameState.Board, from, to, playerColor.Value);
        if (!validation.IsValid)
            return MoveResult.Failure(validation.ErrorCode!, validation.ErrorMessage!);
        
        // 7. Get the piece being moved and any captured piece
        var piece = gameState.Board[from]!;
        var capturedPiece = gameState.Board[to];
        
        // 8. Execute the move on the board
        gameState.Board[to] = piece.MoveTo(to);
        gameState.Board[from] = null;
        
        // 9. Update move counters
        gameState.MoveCount++;
        
        if (capturedPiece is not null)
        {
            gameState.MovesSinceCapture = 0;
        }
        else
        {
            gameState.MovesSinceCapture++;
        }
        
        // 10. Update position history for repetition detection
        var newHash = zobristHasher.ComputeHash(gameState.Board, gameState.GetOpponent());
        gameState.PositionHistory.Add(newHash);
        
        // 11. Record the move in notation
        var moveNotation = $"{from}->{to}";
        if (capturedPiece is not null)
            moveNotation += $"x{capturedPiece.Type}";
        gameState.MoveHistory.Add(moveNotation);
        
        // 12. Switch turns
        gameState.SwitchTurn();
        
        // 13. Check for game-ending conditions
        var resultCheck = resultChecker.Check(gameState);
        if (resultCheck.IsGameOver)
        {
            gameState.Result = resultCheck.Result;
            gameState.ResultReason = resultCheck.WinReason?.ToString() ?? resultCheck.DrawReason?.ToString();
            gameState.Status = MatchStatus.Completed;
        }
        
        // 14. Create the move record
        var move = Move.Create(from, to, piece.Type, capturedPiece?.Type);
        
        return MoveResult.Success(move, resultCheck, gameState.Board.Clone(), elapsed, newHash, piece.Owner);
    }
    
    /// <summary>
    /// Handles player resignation.
    /// </summary>
    public void Resign(GameState gameState, Guid playerId)
    {
        var playerColor = gameState.GetPlayerColor(playerId);
        if (playerColor is null || gameState.Result != GameResult.InProgress)
            return;
        
        gameState.Result = playerColor == Player.Blue ? GameResult.RedWins : GameResult.BlueWins;
        gameState.ResultReason = WinReason.Resignation.ToString();
        gameState.Status = MatchStatus.Completed;
    }
    
    /// <summary>
    /// Handles draw by agreement.
    /// </summary>
    public void AcceptDraw(GameState gameState)
    {
        if (gameState.Result != GameResult.InProgress)
            return;
        
        gameState.Result = GameResult.Draw;
        gameState.ResultReason = DrawReason.Agreement.ToString();
        gameState.Status = MatchStatus.Completed;
    }
}

/// <summary>
/// Result of a move attempt.
/// </summary>
public record MoveResult
{
    public bool IsSuccess { get; init; }
    public string? ErrorCode { get; init; }
    public string? ErrorMessage { get; init; }
    public Move? Move { get; init; }
    public GameResultCheck? GameCheck { get; init; }
    public Board? BoardAfter { get; init; }
    public bool IsTimeout { get; init; }
    public long TimeSpentMs { get; init; }
    public long PositionHash { get; init; }
    public Player MovedBy { get; init; }
    
    public static MoveResult Success(Move move, GameResultCheck gameCheck, Board boardAfter, long timeSpentMs, long positionHash, Player movedBy) => new()
    {
        IsSuccess = true,
        Move = move,
        GameCheck = gameCheck,
        BoardAfter = boardAfter,
        TimeSpentMs = timeSpentMs,
        PositionHash = positionHash,
        MovedBy = movedBy
    };
    
    public static MoveResult Failure(string errorCode, string message) => new()
    {
        IsSuccess = false,
        ErrorCode = errorCode,
        ErrorMessage = message
    };
    
    public static MoveResult Timeout(GameResult result) => new()
    {
        IsSuccess = false,
        IsTimeout = true,
        ErrorCode = "Timeout",
        ErrorMessage = "Time ran out.",
        GameCheck = new GameResultCheck(result, WinReason: WinReason.Timeout)
    };
}
