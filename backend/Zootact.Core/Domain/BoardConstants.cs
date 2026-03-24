namespace Zootact.Core.Domain;

/// <summary>
/// Constants defining the Zootact board layout including river, traps, and dens.
/// Board is 7 columns (0-6) × 9 rows (0-8).
/// Blue player starts at bottom (rows 6-8), Red player at top (rows 0-2).
/// </summary>
public static class BoardConstants
{
    public const int Rows = 9;
    public const int Cols = 7;
    
    /// <summary>
    /// River cells - Only Rat can enter; Lion/Tiger can jump over.
    /// The river occupies rows 3-5, columns 1-2 and 4-5.
    /// </summary>
    public static readonly HashSet<Position> RiverCells =
    [
        // Left river segment
        new Position(3, 1), new Position(3, 2),
        new Position(4, 1), new Position(4, 2),
        new Position(5, 1), new Position(5, 2),
        // Right river segment
        new Position(3, 4), new Position(3, 5),
        new Position(4, 4), new Position(4, 5),
        new Position(5, 4), new Position(5, 5)
    ];
    
    /// <summary>
    /// Blue player's traps (around Blue's den at row 8).
    /// Enemy pieces in traps have rank reduced to 0.
    /// </summary>
    public static readonly HashSet<Position> BlueTrapCells =
    [
        new Position(8, 2),  // Left trap
        new Position(7, 3),  // Top trap
        new Position(8, 4)   // Right trap
    ];
    
    /// <summary>
    /// Red player's traps (around Red's den at row 0).
    /// Enemy pieces in traps have rank reduced to 0.
    /// </summary>
    public static readonly HashSet<Position> RedTrapCells =
    [
        new Position(0, 2),  // Left trap
        new Position(1, 3),  // Bottom trap
        new Position(0, 4)   // Right trap
    ];
    
    /// <summary>
    /// All trap cells on the board.
    /// </summary>
    public static readonly HashSet<Position> AllTrapCells =
        BlueTrapCells.Union(RedTrapCells).ToHashSet();
    
    /// <summary>
    /// Blue player's den (winning position for Red).
    /// </summary>
    public static readonly Position BlueDen = new(8, 3);
    
    /// <summary>
    /// Red player's den (winning position for Blue).
    /// </summary>
    public static readonly Position RedDen = new(0, 3);
    
    /// <summary>
    /// Checks if a position is in the river.
    /// </summary>
    public static bool IsRiver(Position pos) => RiverCells.Contains(pos);
    
    /// <summary>
    /// Checks if a position is a trap cell.
    /// </summary>
    public static bool IsTrap(Position pos) => AllTrapCells.Contains(pos);
    
    /// <summary>
    /// Gets the traps belonging to a specific player.
    /// </summary>
    public static HashSet<Position> GetPlayerTraps(Player player) =>
        player == Player.Blue ? BlueTrapCells : RedTrapCells;
    
    /// <summary>
    /// Gets the den position for a specific player.
    /// </summary>
    public static Position GetPlayerDen(Player player) =>
        player == Player.Blue ? BlueDen : RedDen;
    
    /// <summary>
    /// Gets the opponent's den position (winning target).
    /// </summary>
    public static Position GetOpponentDen(Player player) =>
        player == Player.Blue ? RedDen : BlueDen;
    
    /// <summary>
    /// Checks if a position is the den of a specific player.
    /// </summary>
    public static bool IsPlayerDen(Position pos, Player player) =>
        pos == GetPlayerDen(player);
    
    /// <summary>
    /// Checks if a position is within the board boundaries.
    /// </summary>
    public static bool IsValidPosition(Position pos) =>
        pos.Row >= 0 && pos.Row < Rows && pos.Col >= 0 && pos.Col < Cols;
}
