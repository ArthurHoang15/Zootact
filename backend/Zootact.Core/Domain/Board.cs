using System.Text.Json;

namespace Zootact.Core.Domain;

/// <summary>
/// Represents the 7×9 Zootact game board.
/// Row 0 is Red's back row, Row 8 is Blue's back row.
/// </summary>
public sealed class Board
{
    private readonly Piece?[,] _grid = new Piece?[BoardConstants.Rows, BoardConstants.Cols];
    
    /// <summary>
    /// Creates an empty board.
    /// </summary>
    public Board() { }
    
    /// <summary>
    /// Gets or sets a piece at a specific position.
    /// </summary>
    public Piece? this[Position pos]
    {
        get => IsValidPosition(pos) ? _grid[pos.Row, pos.Col] : null;
        set
        {
            if (IsValidPosition(pos))
                _grid[pos.Row, pos.Col] = value;
        }
    }
    
    /// <summary>
    /// Gets or sets a piece at specific row and column.
    /// </summary>
    public Piece? this[int row, int col]
    {
        get => IsValidPosition(row, col) ? _grid[row, col] : null;
        set
        {
            if (IsValidPosition(row, col))
                _grid[row, col] = value;
        }
    }
    
    /// <summary>
    /// Checks if a position is within board boundaries.
    /// </summary>
    public static bool IsValidPosition(Position pos) =>
        pos.Row >= 0 && pos.Row < BoardConstants.Rows &&
        pos.Col >= 0 && pos.Col < BoardConstants.Cols;
    
    /// <summary>
    /// Checks if a position is within board boundaries.
    /// </summary>
    public static bool IsValidPosition(int row, int col) =>
        row >= 0 && row < BoardConstants.Rows &&
        col >= 0 && col < BoardConstants.Cols;
    
    /// <summary>
    /// Gets all pieces for a specific player.
    /// </summary>
    public IEnumerable<Piece> GetPlayerPieces(Player player)
    {
        for (var row = 0; row < BoardConstants.Rows; row++)
        {
            for (var col = 0; col < BoardConstants.Cols; col++)
            {
                var piece = _grid[row, col];
                if (piece?.Owner == player)
                    yield return piece;
            }
        }
    }
    
    /// <summary>
    /// Gets all pieces on the board.
    /// </summary>
    public IEnumerable<Piece> GetAllPieces()
    {
        for (var row = 0; row < BoardConstants.Rows; row++)
        {
            for (var col = 0; col < BoardConstants.Cols; col++)
            {
                var piece = _grid[row, col];
                if (piece is not null)
                    yield return piece;
            }
        }
    }
    
    /// <summary>
    /// Counts pieces for a specific player.
    /// </summary>
    public int CountPlayerPieces(Player player) => GetPlayerPieces(player).Count();
    
    /// <summary>
    /// Creates a deep copy of the board.
    /// </summary>
    public Board Clone()
    {
        var clone = new Board();
        for (var row = 0; row < BoardConstants.Rows; row++)
        {
            for (var col = 0; col < BoardConstants.Cols; col++)
            {
                clone._grid[row, col] = _grid[row, col];
            }
        }
        return clone;
    }
    
    /// <summary>
    /// Creates a new board with the initial piece layout.
    /// </summary>
    public static Board CreateInitialBoard()
    {
        var board = new Board();
        
        // Red pieces (top, rows 0-2)
        // Row 0: Lion and Tiger at corners
        board[0, 0] = new Piece(PieceType.Lion, Player.Red, new Position(0, 0));
        board[0, 6] = new Piece(PieceType.Tiger, Player.Red, new Position(0, 6));
        
        // Row 1: Dog and Cat
        board[1, 1] = new Piece(PieceType.Dog, Player.Red, new Position(1, 1));
        board[1, 5] = new Piece(PieceType.Cat, Player.Red, new Position(1, 5));
        
        // Row 2: Rat, Leopard, Wolf, Elephant
        board[2, 0] = new Piece(PieceType.Rat, Player.Red, new Position(2, 0));
        board[2, 2] = new Piece(PieceType.Leopard, Player.Red, new Position(2, 2));
        board[2, 4] = new Piece(PieceType.Wolf, Player.Red, new Position(2, 4));
        board[2, 6] = new Piece(PieceType.Elephant, Player.Red, new Position(2, 6));
        
        // Blue pieces (bottom, rows 6-8) - Mirrored layout
        // Row 6: Elephant, Wolf, Leopard, Rat
        board[6, 0] = new Piece(PieceType.Elephant, Player.Blue, new Position(6, 0));
        board[6, 2] = new Piece(PieceType.Wolf, Player.Blue, new Position(6, 2));
        board[6, 4] = new Piece(PieceType.Leopard, Player.Blue, new Position(6, 4));
        board[6, 6] = new Piece(PieceType.Rat, Player.Blue, new Position(6, 6));
        
        // Row 7: Cat and Dog
        board[7, 1] = new Piece(PieceType.Cat, Player.Blue, new Position(7, 1));
        board[7, 5] = new Piece(PieceType.Dog, Player.Blue, new Position(7, 5));
        
        // Row 8: Tiger and Lion at corners
        board[8, 0] = new Piece(PieceType.Tiger, Player.Blue, new Position(8, 0));
        board[8, 6] = new Piece(PieceType.Lion, Player.Blue, new Position(8, 6));
        
        return board;
    }
    
    /// <summary>
    /// Serializes the board to a JSON string for Redis storage.
    /// </summary>
    public string ToJson()
    {
        var cells = new string?[BoardConstants.Rows][];

        for (var row = 0; row < BoardConstants.Rows; row++)
        {
            cells[row] = new string?[BoardConstants.Cols];
        }
        
        for (var row = 0; row < BoardConstants.Rows; row++)
        {
            for (var col = 0; col < BoardConstants.Cols; col++)
            {
                var piece = _grid[row, col];
                if (piece is not null)
                {
                    // Format: "B_Lion" or "R_Rat"
                    var prefix = piece.Owner == Player.Blue ? "B" : "R";
                    cells[row][col] = $"{prefix}_{piece.Type}";
                }
            }
        }
        
        return JsonSerializer.Serialize(cells);
    }
    
    /// <summary>
    /// Deserializes a board from a JSON string.
    /// </summary>
    public static Board FromJson(string json)
    {
        var cells = JsonSerializer.Deserialize<string?[][]>(json) 
            ?? throw new ArgumentException("Invalid board JSON");
        
        var board = new Board();
        
        for (var row = 0; row < BoardConstants.Rows; row++)
        {
            if (row >= cells.Length || cells[row] is null)
            {
                continue;
            }

            for (var col = 0; col < BoardConstants.Cols; col++)
            {
                if (col >= cells[row].Length)
                {
                    continue;
                }

                var cell = cells[row][col];
                if (string.IsNullOrEmpty(cell)) continue;
                
                var parts = cell.Split('_');
                if (parts.Length != 2) continue;
                
                var owner = parts[0] == "B" ? Player.Blue : Player.Red;
                if (Enum.TryParse<PieceType>(parts[1], out var pieceType))
                {
                    board[row, col] = new Piece(pieceType, owner, new Position(row, col));
                }
            }
        }
        
        return board;
    }
}
