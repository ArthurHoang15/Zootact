using Zootact.Core.Domain;

namespace Zootact.Core.GameLogic;

/// <summary>
/// Validates moves according to Zootact (Animal Chess) rules.
/// Implements all special rules: River, Traps, Jumping, Capture hierarchy.
/// </summary>
public sealed class MoveValidator : IMoveValidator
{
    /// <summary>
    /// Adjacent directions: up, down, left, right.
    /// </summary>
    private static readonly (int Row, int Col)[] Directions = [(-1, 0), (1, 0), (0, -1), (0, 1)];

    /// <inheritdoc />
    public MoveValidationResult ValidateMove(Board board, Position from, Position to, Player player)
    {
        // 1. Basic position validation
        if (!Board.IsValidPosition(from))
            return MoveValidationResult.Invalid("InvalidPosition", "Starting position is outside the board.");
        
        if (!Board.IsValidPosition(to))
            return MoveValidationResult.Invalid("InvalidPosition", "Target position is outside the board.");
        
        // 2. Check if there's a piece at the starting position
        var piece = board[from];
        if (piece is null)
            return MoveValidationResult.Invalid("NoPieceAtPosition", "No piece at the starting position.");
        
        // 3. Check if the piece belongs to the player
        if (piece.Owner != player)
            return MoveValidationResult.Invalid("NotYourPiece", "This piece does not belong to you.");
        
        // 4. Cannot stay in place
        if (from == to)
            return MoveValidationResult.Invalid("SamePosition", "Cannot move to the same position.");
        
        // 5. Cannot enter your own den
        if (to == BoardConstants.GetPlayerDen(player))
            return MoveValidationResult.Invalid("OwnDenEntry", "Cannot enter your own den.");
        
        // 6. Check movement rules based on piece type
        var moveType = GetMoveType(from, to, piece.Type);
        
        if (moveType == MoveType.Invalid)
            return MoveValidationResult.Invalid("InvalidMove", "This piece cannot move to that position.");
        
        // 7. Validate specific move types
        var moveValidation = moveType switch
        {
            MoveType.Adjacent => ValidateAdjacentMove(board, from, to, piece),
            MoveType.RiverJump => ValidateRiverJump(board, from, to, piece),
            MoveType.RiverEnter => ValidateRiverEnter(board, from, to, piece),
            MoveType.RiverSwim => ValidateRiverSwim(board, from, to, piece),
            MoveType.RiverExit => ValidateRiverExit(board, from, to, piece),
            _ => MoveValidationResult.Invalid("InvalidMove", "Unknown move type.")
        };
        
        if (!moveValidation.IsValid)
            return moveValidation;
        
        // 8. Validate capture if target has a piece
        var targetPiece = board[to];
        if (targetPiece is not null)
        {
            var captureValidation = ValidateCapture(board, from, to, piece, targetPiece);
            if (!captureValidation.IsValid)
                return captureValidation;
        }
        
        return MoveValidationResult.Valid();
    }
    
    /// <inheritdoc />
    public IEnumerable<Position> GetValidMoves(Board board, Position from, Player player)
    {
        var piece = board[from];
        if (piece is null || piece.Owner != player)
            yield break;
        
        // Check all possible target positions
        for (var row = 0; row < BoardConstants.Rows; row++)
        {
            for (var col = 0; col < BoardConstants.Cols; col++)
            {
                var to = new Position(row, col);
                var result = ValidateMove(board, from, to, player);
                if (result.IsValid)
                    yield return to;
            }
        }
    }
    
    /// <inheritdoc />
    public IEnumerable<(Position From, Position To)> GetAllValidMoves(Board board, Player player)
    {
        foreach (var piece in board.GetPlayerPieces(player))
        {
            foreach (var to in GetValidMoves(board, piece.Position, player))
            {
                yield return (piece.Position, to);
            }
        }
    }
    
    /// <inheritdoc />
    public bool IsCapture(Board board, Position from, Position to, Player player)
    {
        var targetPiece = board[to];
        return targetPiece is not null && targetPiece.Owner != player;
    }
    
    /// <summary>
    /// Determines the type of move based on positions and piece type.
    /// </summary>
    private MoveType GetMoveType(Position from, Position to, PieceType pieceType)
    {
        var rowDiff = Math.Abs(to.Row - from.Row);
        var colDiff = Math.Abs(to.Col - from.Col);
        
        var isFromRiver = BoardConstants.IsRiver(from);
        var isToRiver = BoardConstants.IsRiver(to);
        
        // Adjacent move (one step orthogonally)
        if ((rowDiff == 1 && colDiff == 0) || (rowDiff == 0 && colDiff == 1))
        {
            if (!isFromRiver && !isToRiver)
                return MoveType.Adjacent;
            if (!isFromRiver && isToRiver)
                return MoveType.RiverEnter;
            if (isFromRiver && !isToRiver)
                return MoveType.RiverExit;
            if (isFromRiver && isToRiver)
                return MoveType.RiverSwim;
        }
        
        // River jump (Lion/Tiger only) - must cross the entire river segment
        if (CanJumpRiver(pieceType))
        {
            if (IsValidRiverJump(from, to, out _))
                return MoveType.RiverJump;
        }
        
        return MoveType.Invalid;
    }
    
    /// <summary>
    /// Validates an adjacent (1-step) move.
    /// </summary>
    private MoveValidationResult ValidateAdjacentMove(Board board, Position from, Position to, Piece piece)
    {
        // Adjacent moves on land are always structurally valid
        // (capture validation is done separately)
        return MoveValidationResult.Valid();
    }
    
    /// <summary>
    /// Validates entering the river (only Rat can do this).
    /// </summary>
    private MoveValidationResult ValidateRiverEnter(Board board, Position from, Position to, Piece piece)
    {
        if (piece.Type != PieceType.Rat)
            return MoveValidationResult.Invalid("CannotEnterRiver", "Only the Rat can enter the river.");
        
        return MoveValidationResult.Valid();
    }
    
    /// <summary>
    /// Validates swimming within the river (only Rat).
    /// </summary>
    private MoveValidationResult ValidateRiverSwim(Board board, Position from, Position to, Piece piece)
    {
        if (piece.Type != PieceType.Rat)
            return MoveValidationResult.Invalid("CannotSwim", "Only the Rat can swim in the river.");
        
        return MoveValidationResult.Valid();
    }
    
    /// <summary>
    /// Validates exiting the river (only Rat).
    /// </summary>
    private MoveValidationResult ValidateRiverExit(Board board, Position from, Position to, Piece piece)
    {
        // Rat exiting the river - check if it can capture the target
        return MoveValidationResult.Valid();
    }
    
    /// <summary>
    /// Validates a Lion/Tiger river jump.
    /// The jump is blocked if there's a Rat in the river along the path.
    /// </summary>
    private MoveValidationResult ValidateRiverJump(Board board, Position from, Position to, Piece piece)
    {
        if (!CanJumpRiver(piece.Type))
            return MoveValidationResult.Invalid("CannotJump", "This piece cannot jump over the river.");
        
        if (!IsValidRiverJump(from, to, out var riverPath))
            return MoveValidationResult.Invalid("InvalidJump", "Invalid river jump path.");
        
        // Check if any Rat is blocking the jump path in the river
        foreach (var riverPos in riverPath)
        {
            var riverPiece = board[riverPos];
            if (riverPiece is not null && riverPiece.Type == PieceType.Rat)
                return MoveValidationResult.Invalid("JumpBlocked", "Cannot jump - a Rat is blocking the river.");
        }
        
        return MoveValidationResult.Valid();
    }
    
    /// <summary>
    /// Validates a capture move.
    /// Handles special rules: Trap rank reduction, Rat-Elephant interactions.
    /// </summary>
    private MoveValidationResult ValidateCapture(Board board, Position from, Position to, Piece attacker, Piece defender)
    {
        // Cannot capture own pieces
        if (attacker.Owner == defender.Owner)
            return MoveValidationResult.Invalid("FriendlyFire", "Cannot capture your own piece.");

        var attackerInRiver = BoardConstants.IsRiver(from);
        var defenderInRiver = BoardConstants.IsRiver(to);

        if (attackerInRiver != defenderInRiver)
            return MoveValidationResult.Invalid("RiverLandCapture", "Pieces in river cannot capture pieces on land, and vice versa.");
        
        // Get effective ranks (considering trap effects)
        var attackerRank = GetEffectiveRank(attacker, from);
        var defenderRank = GetEffectiveRank(defender, to);
        
        // Special rule: Rat from river CANNOT capture Elephant on land
        if (attacker.Type == PieceType.Rat && defender.Type == PieceType.Elephant)
        {
            if (attackerInRiver)
                return MoveValidationResult.Invalid("RatFromRiver", "Rat cannot capture Elephant from the river.");
            // Rat on land CAN capture Elephant
            return MoveValidationResult.Valid();
        }
        
        // Special rule: Elephant CANNOT capture Rat
        if (attacker.Type == PieceType.Elephant && defender.Type == PieceType.Rat)
            return MoveValidationResult.Invalid("ElephantVsRat", "Elephant cannot capture the Rat.");
        
        // Normal capture: Higher rank captures lower rank
        // Note: When defender is in attacker's trap, defender rank is 0
        if (attackerRank < defenderRank)
            return MoveValidationResult.Invalid("RankTooLow", "Your piece rank is too low to capture this piece.");
        
        return MoveValidationResult.Valid();
    }
    
    /// <summary>
    /// Gets the effective rank of a piece, considering trap effects.
    /// A piece in the enemy's trap has rank reduced to 0.
    /// </summary>
    private int GetEffectiveRank(Piece piece, Position position)
    {
        // Check if piece is in enemy's trap
        var enemyTraps = BoardConstants.GetPlayerTraps(piece.Owner == Player.Blue ? Player.Red : Player.Blue);
        
        // Wait, that's wrong. The piece loses rank in its ENEMY's trap
        // Blue piece in Red's trap = rank 0
        // So we check if piece is in the OPPONENT's trap
        var opponentTraps = piece.Owner == Player.Blue 
            ? BoardConstants.RedTrapCells 
            : BoardConstants.BlueTrapCells;
        
        if (opponentTraps.Contains(position))
            return 0; // Trapped! Rank reduced to 0
        
        return piece.Rank;
    }
    
    /// <summary>
    /// Checks if a piece type can jump over the river.
    /// </summary>
    private bool CanJumpRiver(PieceType pieceType) =>
        pieceType == PieceType.Lion || pieceType == PieceType.Tiger;
    
    /// <summary>
    /// Validates if a jump is a valid river crossing and returns the river cells in the path.
    /// </summary>
    private bool IsValidRiverJump(Position from, Position to, out List<Position> riverPath)
    {
        riverPath = [];
        
        // River jump must be horizontal or vertical
        var rowDiff = to.Row - from.Row;
        var colDiff = to.Col - from.Col;
        
        // Must be along one axis only
        if (rowDiff != 0 && colDiff != 0)
            return false;
        
        // Vertical jump (crossing river rows 3-5)
        if (colDiff == 0 && rowDiff != 0)
        {
            var startRow = Math.Min(from.Row, to.Row);
            var endRow = Math.Max(from.Row, to.Row);
            
            // Check if we're crossing the river
            bool crossingRiver = false;
            for (var row = startRow + 1; row < endRow; row++)
            {
                var pos = new Position(row, from.Col);
                if (BoardConstants.IsRiver(pos))
                {
                    crossingRiver = true;
                    riverPath.Add(pos);
                }
                else if (crossingRiver)
                {
                    // We've exited river but haven't reached destination - invalid
                    return false;
                }
            }
            
            // Must have crossed at least one river cell
            return riverPath.Count > 0 && !BoardConstants.IsRiver(from) && !BoardConstants.IsRiver(to);
        }
        
        // Horizontal jump (crossing river columns 1-2 or 4-5)
        if (rowDiff == 0 && colDiff != 0)
        {
            var startCol = Math.Min(from.Col, to.Col);
            var endCol = Math.Max(from.Col, to.Col);
            
            // Check if we're crossing the river
            bool crossingRiver = false;
            for (var col = startCol + 1; col < endCol; col++)
            {
                var pos = new Position(from.Row, col);
                if (BoardConstants.IsRiver(pos))
                {
                    crossingRiver = true;
                    riverPath.Add(pos);
                }
                else if (crossingRiver)
                {
                    // We've exited river but haven't reached destination - invalid
                    return false;
                }
            }
            
            // Must have crossed at least one river cell
            return riverPath.Count > 0 && !BoardConstants.IsRiver(from) && !BoardConstants.IsRiver(to);
        }
        
        return false;
    }
}

/// <summary>
/// Types of moves in Zootact.
/// </summary>
internal enum MoveType
{
    Invalid,
    Adjacent,       // Normal 1-step move on land
    RiverEnter,     // Rat entering river
    RiverSwim,      // Rat moving within river
    RiverExit,      // Rat leaving river
    RiverJump       // Lion/Tiger jumping over river
}
