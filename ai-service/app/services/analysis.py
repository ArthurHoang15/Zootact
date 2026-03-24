"""
Game Analysis Service
~~~~~~~~~~~~~~~~~~~~~~

Provides move-by-move analysis with "Smart Replay" style classifications.

Move Classification:
- BestMove (⭐ SuperStar): The move matched or exceeded the engine's top choice
- Excellent (👍 Good): Within 20 centipawns of best
- Good (👍 Good): Within 50 centipawns of best  
- Inaccuracy (🤔 Hmm...): Lost 50-100 centipawns
- Mistake (🍌 Oopsie): Lost 100-300 centipawns
- Blunder (💥 Trip!): Lost more than 300 centipawns
"""

import logging
from typing import Literal

from app.core.board import Board, Position, Player, Move, PieceType, Piece
from app.core.minimax import find_best_move
from app.core.evaluator import evaluate_position

logger = logging.getLogger(__name__)


# Classification thresholds (in centipawns / evaluation units)
THRESHOLDS = {
    "excellent": 20,    # Within 20 of best
    "good": 50,         # Within 50 of best
    "inaccuracy": 100,  # Lost up to 100
    "mistake": 300,     # Lost 100-300
    # anything above 300 is a blunder
}


class MoveAnalysisResult:
    """Result of analyzing a single move."""
    
    def __init__(
        self,
        move_number: int,
        player: Literal["Blue", "Red"],
        played_move: str,
        best_move: str,
        evaluation_before: float,
        evaluation_after: float,
    ):
        self.move_number = move_number
        self.player = player
        self.played_move = played_move
        self.best_move = best_move
        self.evaluation_before = evaluation_before
        self.evaluation_after = evaluation_after
        
        # Calculate classification
        self.classification, self.cute_label = self._classify()
    
    def _classify(self) -> tuple[str, str]:
        """Classify the move based on evaluation loss."""
        # Calculate eval loss from the player's perspective
        if self.player == "Blue":
            eval_loss = self.evaluation_before - self.evaluation_after
        else:
            # For Red, losing (making eval more positive) is bad
            eval_loss = self.evaluation_after - self.evaluation_before
        
        # Adjust for moves that improved position
        if eval_loss <= 0:
            # Move was at least as good as expected
            if self.played_move == self.best_move:
                return ("BestMove", "⭐ SuperStar")
            else:
                return ("Excellent", "👍 Good")
        
        if eval_loss <= THRESHOLDS["excellent"]:
            return ("Excellent", "👍 Good")
        elif eval_loss <= THRESHOLDS["good"]:
            return ("Good", "👍 Good")
        elif eval_loss <= THRESHOLDS["inaccuracy"]:
            return ("Inaccuracy", "🤔 Hmm...")
        elif eval_loss <= THRESHOLDS["mistake"]:
            return ("Mistake", "🍌 Oopsie")
        else:
            return ("Blunder", "💥 Trip!")
    
    def to_dict(self) -> dict:
        """Convert to dictionary for API response."""
        return {
            "move_number": self.move_number,
            "player": self.player,
            "played_move": self.played_move,
            "best_move": self.best_move,
            "evaluation_before": self.evaluation_before,
            "evaluation_after": self.evaluation_after,
            "classification": self.classification,
            "cute_label": self.cute_label,
        }


class GameSummaryResult:
    """Summary statistics for a game analysis."""
    
    def __init__(self):
        self.total_moves_blue = 0
        self.total_moves_red = 0
        self.correct_moves_blue = 0
        self.correct_moves_red = 0
        self.blunders_blue = 0
        self.blunders_red = 0
        self.best_moves_blue = 0
        self.best_moves_red = 0
    
    def update(self, player: str, classification: str):
        """Update summary with a move classification."""
        if player == "Blue":
            self.total_moves_blue += 1
            if classification in ("BestMove", "Excellent", "Good"):
                self.correct_moves_blue += 1
            if classification == "BestMove":
                self.best_moves_blue += 1
            if classification == "Blunder":
                self.blunders_blue += 1
        else:
            self.total_moves_red += 1
            if classification in ("BestMove", "Excellent", "Good"):
                self.correct_moves_red += 1
            if classification == "BestMove":
                self.best_moves_red += 1
            if classification == "Blunder":
                self.blunders_red += 1
    
    @property
    def accuracy_blue(self) -> float:
        if self.total_moves_blue == 0:
            return 100.0
        return (self.correct_moves_blue / self.total_moves_blue) * 100
    
    @property
    def accuracy_red(self) -> float:
        if self.total_moves_red == 0:
            return 100.0
        return (self.correct_moves_red / self.total_moves_red) * 100
    
    def to_dict(self) -> dict:
        return {
            "accuracy_blue": round(self.accuracy_blue, 1),
            "accuracy_red": round(self.accuracy_red, 1),
            "blunders_blue": self.blunders_blue,
            "blunders_red": self.blunders_red,
            "best_moves_blue": self.best_moves_blue,
            "best_moves_red": self.best_moves_red,
        }


class AnalysisService:
    """
    Service for analyzing games and classifying moves.
    
    Uses Minimax engine to evaluate positions and compare
    played moves against best moves.
    """
    
    def __init__(self, analysis_depth: int = 4):
        """
        Initialize the analysis service.
        
        Args:
            analysis_depth: Depth for Minimax analysis (default: 4)
        """
        self.analysis_depth = analysis_depth
    
    def analyze_game(self, moves: list) -> dict:
        """
        Analyze a complete game.
        
        Args:
            moves: List of MoveNotation objects from the API
            
        Returns:
            AnalyzeGameResponse-compatible dict
        """
        board = Board.initial_setup()
        analyzed_moves = []
        summary = GameSummaryResult()
        
        for move_notation in moves:
            # Parse the move
            from_pos = self._parse_position(move_notation.from_pos)
            to_pos = self._parse_position(move_notation.to_pos)
            player = Player.BLUE if move_notation.player == "Blue" else Player.RED
            
            # Evaluate position before the move
            eval_before = evaluate_position(board)
            
            # Find the best move according to the engine
            best_result = find_best_move(board, player, self.analysis_depth)
            best_move_str = f"{best_result.best_move.from_pos} -> {best_result.best_move.to_pos}" if best_result.best_move else "none"
            
            # Apply the played move
            played_move = self._find_move(board, from_pos, to_pos)
            if played_move:
                board = board.make_move(played_move)
            
            # Evaluate position after the move
            eval_after = evaluate_position(board)
            
            # Classify the move
            played_move_str = f"{from_pos} -> {to_pos}"
            analysis = MoveAnalysisResult(
                move_number=move_notation.move_number,
                player=move_notation.player,
                played_move=played_move_str,
                best_move=best_move_str,
                evaluation_before=eval_before,
                evaluation_after=eval_after,
            )
            
            analyzed_moves.append(analysis.to_dict())
            summary.update(move_notation.player, analysis.classification)
            
            logger.debug(
                f"Move {move_notation.move_number}: {move_notation.player} "
                f"{played_move_str} [{analysis.classification}] "
                f"eval: {eval_before:.0f} -> {eval_after:.0f}"
            )
        
        return {
            "moves": analyzed_moves,
            "summary": summary.to_dict(),
        }
    
    def _parse_position(self, pos_str: str) -> Position:
        """Parse a position string like '0,3' into a Position."""
        parts = pos_str.split(",")
        return Position(int(parts[0]), int(parts[1]))
    
    def _find_move(self, board: Board, from_pos: Position, to_pos: Position) -> Move | None:
        """Find a move object matching the given positions."""
        piece = board.get_piece(from_pos)
        if not piece:
            return None
        
        moves = board.get_valid_moves(piece.owner)
        for move in moves:
            if move.from_pos == from_pos and move.to_pos == to_pos:
                return move
        
        # Move not in valid moves - create anyway (for analysis)
        target = board.get_piece(to_pos)
        return Move(from_pos, to_pos, target)
