import { memo } from 'react';
import { motion, type Variants } from 'framer-motion';
import type { PieceDto, TerrainType, Player } from '@/types';
import { getTerrainType, isDenCell } from '@/types';
import { Piece } from './Piece';

interface CellProps {
  row: number;
  col: number;
  piece: PieceDto | null;
  isSelected: boolean;
  isValidMove: boolean;
  isLastMoveFrom: boolean;
  isLastMoveTo: boolean;
  myColor: Player | null;
  canSelect: boolean;
  onCellClick: () => void;
  onPieceSelect: () => void;
}

// Terrain type styling
const terrainStyles: Record<TerrainType, string> = {
  Normal: 'bg-land-light',
  River: 'river-wave',
  Trap: 'bg-trap',
  Den: '', // Will be set based on owner
};

// Valid move highlight animation
const validMoveVariants: Variants = {
  initial: { scale: 0.8, opacity: 0 },
  animate: { 
    scale: 1, 
    opacity: 1,
    transition: {
      type: 'spring',
      stiffness: 300,
      damping: 20,
    }
  },
  pulse: {
    scale: [1, 1.1, 1],
    opacity: [0.7, 1, 0.7],
    transition: {
      duration: 1,
      repeat: Infinity,
      ease: 'easeInOut',
    }
  }
};

function CellComponent({
  row,
  col,
  piece,
  isSelected,
  isValidMove,
  isLastMoveFrom,
  isLastMoveTo,
  myColor,
  canSelect,
  onCellClick,
  onPieceSelect,
}: CellProps) {
  const terrain = getTerrainType(row, col);
  const denOwner = isDenCell(row, col);
  
  // Determine cell background
  let bgStyle = terrainStyles[terrain];
  if (terrain === 'Den') {
    bgStyle = denOwner === 'Blue' ? 'bg-den-blue' : 'bg-den-red';
  }
  
  // Checkerboard pattern for normal cells
  const isLightCell = (row + col) % 2 === 0;
  if (terrain === 'Normal') {
    bgStyle = isLightCell ? 'bg-land-light' : 'bg-land-dark';
  }
  
  // Last move highlight
  const lastMoveStyle = (isLastMoveFrom || isLastMoveTo)
    ? 'ring-2 ring-inset ring-carrot-orange/50'
    : '';
  
  // Interactive styles
  const interactiveStyle = isValidMove
    ? 'cursor-pointer'
    : piece && canSelect && piece.owner === myColor
      ? 'cursor-pointer'
      : 'cursor-default';

  return (
    <motion.div
      className={`
        relative flex items-center justify-center
        aspect-square
        border border-forest-light/10
        ${bgStyle}
        ${lastMoveStyle}
        ${interactiveStyle}
        transition-colors duration-200
      `}
      onClick={onCellClick}
      whileHover={isValidMove ? { backgroundColor: 'rgba(88, 204, 2, 0.2)' } : undefined}
      whileTap={isValidMove ? { scale: 0.95 } : undefined}
    >
      {/* Terrain decorations */}
      {terrain === 'River' && (
        <div className="absolute inset-0 overflow-hidden pointer-events-none">
          <WaveEffect />
        </div>
      )}
      
      {terrain === 'Trap' && (
        <div className="absolute inset-1 flex items-center justify-center pointer-events-none opacity-40">
          <span className="text-2xl">⚠️</span>
        </div>
      )}
      
      {terrain === 'Den' && (
        <div className="absolute inset-1 flex items-center justify-center pointer-events-none opacity-30">
          <span className="text-3xl">🏠</span>
        </div>
      )}
      
      {/* Valid move indicator */}
      {isValidMove && !piece && (
        <motion.div
          className="absolute w-1/3 h-1/3 rounded-full bg-candy-green/70 shadow-glow-green"
          variants={validMoveVariants}
          initial="initial"
          animate="pulse"
        />
      )}
      
      {/* Capture indicator (valid move with enemy piece) */}
      {isValidMove && piece && (
        <motion.div
          className="absolute inset-1 rounded-lg border-4 border-player-red/50 bg-player-red/10"
          variants={validMoveVariants}
          initial="initial"
          animate="animate"
        />
      )}
      
      {/* Piece */}
      {piece && (
        <Piece
          piece={piece}
          isSelected={isSelected}
          isMyPiece={piece.owner === myColor}
          canSelect={canSelect}
          onSelect={onPieceSelect}
        />
      )}
    </motion.div>
  );
}

// Animated wave effect for river cells
function WaveEffect() {
  return (
    <motion.div
      className="absolute inset-0"
      style={{
        background: `
          repeating-linear-gradient(
            45deg,
            transparent,
            transparent 10px,
            rgba(255, 255, 255, 0.1) 10px,
            rgba(255, 255, 255, 0.1) 20px
          )
        `,
      }}
      animate={{
        backgroundPosition: ['0% 0%', '100% 100%'],
      }}
      transition={{
        duration: 4,
        repeat: Infinity,
        ease: 'linear',
      }}
    />
  );
}

export const Cell = memo(CellComponent);
export default Cell;
