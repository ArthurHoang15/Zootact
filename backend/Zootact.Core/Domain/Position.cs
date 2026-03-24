namespace Zootact.Core.Domain;

/// <summary>
/// Represents a position on the 7x9 board.
/// Row: 0-8 (top to bottom), Col: 0-6 (left to right).
/// </summary>
/// <param name="Row">Row index (0-8). Row 0 is Red player's back row.</param>
/// <param name="Col">Column index (0-6).</param>
public readonly record struct Position(int Row, int Col)
{
    /// <summary>
    /// Validates if the position is within the board boundaries.
    /// </summary>
    public bool IsValid => Row >= 0 && Row <= 8 && Col >= 0 && Col <= 6;

    /// <summary>
    /// Parses a position string like "0,3" or "0-3" into a Position.
    /// </summary>
    public static Position Parse(string value)
    {
        var parts = value.Split(',', '-');
        if (parts.Length != 2)
            throw new ArgumentException($"Invalid position format: {value}");
        
        return new Position(int.Parse(parts[0]), int.Parse(parts[1]));
    }

    /// <summary>
    /// Tries to parse a position string.
    /// </summary>
    public static bool TryParse(string value, out Position position)
    {
        position = default;
        
        var parts = value.Split(',', '-');
        if (parts.Length != 2)
            return false;
        
        if (!int.TryParse(parts[0], out var row) || !int.TryParse(parts[1], out var col))
            return false;
        
        position = new Position(row, col);
        return position.IsValid;
    }

    public override string ToString() => $"{Row},{Col}";
}
