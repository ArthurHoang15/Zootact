"""
Minimax Algorithm with Alpha-Beta Pruning
~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~

Implements the Minimax search algorithm for finding the best move.

Features:
- Alpha-Beta pruning for efficiency
- Move ordering for better cutoffs
- Iterative deepening (optional)
- Transposition table (future enhancement)

Performance target: < 200ms for depth 4
"""

import time
from dataclasses import dataclass
from typing import Optional

from app.core.board import Board, Move, Player
from app.core.evaluator import evaluate_position, quick_evaluate_move


@dataclass
class MinimaxResult:
    """Result of a Minimax search."""
    best_move: Optional[Move]
    evaluation: float
    nodes_evaluated: int
    depth_reached: int
    time_ms: float


# Global counter for nodes evaluated (reset each search)
_nodes_evaluated = 0


def find_best_move(
    board: Board,
    player: Player,
    depth: int = 4,
    time_limit_ms: Optional[float] = None
) -> MinimaxResult:
    """
    Find the best move using Minimax with Alpha-Beta pruning.
    
    Args:
        board: Current board state
        player: Player to move
        depth: Maximum search depth (default: 4)
        time_limit_ms: Optional time limit in milliseconds
        
    Returns:
        MinimaxResult with best move and evaluation
    """
    global _nodes_evaluated
    _nodes_evaluated = 0
    
    start_time = time.perf_counter()
    
    is_maximizing = player == Player.BLUE
    
    best_move = None
    best_eval = float('-inf') if is_maximizing else float('inf')
    alpha = float('-inf')
    beta = float('inf')
    
    moves = board.get_valid_moves(player)
    
    if not moves:
        # No valid moves - game might be over
        return MinimaxResult(
            best_move=None,
            evaluation=evaluate_position(board),
            nodes_evaluated=1,
            depth_reached=0,
            time_ms=0.0
        )
    
    # Order moves for better alpha-beta cutoffs
    moves = _order_moves(board, moves)
    
    for move in moves:
        new_board = board.make_move(move)
        
        if is_maximizing:
            eval_score = _minimax(new_board, depth - 1, alpha, beta, False)
            if eval_score > best_eval:
                best_eval = eval_score
                best_move = move
            alpha = max(alpha, eval_score)
        else:
            eval_score = _minimax(new_board, depth - 1, alpha, beta, True)
            if eval_score < best_eval:
                best_eval = eval_score
                best_move = move
            beta = min(beta, eval_score)
    
    elapsed_ms = (time.perf_counter() - start_time) * 1000
    
    return MinimaxResult(
        best_move=best_move,
        evaluation=best_eval,
        nodes_evaluated=_nodes_evaluated,
        depth_reached=depth,
        time_ms=elapsed_ms
    )


def _minimax(
    board: Board,
    depth: int,
    alpha: float,
    beta: float,
    is_maximizing: bool
) -> float:
    """
    Minimax algorithm with Alpha-Beta pruning.
    
    Args:
        board: Current board state
        depth: Remaining search depth
        alpha: Best value for maximizer
        beta: Best value for minimizer
        is_maximizing: True if maximizing player (Blue)
        
    Returns:
        Evaluation score of the position
    """
    global _nodes_evaluated
    _nodes_evaluated += 1
    
    # Check terminal state
    winner = board.is_terminal()
    if winner:
        return 100000.0 if winner == Player.BLUE else -100000.0
    
    # Depth limit reached - return heuristic evaluation
    if depth <= 0:
        return evaluate_position(board)
    
    player = Player.BLUE if is_maximizing else Player.RED
    moves = board.get_valid_moves(player)
    
    # Stalemate check
    if not moves:
        return 0.0  # Draw by stalemate
    
    # Order moves for better cutoffs
    moves = _order_moves(board, moves)
    
    if is_maximizing:
        max_eval = float('-inf')
        for move in moves:
            new_board = board.make_move(move)
            eval_score = _minimax(new_board, depth - 1, alpha, beta, False)
            max_eval = max(max_eval, eval_score)
            alpha = max(alpha, eval_score)
            if beta <= alpha:
                break  # Beta cutoff
        return max_eval
    else:
        min_eval = float('inf')
        for move in moves:
            new_board = board.make_move(move)
            eval_score = _minimax(new_board, depth - 1, alpha, beta, True)
            min_eval = min(min_eval, eval_score)
            beta = min(beta, eval_score)
            if beta <= alpha:
                break  # Alpha cutoff
        return min_eval


def _order_moves(board: Board, moves: list[Move]) -> list[Move]:
    """
    Order moves for better alpha-beta pruning.
    
    Good move ordering can dramatically improve search efficiency.
    We prioritize:
    1. Captures (especially high-value victims)
    2. Moves toward opponent's den
    3. Other moves
    """
    move_scores = [(move, quick_evaluate_move(board, move)) for move in moves]
    move_scores.sort(key=lambda x: x[1], reverse=True)
    return [move for move, _ in move_scores]


# ============================================================================
# Iterative Deepening (for time-limited search)
# ============================================================================

def find_best_move_iterative(
    board: Board,
    player: Player,
    time_limit_ms: float = 200
) -> MinimaxResult:
    """
    Find the best move using iterative deepening.
    
    Searches to increasing depths until time runs out.
    This ensures we always have a move ready even if time is limited.
    
    Args:
        board: Current board state
        player: Player to move
        time_limit_ms: Time limit in milliseconds
        
    Returns:
        MinimaxResult with best move found within time limit
    """
    start_time = time.perf_counter()
    best_result = None
    
    for depth in range(1, 9):  # Max depth 8
        elapsed = (time.perf_counter() - start_time) * 1000
        
        # Check if we have enough time for another iteration
        if elapsed > time_limit_ms * 0.7:  # 70% of time used
            break
        
        result = find_best_move(board, player, depth)
        best_result = result
        
        # If we found a winning move, stop searching
        if abs(result.evaluation) > 90000:
            break
    
    return best_result or MinimaxResult(
        best_move=None,
        evaluation=0.0,
        nodes_evaluated=0,
        depth_reached=0,
        time_ms=0.0
    )
