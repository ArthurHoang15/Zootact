import { motion, AnimatePresence } from 'framer-motion';
import { useTranslation } from 'react-i18next';
import { useGameStore } from '@/stores';
import { CuteButton, Card } from '@/components/ui';

interface GameEndModalProps {
  onRematch?: () => void;
  onNewGame?: () => void;
}

export function GameEndModal({ onRematch, onNewGame }: GameEndModalProps) {
  const { t } = useTranslation();
  // Use individual selectors to avoid infinite loop from object creation
  const isGameOver = useGameStore(state => state.isGameOver);
  const result = useGameStore(state => state.result);
  const endReason = useGameStore(state => state.endReason);
  const eloChange = useGameStore(state => state.eloChange);
  const myColor = useGameStore(state => state.myColor);
  
  if (!isGameOver) return null;
  
  // Determine if I won
  const isVictory = result === `${myColor}Wins`;
  const isDraw = result === 'Draw';
  
  // Result styling
  const resultConfig = isVictory
    ? {
        title: t('game.victory'),
        emoji: '🎉',
        bgClass: 'from-candy-green to-candy-green-light',
        textClass: 'text-candy-green',
      }
    : isDraw
      ? {
          title: t('game.draw'),
          emoji: '🤝',
          bgClass: 'from-carrot-orange to-carrot-orange-light',
          textClass: 'text-carrot-orange',
        }
      : {
          title: t('game.defeat'),
          emoji: '😢',
          bgClass: 'from-player-red to-player-red-light',
          textClass: 'text-player-red',
        };
  
  // ELO change display
  const eloDisplay = eloChange >= 0 
    ? `+${eloChange}` 
    : String(eloChange);
  const eloClass = eloChange >= 0 
    ? 'text-candy-green' 
    : 'text-player-red';

  return (
    <AnimatePresence>
      <motion.div
        className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-forest-dark/50 backdrop-blur-sm"
        initial={{ opacity: 0 }}
        animate={{ opacity: 1 }}
        exit={{ opacity: 0 }}
      >
        <motion.div
          initial={{ scale: 0.8, opacity: 0, y: 20 }}
          animate={{ scale: 1, opacity: 1, y: 0 }}
          exit={{ scale: 0.8, opacity: 0, y: 20 }}
          transition={{ type: 'spring', stiffness: 300, damping: 25 }}
        >
          <Card
            variant="elevated"
            padding="lg"
            className="max-w-sm w-full text-center"
          >
            {/* Result header */}
            <div className={`
              -mx-7 -mt-7 mb-6 p-6
              bg-gradient-to-r ${resultConfig.bgClass}
              rounded-t-3xl
            `}>
              <motion.span
                className="text-6xl block mb-2"
                animate={{ 
                  scale: [1, 1.1, 1],
                  rotate: [0, 5, -5, 0],
                }}
                transition={{ 
                  duration: 0.5,
                  delay: 0.3,
                }}
              >
                {resultConfig.emoji}
              </motion.span>
              <h2 className="font-display text-3xl text-white">
                {resultConfig.title}
              </h2>
            </div>
            
            {/* Reason */}
            {endReason && (
              <p className="text-forest-light mb-4">
                {t(`endReason.${endReason}`)}
              </p>
            )}
            
            {/* ELO change */}
            <div className="mb-6">
              <p className="text-sm text-forest-light mb-1">
                {t('home.forestPoints')}
              </p>
              <motion.p
                className={`font-display text-3xl ${eloClass}`}
                initial={{ scale: 0 }}
                animate={{ scale: 1 }}
                transition={{ delay: 0.5, type: 'spring' }}
              >
                {eloDisplay}
              </motion.p>
            </div>
            
            {/* Actions */}
            <div className="flex flex-col gap-3">
              {onRematch && (
                <CuteButton
                  variant="primary"
                  fullWidth
                  onClick={onRematch}
                >
                  {t('game.rematch')} 🔄
                </CuteButton>
              )}
              {onNewGame && (
                <CuteButton
                  variant="secondary"
                  fullWidth
                  onClick={onNewGame}
                >
                  {t('game.newGame')} 🎮
                </CuteButton>
              )}
            </div>
          </Card>
        </motion.div>
      </motion.div>
    </AnimatePresence>
  );
}

export default GameEndModal;
