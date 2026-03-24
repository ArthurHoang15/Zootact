import { useCallback, useMemo } from 'react';
import { motion, AnimatePresence, type Variants } from 'framer-motion';
import { useTranslation } from 'react-i18next';
import { useGameStore, selectCanSelect } from '@/stores';
import { BOARD_ROWS, BOARD_COLS, type PositionDto } from '@/types';
import { isValidMoveDestination } from '@/utils';
import { Cell } from './Cell';

interface BoardProps {
  onMove?: (from: PositionDto, to: PositionDto) => void;
  className?: string;
}

// Board entrance animation
const boardVariants: Variants = {
  hidden: { opacity: 0, scale: 0.9 },
  visible: {
    opacity: 1,
    scale: 1,
    transition: {
      duration: 0.4,
      ease: 'easeOut',
      when: 'beforeChildren',
      staggerChildren: 0.01,
    },
  },
};

const cellVariants: Variants = {
  hidden: { opacity: 0, y: 10 },
  visible: { 
    opacity: 1, 
    y: 0,
    transition: { duration: 0.2 }
  },
};

export function Board({ onMove, className = '' }: BoardProps) {
  const { t } = useTranslation();
  
  // Game state from store
  const board = useGameStore(state => state.board);
  const myColor = useGameStore(state => state.myColor);
  const selectedPiece = useGameStore(state => state.selectedPiece);
  const validMoves = useGameStore(state => state.validMoves);
  const moveHistory = useGameStore(state => state.moveHistory);
  const canSelect = useGameStore(selectCanSelect);
  const selectPiece = useGameStore(state => state.selectPiece);
  const clearSelection = useGameStore(state => state.clearSelection);
  
  // Get last move for highlighting
  const lastMove = moveHistory.length > 0 
    ? moveHistory[moveHistory.length - 1] 
    : null;
  
  // Handle cell click (Tap-to-Move logic)
  const handleCellClick = useCallback((row: number, col: number) => {
    const position: PositionDto = { row, col };
    
    // If clicking a valid move destination, execute move
    if (selectedPiece && isValidMoveDestination(validMoves, position)) {
      onMove?.(selectedPiece, position);
      clearSelection();
      return;
    }
    
    // Otherwise, try to select piece at this position
    if (board) {
      const piece = board.cells[row]?.[col];
      if (piece && piece.owner === myColor && canSelect) {
        selectPiece(position);
      } else {
        // Clicked empty cell or enemy piece (not valid move) - clear selection
        clearSelection();
      }
    }
  }, [
    board, 
    myColor, 
    selectedPiece, 
    validMoves, 
    canSelect, 
    selectPiece, 
    clearSelection, 
    onMove
  ]);
  
  // Generate grid cells
  const cells = useMemo(() => {
    if (!board) return null;
    
    const gridCells = [];
    for (let row = 0; row < BOARD_ROWS; row++) {
      for (let col = 0; col < BOARD_COLS; col++) {
        const piece = board.cells[row]?.[col] ?? null;
        const isSelected = selectedPiece?.row === row && selectedPiece?.col === col;
        const isValidMove = isValidMoveDestination(validMoves, { row, col });
        const isLastMoveFrom = lastMove?.from.row === row && lastMove?.from.col === col;
        const isLastMoveTo = lastMove?.to.row === row && lastMove?.to.col === col;
        
        gridCells.push(
          <motion.div key={`${row}-${col}`} variants={cellVariants}>
            <Cell
              row={row}
              col={col}
              piece={piece}
              isSelected={isSelected}
              isValidMove={isValidMove}
              isLastMoveFrom={isLastMoveFrom}
              isLastMoveTo={isLastMoveTo}
              myColor={myColor}
              canSelect={canSelect}
              onCellClick={() => handleCellClick(row, col)}
              onPieceSelect={() => selectPiece({ row, col })}
            />
          </motion.div>
        );
      }
    }
    return gridCells;
  }, [
    board, 
    selectedPiece, 
    validMoves, 
    lastMove, 
    myColor, 
    canSelect, 
    handleCellClick,
    selectPiece
  ]);

  if (!board) {
    return (
      <div className={`flex items-center justify-center p-8 ${className}`}>
        <div className="text-center">
          <motion.div
            className="text-6xl mb-4"
            animate={{ 
              rotate: [0, 10, -10, 0],
              scale: [1, 1.1, 1]
            }}
            transition={{ 
              duration: 1.5, 
              repeat: Infinity,
              ease: 'easeInOut'
            }}
          >
            🐾
          </motion.div>
          <p className="font-display text-lg text-forest-light">
            {t('game.waiting')}
          </p>
        </div>
      </div>
    );
  }

  return (
    <div className={`relative ${className}`}>
      {/* Board container with cute styling */}
      <div className="p-2 sm:p-3 bg-gradient-to-br from-carrot-orange to-carrot-orange-dark rounded-3xl shadow-cute-lg">
        {/* Inner board border */}
        <div className="p-1 bg-forest-dark/20 rounded-2xl">
          {/* Grid */}
          <motion.div
            className="grid gap-0.5 sm:gap-1 rounded-xl overflow-hidden bg-forest-dark/30"
            style={{
              gridTemplateColumns: `repeat(${BOARD_COLS}, 1fr)`,
              gridTemplateRows: `repeat(${BOARD_ROWS}, 1fr)`,
            }}
            variants={boardVariants}
            initial="hidden"
            animate="visible"
          >
            <AnimatePresence mode="sync">
              {cells}
            </AnimatePresence>
          </motion.div>
        </div>
      </div>
      
      {/* Turn indicator overlay */}
      <TurnIndicator />
    </div>
  );
}

// Turn indicator component
function TurnIndicator() {
  const { t } = useTranslation();
  const currentTurn = useGameStore(state => state.currentTurn);
  const myColor = useGameStore(state => state.myColor);
  const isGameOver = useGameStore(state => state.isGameOver);
  
  if (isGameOver) return null;
  
  const isMyTurn = currentTurn === myColor;
  
  return (
    <motion.div
      className={`
        absolute -top-3 left-1/2 -translate-x-1/2
        px-4 py-1.5 rounded-full
        font-display text-sm text-white shadow-cute
        ${isMyTurn ? 'bg-candy-green' : 'bg-forest-light'}
      `}
      initial={{ y: -20, opacity: 0 }}
      animate={{ y: 0, opacity: 1 }}
      key={currentTurn}
    >
      {isMyTurn ? t('game.yourTurn') : t('game.opponentTurn')}
    </motion.div>
  );
}

export default Board;
