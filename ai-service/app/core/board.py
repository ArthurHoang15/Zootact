"""
Board Representation for Dou Shou Qi (Animal Chess)
~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~

Implements the 7x9 board with all terrain types and piece logic.

Board Layout (9 rows x 7 cols):
- Row 0: Blue's back rank (Blue's den at (0,3))
- Row 8: Red's back rank (Red's den at (8,3))
- Rows 3-5, Cols 1-2 and 4-5: River tiles
- Traps: Adjacent to each den

Piece Notation:
- Uppercase = Blue (e.g., 'L' = Blue Lion)
- Lowercase = Red (e.g., 'l' = Red Lion)
- Number suffix = Rank (1-8)
"""

from dataclasses import dataclass
from enum import IntEnum, Enum
from typing import Optional
import numpy as np


class Player(Enum):
    """Player colors. Blue always moves first."""
    BLUE = "Blue"
    RED = "Red"
    
    def opponent(self) -> "Player":
        return Player.RED if self == Player.BLUE else Player.BLUE


class PieceType(IntEnum):
    """
    Piece types with their ranks.
    Higher rank captures lower rank (except Rat -> Elephant).
    """
    RAT = 1       # 🐀 Can enter river, captures Elephant from land
    CAT = 2       # 🐱
    WOLF = 3      # 🐺  
    DOG = 4       # 🐕
    LEOPARD = 5   # 🐆
    TIGER = 6     # 🐅 Can jump over river
    LION = 7      # 🦁 Can jump over river
    ELEPHANT = 8  # 🐘 Highest rank, but cannot capture Rat


class TerrainType(Enum):
    """Types of terrain on the board."""
    NORMAL = "normal"
    RIVER = "river"
    TRAP = "trap"
    DEN = "den"


@dataclass(frozen=True, slots=True)
class Position:
    """A position on the 7x9 board."""
    row: int  # 0-8
    col: int  # 0-6
    
    def is_valid(self) -> bool:
        """Check if position is within board bounds."""
        return 0 <= self.row <= 8 and 0 <= self.col <= 6
    
    def __add__(self, other: tuple[int, int]) -> "Position":
        """Add a direction offset to position."""
        return Position(self.row + other[0], self.col + other[1])
    
    def __str__(self) -> str:
        return f"{self.row},{self.col}"


@dataclass(frozen=True, slots=True)
class Piece:
    """A game piece with type and owner."""
    piece_type: PieceType
    owner: Player
    
    @property
    def rank(self) -> int:
        """Get the rank value of this piece."""
        return int(self.piece_type)
    
    def can_capture(self, other: "Piece", from_river: bool = False, in_trap: bool = False) -> bool:
        """
        Check if this piece can capture another.
        
        Args:
            other: The piece to potentially capture
            from_river: Whether this piece is attacking from the river
            in_trap: Whether the target piece is in a trap
            
        Returns:
            bool: True if capture is allowed
        """
        # Cannot capture own pieces
        if self.owner == other.owner:
            return False
        
        # Trapped pieces can be captured by anyone
        if in_trap:
            return True
        
        # Special case: Rat -> Elephant
        if self.piece_type == PieceType.RAT and other.piece_type == PieceType.ELEPHANT:
            # Rat can only capture Elephant from land, not from river
            return not from_river
        
        # Special case: Elephant cannot capture Rat
        if self.piece_type == PieceType.ELEPHANT and other.piece_type == PieceType.RAT:
            return False
        
        # Normal capture: higher or equal rank wins
        return self.rank >= other.rank
    
    def to_code(self) -> str:
        """Convert to notation code (e.g., 'L7' for Blue Lion)."""
        type_char = self.piece_type.name[0]  # First letter
        if self.owner == Player.BLUE:
            return f"{type_char.upper()}{self.rank}"
        else:
            return f"{type_char.lower()}{self.rank}"
    
    @staticmethod
    def from_code(code: str) -> Optional["Piece"]:
        """Parse a piece from notation code."""
        if not code or len(code) < 2:
            return None
        
        type_char = code[0].upper()
        is_blue = code[0].isupper()
        
        type_map = {
            'R': PieceType.RAT,
            'C': PieceType.CAT,
            'W': PieceType.WOLF,
            'D': PieceType.DOG,
            'L': PieceType.LEOPARD if type_char == 'L' and len(code) > 1 and code[1] == '5' else PieceType.LION,
            'T': PieceType.TIGER,
            'E': PieceType.ELEPHANT,
        }
        
        # Handle Leopard vs Lion ambiguity by checking rank
        if type_char == 'L':
            rank = int(code[1]) if len(code) > 1 and code[1].isdigit() else 7
            piece_type = PieceType.LEOPARD if rank == 5 else PieceType.LION
        else:
            piece_type = type_map.get(type_char)
            
        if piece_type is None:
            return None
            
        return Piece(
            piece_type=piece_type,
            owner=Player.BLUE if is_blue else Player.RED
        )


@dataclass(frozen=True, slots=True)
class Move:
    """A move from one position to another."""
    from_pos: Position
    to_pos: Position
    captured_piece: Optional[Piece] = None
    
    def __str__(self) -> str:
        cap = f" x{self.captured_piece.to_code()}" if self.captured_piece else ""
        return f"{self.from_pos} -> {self.to_pos}{cap}"


class BoardConstants:
    """
    Static board configuration for Dou Shou Qi.
    
    Board Layout:
    ```
        0   1   2   3   4   5   6
    0  [L] [ ] [T] [D] [T] [ ] [L]   <- Blue back rank
    1  [ ] [D] [ ] [ ] [ ] [C] [ ]
    2  [R] [ ] [L] [ ] [W] [ ] [E]
    3  [ ] [~] [~] [ ] [~] [~] [ ]   <- River
    4  [ ] [~] [~] [ ] [~] [~] [ ]   <- River
    5  [ ] [~] [~] [ ] [~] [~] [ ]   <- River
    6  [e] [ ] [w] [ ] [l] [ ] [r]
    7  [ ] [c] [ ] [ ] [ ] [d] [ ]
    8  [l] [ ] [t] [d] [t] [ ] [l]   <- Red back rank
    ```
    """
    
    ROWS = 9
    COLS = 7
    
    # River cells (rows 3-5, cols 1-2 and 4-5)
    RIVER_CELLS: frozenset[tuple[int, int]] = frozenset([
        (3, 1), (3, 2), (3, 4), (3, 5),
        (4, 1), (4, 2), (4, 4), (4, 5),
        (5, 1), (5, 2), (5, 4), (5, 5),
    ])
    
    # Den positions
    BLUE_DEN = Position(0, 3)
    RED_DEN = Position(8, 3)
    
    # Trap positions (adjacent to dens)
    BLUE_TRAPS: frozenset[tuple[int, int]] = frozenset([
        (0, 2), (0, 4), (1, 3)
    ])
    RED_TRAPS: frozenset[tuple[int, int]] = frozenset([
        (8, 2), (8, 4), (7, 3)
    ])
    
    # Movement directions (row_delta, col_delta)
    DIRECTIONS = [(0, 1), (0, -1), (1, 0), (-1, 0)]
    
    @classmethod
    def is_river(cls, pos: Position) -> bool:
        """Check if a position is a river cell."""
        return (pos.row, pos.col) in cls.RIVER_CELLS
    
    @classmethod
    def is_trap(cls, pos: Position, for_player: Player) -> bool:
        """Check if a position is a trap for the given player."""
        if for_player == Player.BLUE:
            return (pos.row, pos.col) in cls.RED_TRAPS  # Red's traps trap Blue pieces
        return (pos.row, pos.col) in cls.BLUE_TRAPS
    
    @classmethod
    def is_den(cls, pos: Position) -> Optional[Player]:
        """Check if position is a den, return owner if so."""
        if pos == cls.BLUE_DEN:
            return Player.BLUE
        if pos == cls.RED_DEN:
            return Player.RED
        return None
    
    @classmethod
    def get_terrain(cls, pos: Position) -> TerrainType:
        """Get the terrain type at a position."""
        if cls.is_river(pos):
            return TerrainType.RIVER
        if (pos.row, pos.col) in cls.BLUE_TRAPS or (pos.row, pos.col) in cls.RED_TRAPS:
            return TerrainType.TRAP
        if cls.is_den(pos):
            return TerrainType.DEN
        return TerrainType.NORMAL


class Board:
    """
    The game board state.
    
    Uses a 9x7 numpy array for efficient operations.
    """
    
    def __init__(self, grid: Optional[np.ndarray] = None):
        """
        Initialize the board.
        
        Args:
            grid: Optional 9x7 array of piece indices (0 = empty, 1-8 = Blue, -1 to -8 = Red)
        """
        if grid is not None:
            self._grid = grid.copy()
            self._pieces: dict[Position, Piece] = {}
            self._rebuild_pieces()
        else:
            self._grid = np.zeros((BoardConstants.ROWS, BoardConstants.COLS), dtype=np.int8)
            self._pieces = {}
    
    def _rebuild_pieces(self):
        """Rebuild the pieces dictionary from the grid."""
        self._pieces.clear()
        for row in range(BoardConstants.ROWS):
            for col in range(BoardConstants.COLS):
                val = self._grid[row, col]
                if val != 0:
                    pos = Position(row, col)
                    if val > 0:
                        self._pieces[pos] = Piece(PieceType(val), Player.BLUE)
                    else:
                        self._pieces[pos] = Piece(PieceType(-val), Player.RED)
    
    @classmethod
    def initial_setup(cls) -> "Board":
        """Create a board with the standard initial piece setup."""
        board = cls()
        
        # Blue pieces (top half)
        blue_setup = [
            (Position(0, 0), PieceType.LION),
            (Position(0, 6), PieceType.TIGER),
            (Position(1, 1), PieceType.DOG),
            (Position(1, 5), PieceType.CAT),
            (Position(2, 0), PieceType.RAT),
            (Position(2, 2), PieceType.LEOPARD),
            (Position(2, 4), PieceType.WOLF),
            (Position(2, 6), PieceType.ELEPHANT),
        ]
        
        # Red pieces (bottom half, mirrored)
        red_setup = [
            (Position(8, 6), PieceType.LION),
            (Position(8, 0), PieceType.TIGER),
            (Position(7, 5), PieceType.DOG),
            (Position(7, 1), PieceType.CAT),
            (Position(6, 6), PieceType.RAT),
            (Position(6, 4), PieceType.LEOPARD),
            (Position(6, 2), PieceType.WOLF),
            (Position(6, 0), PieceType.ELEPHANT),
        ]
        
        for pos, piece_type in blue_setup:
            board.place_piece(pos, Piece(piece_type, Player.BLUE))
        
        for pos, piece_type in red_setup:
            board.place_piece(pos, Piece(piece_type, Player.RED))
        
        return board
    
    @classmethod
    def from_grid(cls, grid: list[list[str | None]]) -> "Board":
        """
        Create a board from a 2D grid of piece codes.
        
        Args:
            grid: 9x7 list with piece codes (e.g., 'L7' for Blue Lion) or None for empty
            
        Returns:
            Board instance
        """
        if len(grid) != 9:
            raise ValueError(f"Grid must have 9 rows, got {len(grid)}")
        
        board = cls()
        
        for row_idx, row in enumerate(grid):
            if len(row) != 7:
                raise ValueError(f"Row {row_idx} must have 7 columns, got {len(row)}")
            
            for col_idx, cell in enumerate(row):
                if cell:
                    piece = Piece.from_code(cell)
                    if piece:
                        board.place_piece(Position(row_idx, col_idx), piece)
        
        return board
    
    def copy(self) -> "Board":
        """Create a deep copy of the board."""
        new_board = Board()
        new_board._grid = self._grid.copy()
        new_board._pieces = self._pieces.copy()
        return new_board
    
    def get_piece(self, pos: Position) -> Optional[Piece]:
        """Get the piece at a position, or None if empty."""
        return self._pieces.get(pos)
    
    def place_piece(self, pos: Position, piece: Piece):
        """Place a piece at a position."""
        self._pieces[pos] = piece
        sign = 1 if piece.owner == Player.BLUE else -1
        self._grid[pos.row, pos.col] = sign * int(piece.piece_type)
    
    def remove_piece(self, pos: Position) -> Optional[Piece]:
        """Remove and return the piece at a position."""
        piece = self._pieces.pop(pos, None)
        self._grid[pos.row, pos.col] = 0
        return piece
    
    def get_player_pieces(self, player: Player) -> list[tuple[Position, Piece]]:
        """Get all pieces belonging to a player."""
        return [(pos, piece) for pos, piece in self._pieces.items() if piece.owner == player]
    
    def is_terminal(self) -> Optional[Player]:
        """
        Check if the game is over.
        
        Returns:
            Winner if game is over, None otherwise
        """
        # Check if anyone entered opponent's den
        for pos, piece in self._pieces.items():
            den_owner = BoardConstants.is_den(pos)
            if den_owner and den_owner != piece.owner:
                return piece.owner  # Player who entered den wins
        
        # Check if a player has no pieces left
        blue_pieces = [p for p in self._pieces.values() if p.owner == Player.BLUE]
        red_pieces = [p for p in self._pieces.values() if p.owner == Player.RED]
        
        if not blue_pieces:
            return Player.RED
        if not red_pieces:
            return Player.BLUE
        
        return None
    
    def get_valid_moves(self, player: Player) -> list[Move]:
        """
        Generate all valid moves for a player.
        
        Implements all special movement rules:
        - Rat can enter river
        - Lion/Tiger can jump over river (if no Rat blocking)
        - Cannot enter own den
        - Capture rules with rank comparison
        """
        moves = []
        
        for pos, piece in self.get_player_pieces(player):
            moves.extend(self._get_piece_moves(pos, piece))
        
        return moves
    
    def _get_piece_moves(self, pos: Position, piece: Piece) -> list[Move]:
        """Get all valid moves for a specific piece."""
        moves = []
        
        for direction in BoardConstants.DIRECTIONS:
            # Check for jumping (Lion/Tiger)
            if piece.piece_type in (PieceType.LION, PieceType.TIGER):
                jump_move = self._try_jump(pos, piece, direction)
                if jump_move:
                    moves.append(jump_move)
            
            # Normal movement
            new_pos = pos + direction
            if not new_pos.is_valid():
                continue
            
            # Check river entry
            if BoardConstants.is_river(new_pos):
                # Only Rat can enter river
                if piece.piece_type != PieceType.RAT:
                    continue
            
            # Check den entry - cannot enter own den
            den_owner = BoardConstants.is_den(new_pos)
            if den_owner == piece.owner:
                continue
            
            # Check capture
            target = self.get_piece(new_pos)
            if target:
                from_river = BoardConstants.is_river(pos)
                in_trap = BoardConstants.is_trap(new_pos, target.owner)
                
                if piece.can_capture(target, from_river=from_river, in_trap=in_trap):
                    moves.append(Move(pos, new_pos, target))
            else:
                moves.append(Move(pos, new_pos))
        
        return moves
    
    def _try_jump(self, pos: Position, piece: Piece, direction: tuple[int, int]) -> Optional[Move]:
        """
        Try to perform a river jump for Lion or Tiger.
        
        Returns:
            Move if jump is valid, None otherwise
        """
        # Check if we're at the edge of the river
        next_pos = pos + direction
        if not next_pos.is_valid() or not BoardConstants.is_river(next_pos):
            return None
        
        # Find the landing position (after crossing river)
        current = next_pos
        while current.is_valid() and BoardConstants.is_river(current):
            # Check for Rat blocking in river
            blocking_piece = self.get_piece(current)
            if blocking_piece and blocking_piece.piece_type == PieceType.RAT:
                return None  # Rat blocks the jump
            current = current + direction
        
        if not current.is_valid():
            return None
        
        # Check if landing square is valid
        target = self.get_piece(current)
        if target:
            if target.owner == piece.owner:
                return None  # Can't capture own piece
            in_trap = BoardConstants.is_trap(current, target.owner)
            if piece.can_capture(target, in_trap=in_trap):
                return Move(pos, current, target)
            return None
        
        # Cannot land in own den
        den_owner = BoardConstants.is_den(current)
        if den_owner == piece.owner:
            return None
        
        return Move(pos, current)
    
    def make_move(self, move: Move) -> "Board":
        """
        Apply a move and return a new board state.
        
        This is an immutable operation - the original board is not modified.
        """
        new_board = self.copy()
        
        piece = new_board.remove_piece(move.from_pos)
        if move.captured_piece:
            new_board.remove_piece(move.to_pos)
        
        if piece:
            new_board.place_piece(move.to_pos, piece)
        
        return new_board
    
    def to_grid(self) -> list[list[str | None]]:
        """Convert board to a 2D grid of piece codes."""
        grid = []
        for row in range(BoardConstants.ROWS):
            row_data = []
            for col in range(BoardConstants.COLS):
                piece = self.get_piece(Position(row, col))
                row_data.append(piece.to_code() if piece else None)
            grid.append(row_data)
        return grid
    
    def __str__(self) -> str:
        """Pretty print the board for debugging."""
        lines = []
        lines.append("    0   1   2   3   4   5   6")
        lines.append("  +" + "---+" * 7)
        
        for row in range(BoardConstants.ROWS):
            row_str = f"{row} |"
            for col in range(BoardConstants.COLS):
                pos = Position(row, col)
                piece = self.get_piece(pos)
                
                if piece:
                    cell = f"{piece.to_code():3}"
                elif BoardConstants.is_river(pos):
                    cell = " ~ "
                elif BoardConstants.is_den(pos):
                    cell = " X "
                elif (row, col) in BoardConstants.BLUE_TRAPS or (row, col) in BoardConstants.RED_TRAPS:
                    cell = " # "
                else:
                    cell = "   "
                
                row_str += cell + "|"
            lines.append(row_str)
            lines.append("  +" + "---+" * 7)
        
        return "\n".join(lines)
