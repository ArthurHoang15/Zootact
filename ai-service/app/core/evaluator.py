"""
Position Evaluator for Dou Shou Qi
~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~

Heuristic evaluation function for assessing board positions.

Evaluation Components:
1. Material value (piece count with weighted ranks)
2. Positional advantage (proximity to opponent's den)
3. Mobility (number of available moves)
4. Control (pieces in strategic positions)
5. Threat assessment (pieces under attack)

Convention: Positive score = Blue advantage, Negative = Red advantage
"""

from app.core.board import Board, Position, Player, Piece, PieceType, BoardConstants


# ============================================================================
# Piece Value Tables
# ============================================================================

# Base material values (roughly exponential with rank)
PIECE_VALUES = {
    PieceType.RAT: 80,       # High value due to Elephant capture potential
    PieceType.CAT: 100,
    PieceType.WOLF: 120,
    PieceType.DOG: 140,
    PieceType.LEOPARD: 200,
    PieceType.TIGER: 400,
    PieceType.LION: 500,
    PieceType.ELEPHANT: 600,
}

# Position bonuses for advancing toward opponent's den
# Blue wants to move toward row 8, Red toward row 0
ADVANCEMENT_BONUS = [0, 5, 10, 15, 20, 30, 45, 70, 100]  # Indexed by row for Blue


# Position-specific bonuses (center and near-den positions are valuable)
POSITION_BONUS_BLUE = [
    #  0   1   2   3   4   5   6
    [ 0,  0,  0,  0,  0,  0,  0],  # Row 0 - own back rank
    [ 5,  5,  5,  5,  5,  5,  5],  # Row 1
    [10, 10, 10, 10, 10, 10, 10],  # Row 2
    [15, 10, 10, 20, 10, 10, 15],  # Row 3 - near river
    [20, 15, 15, 25, 15, 15, 20],  # Row 4 - river crossing
    [25, 20, 20, 30, 20, 20, 25],  # Row 5 - near enemy territory
    [35, 30, 35, 40, 35, 30, 35],  # Row 6 - enemy territory
    [50, 40, 45, 60, 45, 40, 50],  # Row 7 - near enemy den
    [60, 50, 80,999, 80, 50, 60],  # Row 8 - enemy back rank (den = instant win)
]

# Trap bonuses - having enemy piece in your trap is very valuable
TRAP_CONTROL_BONUS = 150

# Mobility weight
MOBILITY_WEIGHT = 3


# ============================================================================
# Evaluation Function
# ============================================================================

def evaluate_position(board: Board, perspective: Player = Player.BLUE) -> float:
    """
    Evaluate a board position.
    
    Args:
        board: The current board state
        perspective: Whose perspective to evaluate from (default: Blue)
        
    Returns:
        float: Evaluation score
            - Positive: Blue has advantage
            - Negative: Red has advantage
            - Large values (±10000): Near-winning position
    """
    # Check for terminal states first
    winner = board.is_terminal()
    if winner:
        return 100000.0 if winner == Player.BLUE else -100000.0
    
    score = 0.0
    
    # Material and positional evaluation
    score += _evaluate_material_and_position(board)
    
    # Mobility evaluation
    score += _evaluate_mobility(board)
    
    # Trap control
    score += _evaluate_trap_control(board)
    
    # Den proximity bonus
    score += _evaluate_den_proximity(board)
    
    return score


def _evaluate_material_and_position(board: Board) -> float:
    """Evaluate material count and positional value."""
    score = 0.0
    
    for pos, piece in board._pieces.items():
        # Base material value
        value = PIECE_VALUES[piece.piece_type]
        
        # Position bonus based on advancement
        if piece.owner == Player.BLUE:
            pos_bonus = POSITION_BONUS_BLUE[pos.row][pos.col]
        else:
            # Mirror for Red (row 8 - row gives their advancement)
            pos_bonus = POSITION_BONUS_BLUE[8 - pos.row][6 - pos.col]
        
        total_value = value + pos_bonus
        
        # Add or subtract based on owner
        if piece.owner == Player.BLUE:
            score += total_value
        else:
            score -= total_value
    
    return score


def _evaluate_mobility(board: Board) -> float:
    """Evaluate mobility (number of legal moves)."""
    blue_moves = len(board.get_valid_moves(Player.BLUE))
    red_moves = len(board.get_valid_moves(Player.RED))
    
    return (blue_moves - red_moves) * MOBILITY_WEIGHT


def _evaluate_trap_control(board: Board) -> float:
    """Evaluate pieces caught in traps."""
    score = 0.0
    
    # Check Blue's traps (traps Red pieces)
    for trap_pos in BoardConstants.BLUE_TRAPS:
        piece = board.get_piece(Position(*trap_pos))
        if piece and piece.owner == Player.RED:
            score += TRAP_CONTROL_BONUS  # Advantage for Blue
    
    # Check Red's traps (traps Blue pieces)
    for trap_pos in BoardConstants.RED_TRAPS:
        piece = board.get_piece(Position(*trap_pos))
        if piece and piece.owner == Player.BLUE:
            score -= TRAP_CONTROL_BONUS  # Advantage for Red
    
    return score


def _evaluate_den_proximity(board: Board) -> float:
    """
    Bonus for pieces threatening the opponent's den.
    
    Having strong pieces near the enemy den is very valuable.
    """
    score = 0.0
    
    # Blue den proximity bonus
    red_den = BoardConstants.RED_DEN
    for pos, piece in board.get_player_pieces(Player.BLUE):
        distance = abs(pos.row - red_den.row) + abs(pos.col - red_den.col)
        if distance <= 2:
            # Close to enemy den - bonus based on piece value
            proximity_bonus = PIECE_VALUES[piece.piece_type] * (3 - distance) / 10
            score += proximity_bonus
    
    # Red den proximity bonus
    blue_den = BoardConstants.BLUE_DEN
    for pos, piece in board.get_player_pieces(Player.RED):
        distance = abs(pos.row - blue_den.row) + abs(pos.col - blue_den.col)
        if distance <= 2:
            proximity_bonus = PIECE_VALUES[piece.piece_type] * (3 - distance) / 10
            score -= proximity_bonus
    
    return score


# ============================================================================
# Quick Evaluation (for move ordering)
# ============================================================================

def quick_evaluate_move(board: Board, move) -> float:
    """
    Quick heuristic evaluation of a move (without full board evaluation).
    
    Used for move ordering in alpha-beta search.
    Higher scores are searched first.
    """
    score = 0.0
    
    # Captures are generally good (MVV-LVA: Most Valuable Victim - Least Valuable Attacker)
    if move.captured_piece:
        victim_value = PIECE_VALUES[move.captured_piece.piece_type]
        piece = board.get_piece(move.from_pos)
        attacker_value = PIECE_VALUES[piece.piece_type] if piece else 0
        score += victim_value - attacker_value / 10  # MVV-LVA
    
    # Moving toward opponent's den is good
    piece = board.get_piece(move.from_pos)
    if piece:
        if piece.owner == Player.BLUE:
            advancement = move.to_pos.row - move.from_pos.row
        else:
            advancement = move.from_pos.row - move.to_pos.row
        score += advancement * 10
    
    # Threatening the den is very good
    target_den = BoardConstants.RED_DEN if piece and piece.owner == Player.BLUE else BoardConstants.BLUE_DEN
    if move.to_pos == target_den:
        score += 1000  # Winning move
    
    return score
