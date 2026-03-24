"""
Tests for Board representation and game logic.
"""

import pytest
from app.core.board import (
    Board, Position, Player, Piece, PieceType, 
    BoardConstants, Move
)


class TestPosition:
    """Tests for Position class."""
    
    def test_position_is_valid(self):
        """Valid positions are within board bounds."""
        assert Position(0, 0).is_valid()
        assert Position(8, 6).is_valid()
        assert Position(4, 3).is_valid()
    
    def test_position_is_invalid(self):
        """Invalid positions are outside board bounds."""
        assert not Position(-1, 0).is_valid()
        assert not Position(0, -1).is_valid()
        assert not Position(9, 0).is_valid()
        assert not Position(0, 7).is_valid()
    
    def test_position_addition(self):
        """Position can add direction offsets."""
        pos = Position(4, 3)
        assert pos + (0, 1) == Position(4, 4)
        assert pos + (1, 0) == Position(5, 3)
        assert pos + (-1, -1) == Position(3, 2)


class TestBoardConstants:
    """Tests for BoardConstants."""
    
    def test_river_cells(self):
        """River cells are correctly defined."""
        assert BoardConstants.is_river(Position(3, 1))
        assert BoardConstants.is_river(Position(4, 2))
        assert BoardConstants.is_river(Position(5, 5))
        assert not BoardConstants.is_river(Position(4, 3))  # Center column is land
        assert not BoardConstants.is_river(Position(0, 0))
    
    def test_den_positions(self):
        """Den positions are correctly identified."""
        assert BoardConstants.is_den(Position(0, 3)) == Player.BLUE
        assert BoardConstants.is_den(Position(8, 3)) == Player.RED
        assert BoardConstants.is_den(Position(4, 3)) is None
    
    def test_trap_positions(self):
        """Trap positions correctly trap opposing pieces."""
        # Blue traps are at positions that trap Red pieces
        assert BoardConstants.is_trap(Position(0, 2), Player.RED)  # Red in Blue's trap
        assert BoardConstants.is_trap(Position(0, 4), Player.RED)
        assert BoardConstants.is_trap(Position(1, 3), Player.RED)
        
        # Red traps
        assert BoardConstants.is_trap(Position(8, 2), Player.BLUE)
        assert BoardConstants.is_trap(Position(7, 3), Player.BLUE)


class TestPiece:
    """Tests for Piece class."""
    
    def test_piece_ranks(self):
        """Pieces have correct ranks."""
        rat = Piece(PieceType.RAT, Player.BLUE)
        elephant = Piece(PieceType.ELEPHANT, Player.BLUE)
        lion = Piece(PieceType.LION, Player.RED)
        
        assert rat.rank == 1
        assert elephant.rank == 8
        assert lion.rank == 7
    
    def test_higher_rank_captures_lower(self):
        """Higher rank can capture lower rank."""
        lion = Piece(PieceType.LION, Player.BLUE)
        cat = Piece(PieceType.CAT, Player.RED)
        
        assert lion.can_capture(cat)
    
    def test_cannot_capture_own_piece(self):
        """Cannot capture own pieces."""
        lion = Piece(PieceType.LION, Player.BLUE)
        tiger = Piece(PieceType.TIGER, Player.BLUE)
        
        assert not lion.can_capture(tiger)
    
    def test_rat_captures_elephant_from_land(self):
        """Rat can capture Elephant from land."""
        rat = Piece(PieceType.RAT, Player.BLUE)
        elephant = Piece(PieceType.ELEPHANT, Player.RED)
        
        assert rat.can_capture(elephant, from_river=False)
    
    def test_rat_cannot_capture_elephant_from_river(self):
        """Rat cannot capture Elephant from river."""
        rat = Piece(PieceType.RAT, Player.BLUE)
        elephant = Piece(PieceType.ELEPHANT, Player.RED)
        
        assert not rat.can_capture(elephant, from_river=True)
    
    def test_elephant_cannot_capture_rat(self):
        """Elephant cannot capture Rat."""
        elephant = Piece(PieceType.ELEPHANT, Player.BLUE)
        rat = Piece(PieceType.RAT, Player.RED)
        
        assert not elephant.can_capture(rat)
    
    def test_trapped_piece_can_be_captured_by_any(self):
        """Trapped piece can be captured by any enemy piece."""
        rat = Piece(PieceType.RAT, Player.BLUE)
        elephant = Piece(PieceType.ELEPHANT, Player.RED)
        
        # Normally rat can't capture elephant (from river), but in trap it's different
        # The trap reduces enemy's rank to 0
        assert rat.can_capture(elephant, in_trap=True)
    
    def test_piece_to_code(self):
        """Pieces convert to correct notation codes."""
        blue_lion = Piece(PieceType.LION, Player.BLUE)
        red_rat = Piece(PieceType.RAT, Player.RED)
        
        assert blue_lion.to_code() == "L7"
        assert red_rat.to_code() == "r1"


class TestBoard:
    """Tests for Board class."""
    
    def test_initial_setup(self):
        """Board initial setup has all pieces in correct positions."""
        board = Board.initial_setup()
        
        # Blue Lion at (0, 0)
        lion = board.get_piece(Position(0, 0))
        assert lion is not None
        assert lion.piece_type == PieceType.LION
        assert lion.owner == Player.BLUE
        
        # Red Lion at (8, 6)
        red_lion = board.get_piece(Position(8, 6))
        assert red_lion is not None
        assert red_lion.piece_type == PieceType.LION
        assert red_lion.owner == Player.RED
        
        # Check piece counts
        blue_pieces = board.get_player_pieces(Player.BLUE)
        red_pieces = board.get_player_pieces(Player.RED)
        assert len(blue_pieces) == 8
        assert len(red_pieces) == 8
    
    def test_rat_can_enter_river(self):
        """Rat can move into river cells."""
        board = Board()
        # Place a Blue Rat near the river
        board.place_piece(Position(2, 1), Piece(PieceType.RAT, Player.BLUE))
        
        moves = board.get_valid_moves(Player.BLUE)
        river_moves = [m for m in moves if BoardConstants.is_river(m.to_pos)]
        
        # Should be able to move to (3, 1) which is a river cell
        assert any(m.to_pos == Position(3, 1) for m in moves)
    
    def test_non_rat_cannot_enter_river(self):
        """Non-rat pieces cannot enter river."""
        board = Board()
        # Place a Blue Cat near the river
        board.place_piece(Position(2, 1), Piece(PieceType.CAT, Player.BLUE))
        
        moves = board.get_valid_moves(Player.BLUE)
        river_moves = [m for m in moves if BoardConstants.is_river(m.to_pos)]
        
        assert len(river_moves) == 0
    
    def test_lion_jumps_river(self):
        """Lion can jump over river (no rat blocking)."""
        board = Board()
        # Place a Blue Lion at the edge of river
        board.place_piece(Position(2, 1), Piece(PieceType.LION, Player.BLUE))
        
        moves = board.get_valid_moves(Player.BLUE)
        
        # Should be able to jump to (6, 1)
        jump_move = [m for m in moves if m.to_pos == Position(6, 1)]
        assert len(jump_move) == 1
    
    def test_lion_cannot_jump_if_rat_blocking(self):
        """Lion cannot jump if Rat is in the river path."""
        board = Board()
        board.place_piece(Position(2, 1), Piece(PieceType.LION, Player.BLUE))
        # Place a Red Rat in the river path
        board.place_piece(Position(4, 1), Piece(PieceType.RAT, Player.RED))
        
        moves = board.get_valid_moves(Player.BLUE)
        
        # Should NOT be able to jump to (6, 1)
        jump_move = [m for m in moves if m.to_pos == Position(6, 1)]
        assert len(jump_move) == 0
    
    def test_cannot_enter_own_den(self):
        """Cannot enter own den."""
        board = Board()
        # Place a Blue Lion near Blue's den
        board.place_piece(Position(1, 3), Piece(PieceType.LION, Player.BLUE))
        
        moves = board.get_valid_moves(Player.BLUE)
        
        # Should NOT be able to move to (0, 3) - Blue's den
        den_move = [m for m in moves if m.to_pos == Position(0, 3)]
        assert len(den_move) == 0
    
    def test_can_enter_opponent_den(self):
        """Can enter opponent's den."""
        board = Board()
        # Place a Blue Lion near Red's den
        board.place_piece(Position(7, 3), Piece(PieceType.LION, Player.BLUE))
        
        moves = board.get_valid_moves(Player.BLUE)
        
        # Should be able to move to (8, 3) - Red's den
        den_move = [m for m in moves if m.to_pos == Position(8, 3)]
        assert len(den_move) == 1
    
    def test_entering_den_wins(self):
        """Entering opponent's den results in victory."""
        board = Board()
        board.place_piece(Position(7, 3), Piece(PieceType.LION, Player.BLUE))
        
        # Make the move
        move = Move(Position(7, 3), Position(8, 3))
        new_board = board.make_move(move)
        
        # Blue should win
        assert new_board.is_terminal() == Player.BLUE
    
    def test_all_pieces_captured_wins(self):
        """Capturing all opponent's pieces results in victory."""
        board = Board()
        board.place_piece(Position(4, 3), Piece(PieceType.LION, Player.BLUE))
        # No Red pieces = Blue wins
        
        assert board.is_terminal() == Player.BLUE


class TestBoardCopy:
    """Tests for immutable board operations."""
    
    def test_make_move_is_immutable(self):
        """make_move returns new board, doesn't modify original."""
        board = Board.initial_setup()
        original_piece = board.get_piece(Position(0, 0))
        
        move = Move(Position(0, 0), Position(1, 0))
        new_board = board.make_move(move)
        
        # Original board unchanged
        assert board.get_piece(Position(0, 0)) == original_piece
        assert board.get_piece(Position(1, 0)) is None
        
        # New board has the move applied
        assert new_board.get_piece(Position(0, 0)) is None
        assert new_board.get_piece(Position(1, 0)) is not None
