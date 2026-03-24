import { motion, AnimatePresence } from 'framer-motion';
import { useTranslation } from 'react-i18next';
import { useGameStore } from '@/stores';
import { PIECE_EMOJIS } from '@/types';
import type { GameMove } from '@/types';

interface MoveHistoryProps {
  className?: string;
  maxMoves?: number;
}

export function MoveHistory({ className = '', maxMoves = 20 }: MoveHistoryProps) {
  const { t } = useTranslation();
  const moveHistory = useGameStore(state => state.moveHistory);
  const myColor = useGameStore(state => state.myColor);
  
  // Take only last N moves
  const displayMoves = moveHistory.slice(-maxMoves);

  return (
    <div className={`bg-white rounded-2xl shadow-cute overflow-hidden ${className}`}>
      {/* Header */}
      <div className="px-4 py-3 bg-gradient-to-r from-sky-blue to-sky-blue-light">
        <h3 className="font-display text-white flex items-center gap-2">
          <span>📜</span>
          {t('game.moveHistory')}
        </h3>
      </div>
      
      {/* Move list */}
      <div className="max-h-60 overflow-y-auto scrollbar-hide p-2">
        {displayMoves.length === 0 ? (
          <p className="text-center text-forest-light py-4 text-sm">
            {t('game.waiting')}
          </p>
        ) : (
          <AnimatePresence initial={false}>
            {displayMoves.map((move, index) => (
              <MoveItem
                key={`${move.moveNumber}-${move.timestamp}`}
                move={move}
                isMyMove={move.piece?.owner === myColor}
                isNew={index === displayMoves.length - 1}
              />
            ))}
          </AnimatePresence>
        )}
      </div>
    </div>
  );
}

interface MoveItemProps {
  move: GameMove;
  isMyMove: boolean;
  isNew: boolean;
}

function MoveItem({ move, isMyMove, isNew }: MoveItemProps) {
  const emoji = move.piece ? PIECE_EMOJIS[move.piece.type] : '❓';
  const captureEmoji = move.capturedPiece ? PIECE_EMOJIS[move.capturedPiece.type] : null;
  
  const fromStr = `${String.fromCharCode(65 + move.from.col)}${9 - move.from.row}`;
  const toStr = `${String.fromCharCode(65 + move.to.col)}${9 - move.to.row}`;
  
  return (
    <motion.div
      initial={isNew ? { opacity: 0, x: -20 } : false}
      animate={{ opacity: 1, x: 0 }}
      className={`
        flex items-center gap-2 p-2 rounded-xl mb-1
        ${isMyMove ? 'bg-sky-blue/10' : 'bg-player-red/10'}
      `}
    >
      {/* Move number */}
      <span className="text-xs text-forest-light w-6">
        {move.moveNumber}.
      </span>
      
      {/* Piece emoji */}
      <span className="text-lg">{emoji}</span>
      
      {/* Move notation */}
      <span className="text-sm font-medium">
        {fromStr} → {toStr}
      </span>
      
      {/* Capture indicator */}
      {captureEmoji && (
        <span className="ml-auto flex items-center gap-1 text-player-red">
          <span className="text-xs">×</span>
          <span className="text-lg">{captureEmoji}</span>
        </span>
      )}
    </motion.div>
  );
}

export default MoveHistory;
