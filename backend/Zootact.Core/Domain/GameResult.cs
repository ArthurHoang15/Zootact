namespace Zootact.Core.Domain;

/// <summary>
/// The result of a completed game.
/// </summary>
public enum GameResult
{
    InProgress,
    BlueWins,
    RedWins,
    Draw
}

/// <summary>
/// The reason for a draw.
/// </summary>
public enum DrawReason
{
    ThreefoldRepetition,
    RuleOfThirty,
    Stalemate,
    Agreement
}

/// <summary>
/// The reason for a win.
/// </summary>
public enum WinReason
{
    DenCapture,         // Entered opponent's den
    AllPiecesCaptured,  // Captured all opponent pieces
    Timeout,            // Opponent ran out of time
    Resignation,        // Opponent resigned
    Abandonment         // Opponent disconnected and didn't return
}

/// <summary>
/// Match status for persistence.
/// </summary>
public enum MatchStatus
{
    InProgress,
    Completed,
    Abandoned
}
