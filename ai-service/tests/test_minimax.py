"""
Tests for Minimax algorithm.
"""

import pytest
from app.core.board import Board, Position, Player, Piece, PieceType
from app.core.minimax import find_best_move


class TestMinimax:
    """Tests for Minimax search."""
    
    def test_finds_winning_move(self):
        """Minimax finds a move that enters opponent's den."""
        board = Board()
        # Blue Lion one step from Red's den
        board.place_piece(Position(7, 3), Piece(PieceType.LION, Player.BLUE))
        # Add a Red piece so it's not immediately terminal
        board.place_piece(Position(0, 0), Piece(PieceType.RAT, Player.RED))
        
        result = find_best_move(board, Player.BLUE, depth=2)
        
        # Should find the winning move
        assert result.best_move is not None
        assert result.best_move.to_pos == Position(8, 3)
        assert result.evaluation > 10000  # Winning position
    
    def test_finds_capture(self):
        """Minimax prefers capturing high-value pieces when it's clearly advantageous."""
        board = Board()
        # Scenario: Blue Rat that can capture Red Elephant
        # This is a special capture (Rat -> Elephant) that should be preferred
        # Place Rat where capture is clearly best (Elephant undefended, Rat can approach)
        board.place_piece(Position(4, 3), Piece(PieceType.RAT, Player.BLUE))
        board.place_piece(Position(4, 4), Piece(PieceType.ELEPHANT, Player.RED))
        # Add another Red piece far away so game isn't terminal
        board.place_piece(Position(0, 0), Piece(PieceType.LION, Player.RED))
        
        result = find_best_move(board, Player.BLUE, depth=2)
        
        # Rat should capture the Elephant (special rule: Rat can capture Elephant from land)
        assert result.best_move is not None
        assert result.best_move.to_pos == Position(4, 4)
        assert result.best_move.captured_piece is not None
    
    def test_returns_none_for_no_moves(self):
        """Returns None when no moves are available."""
        board = Board()
        # Only opponent pieces
        board.place_piece(Position(4, 3), Piece(PieceType.LION, Player.RED))
        
        result = find_best_move(board, Player.BLUE, depth=2)
        
        assert result.best_move is None
    
    def test_evaluates_multiple_nodes(self):
        """Minimax evaluates multiple nodes with depth > 1."""
        board = Board.initial_setup()
        
        result = find_best_move(board, Player.BLUE, depth=2)
        
        assert result.best_move is not None
        assert result.nodes_evaluated > 1
    
    def test_respects_depth_limit(self):
        """Higher depth evaluates more nodes."""
        board = Board.initial_setup()
        
        result_d1 = find_best_move(board, Player.BLUE, depth=1)
        result_d2 = find_best_move(board, Player.BLUE, depth=2)
        
        # Depth 2 should evaluate more nodes
        assert result_d2.nodes_evaluated > result_d1.nodes_evaluated


class TestMinimaxPerformance:
    """Performance tests for Minimax."""
    
    def test_depth_4_completes(self):
        """Depth 4 search completes in reasonable time."""
        board = Board.initial_setup()
        
        result = find_best_move(board, Player.BLUE, depth=4)
        
        # Target: < 200ms in production, but allow more in test environment
        # Performance varies significantly based on CPU and load
        assert result.time_ms < 2000  # Generous timeout for CI environments
        assert result.best_move is not None
        
        # Log for monitoring
        print(f"Depth 4 completed in {result.time_ms:.2f}ms, {result.nodes_evaluated} nodes")

