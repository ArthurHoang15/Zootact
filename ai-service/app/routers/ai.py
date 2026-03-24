"""
AI Analysis Router
~~~~~~~~~~~~~~~~~~~

Endpoints for move analysis and game review.
"""

import time
import logging
from typing import Literal

from fastapi import APIRouter, HTTPException
from pydantic import BaseModel, Field

from app.config import get_settings
from app.core.board import Board, Position, Player, PieceType
from app.core.minimax import find_best_move
from app.services.analysis import AnalysisService

router = APIRouter()
logger = logging.getLogger(__name__)


# ============================================================================
# Request/Response Models (matching implementation_plan.md API contracts)
# ============================================================================

class PositionDto(BaseModel):
    """Position on the board."""
    row: int = Field(..., ge=0, le=8, description="Row (0-8)")
    col: int = Field(..., ge=0, le=6, description="Column (0-6)")


class BestMoveRequest(BaseModel):
    """Request for best move analysis."""
    board: list[list[str | None]] = Field(
        ..., 
        description="9x7 grid with piece codes (e.g., 'L7' for Blue Lion, 'l7' for Red Lion, null for empty)"
    )
    current_player: Literal["Blue", "Red"]
    depth: int = Field(default=4, ge=1, le=8, description="Search depth (1-8)")


class BestMoveResponse(BaseModel):
    """Response with the best move found."""
    from_pos: PositionDto = Field(..., alias="from")
    to_pos: PositionDto = Field(..., alias="to")
    evaluation_score: float = Field(..., description="Positive = Blue advantage")
    search_depth: int
    nodes_evaluated: int
    time_ms: float
    
    class Config:
        populate_by_name = True


class MoveNotation(BaseModel):
    """Single move notation for game analysis."""
    move_number: int
    player: Literal["Blue", "Red"]
    from_pos: str = Field(..., alias="from", description="e.g., '0,3'")
    to_pos: str = Field(..., alias="to")
    piece: str
    
    class Config:
        populate_by_name = True


class AnalyzeGameRequest(BaseModel):
    """Request for full game analysis."""
    moves: list[MoveNotation]


class MoveAnalysis(BaseModel):
    """Analysis result for a single move."""
    move_number: int
    player: Literal["Blue", "Red"]
    played_move: str
    best_move: str
    evaluation_before: float
    evaluation_after: float
    classification: Literal["BestMove", "Excellent", "Good", "Inaccuracy", "Mistake", "Blunder"]
    cute_label: Literal["⭐ SuperStar", "👍 Good", "🤔 Hmm...", "🍌 Oopsie", "💥 Trip!"]


class GameSummary(BaseModel):
    """Summary statistics for the analyzed game."""
    accuracy_blue: float = Field(..., ge=0, le=100, description="Blue accuracy 0-100%")
    accuracy_red: float = Field(..., ge=0, le=100, description="Red accuracy 0-100%")
    blunders_blue: int
    blunders_red: int
    best_moves_blue: int
    best_moves_red: int


class AnalyzeGameResponse(BaseModel):
    """Response with full game analysis."""
    moves: list[MoveAnalysis]
    summary: GameSummary


# ============================================================================
# Endpoints
# ============================================================================

@router.post("/best-move", response_model=BestMoveResponse)
async def get_best_move(request: BestMoveRequest):
    """
    Find the best move for a given board position.
    
    Uses Minimax with Alpha-Beta pruning for efficient search.
    Target response time: < 200ms for depth 4.
    """
    settings = get_settings()
    
    # Clamp depth to configured maximum
    depth = min(request.depth, settings.max_search_depth)
    
    try:
        # Parse the board from the request
        board = Board.from_grid(request.board)
        player = Player.BLUE if request.current_player == "Blue" else Player.RED
        
        # Find best move using Minimax
        start_time = time.perf_counter()
        result = find_best_move(board, player, depth)
        elapsed_ms = (time.perf_counter() - start_time) * 1000
        
        if result.best_move is None:
            raise HTTPException(
                status_code=400, 
                detail="No valid moves available (game may be over)"
            )
        
        logger.info(
            f"Best move found: {result.best_move.from_pos} -> {result.best_move.to_pos} "
            f"(eval: {result.evaluation:.2f}, depth: {depth}, nodes: {result.nodes_evaluated}, "
            f"time: {elapsed_ms:.2f}ms)"
        )
        
        return BestMoveResponse(
            from_pos=PositionDto(row=result.best_move.from_pos.row, col=result.best_move.from_pos.col),
            to_pos=PositionDto(row=result.best_move.to_pos.row, col=result.best_move.to_pos.col),
            evaluation_score=result.evaluation,
            search_depth=depth,
            nodes_evaluated=result.nodes_evaluated,
            time_ms=elapsed_ms
        )
        
    except ValueError as e:
        logger.error(f"Invalid board state: {e}")
        raise HTTPException(status_code=400, detail=f"Invalid board state: {e}")


@router.post("/analyze", response_model=AnalyzeGameResponse)
async def analyze_game(request: AnalyzeGameRequest):
    """
    Analyze a complete game and classify each move.
    
    Provides move-by-move evaluation with "Smart Replay" style labels:
    - ⭐ SuperStar (Best Move)
    - 👍 Good
    - 🤔 Hmm... (Inaccuracy)
    - 🍌 Oopsie (Mistake)
    - 💥 Trip! (Blunder)
    """
    if not request.moves:
        raise HTTPException(status_code=400, detail="No moves to analyze")
    
    try:
        service = AnalysisService()
        result = service.analyze_game(request.moves)
        
        logger.info(
            f"Game analyzed: {len(request.moves)} moves, "
            f"Blue accuracy: {result['summary']['accuracy_blue']:.1f}%, "
            f"Red accuracy: {result['summary']['accuracy_red']:.1f}%"
        )
        
        return result
        
    except Exception as e:
        logger.error(f"Game analysis failed: {e}", exc_info=True)
        raise HTTPException(status_code=500, detail=f"Analysis failed: {e}")
