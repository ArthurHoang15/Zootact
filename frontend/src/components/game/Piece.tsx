import { memo } from 'react';
import { motion, AnimatePresence, type Variants } from 'framer-motion';
import { useTranslation } from 'react-i18next';
import type { PieceDto } from '@/types';
import { PIECE_EMOJIS } from '@/types';

interface PieceProps {
  piece: PieceDto;
  isSelected: boolean;
  isMyPiece: boolean;
  canSelect: boolean;
  onSelect: () => void;
}

// Spring animation for bouncy piece movement
const pieceVariants: Variants = {
  initial: { scale: 0, rotate: -180 },
  enter: { 
    scale: 1, 
    rotate: 0,
    transition: { 
      type: 'spring', 
      stiffness: 260, 
      damping: 20 
    }
  },
  exit: { 
    scale: 0, 
    opacity: 0,
    transition: { duration: 0.2 }
  },
  selected: {
    scale: 1.1,
    y: [0, -8, 0],
    transition: {
      scale: {
        type: 'spring',
        stiffness: 400,
        damping: 10,
      },
      y: {
        duration: 0.5,
        repeat: Infinity,
        repeatType: 'reverse' as const,
        ease: 'easeInOut',
      }
    }
  },
  hover: {
    scale: 1.05,
    transition: {
      type: 'spring',
      stiffness: 400,
      damping: 17,
    }
  },
  tap: {
    scale: 0.95,
  },
};



function PieceComponent({ 
  piece, 
  isSelected, 
  isMyPiece, 
  canSelect,
  onSelect 
}: PieceProps) {
  const { t } = useTranslation();
  const emoji = PIECE_EMOJIS[piece.type];
  const isBlue = piece.owner === 'Blue';
  const pieceName = t(`pieces.${piece.type}`, piece.type);
  
  // Color styling based on owner
  const ownerStyles = isBlue
    ? 'bg-gradient-to-br from-player-blue-light to-player-blue border-player-blue-dark'
    : 'bg-gradient-to-br from-player-red-light to-player-red border-player-red-dark';
  
  // Selection ring
  const selectionStyles = isSelected
    ? 'ring-4 ring-candy-green ring-offset-2 ring-offset-cream'
    : '';
  
  // Interactive styles
  const interactiveStyles = canSelect && isMyPiece
    ? 'cursor-pointer hover:ring-2 hover:ring-candy-green/50'
    : 'cursor-default';

  return (
    <AnimatePresence mode="wait">
      <motion.div
        key={`${piece.type}-${piece.owner}`}
        className={`
          w-[85%] h-[85%] rounded-full
          flex items-center justify-center
          border-b-4 shadow-cute
          ${ownerStyles}
          ${selectionStyles}
          ${interactiveStyles}
          select-none
        `}
        variants={pieceVariants}
        initial="initial"
        animate={isSelected ? 'selected' : 'enter'}
        exit="exit"
        whileHover={canSelect && isMyPiece ? 'hover' : undefined}
        whileTap={canSelect && isMyPiece ? 'tap' : undefined}
        title={pieceName}
        onClick={(e) => {
          if (canSelect && isMyPiece) {
            e.stopPropagation();
            onSelect();
          }
        }}

      >
        {/* Piece emoji */}
        <span 
          className="text-2xl sm:text-3xl md:text-4xl filter drop-shadow-md"
          role="img"
          aria-label={pieceName}
        >
          {emoji}
        </span>
        
        {/* Rank indicator */}
        <span 
          className={`
            absolute -bottom-1 -right-1
            w-5 h-5 rounded-full
            flex items-center justify-center
            text-xs font-bold text-white
            shadow-sm
            ${isBlue ? 'bg-player-blue-dark' : 'bg-player-red-dark'}
          `}
        >
          {piece.rank}
        </span>
        
        {/* Selection glow effect */}
        {isSelected && (
          <motion.div
            className="absolute inset-0 rounded-full bg-candy-green/20"
            animate={{
              scale: [1, 1.2, 1],
              opacity: [0.5, 0.2, 0.5],
            }}
            transition={{
              duration: 1,
              repeat: Infinity,
              ease: 'easeInOut',
            }}
          />
        )}
      </motion.div>
    </AnimatePresence>
  );
}

export const Piece = memo(PieceComponent);
export default Piece;
