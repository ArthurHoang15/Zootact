import { useEffect, useRef } from 'react';
import { motion } from 'framer-motion';
import { useGameStore } from '@/stores';
import { formatGameTime } from '@/utils';


interface TimerProps {
  player: 'me' | 'opponent';
  className?: string;
}

export function Timer({ player, className = '' }: TimerProps) {
  const myTimeMs = useGameStore(state => state.myTimeMs);
  const opponentTimeMs = useGameStore(state => state.opponentTimeMs);
  const currentTurn = useGameStore(state => state.currentTurn);
  const myColor = useGameStore(state => state.myColor);
  const isGameOver = useGameStore(state => state.isGameOver);
  const isOpponentDisconnected = useGameStore(state => state.isOpponentDisconnected);
  const tickTime = useGameStore(state => state.tickTime);
  
  const timeMs = player === 'me' ? myTimeMs : opponentTimeMs;
  const isActive = !isGameOver && !isOpponentDisconnected && (
    player === 'me' 
      ? currentTurn === myColor 
      : currentTurn !== myColor
  );
  
  // Timer tick logic
  const lastTickRef = useRef<number>(Date.now());
  
  useEffect(() => {
    if (!isActive) {
      lastTickRef.current = Date.now();
      return;
    }
    
    const interval = setInterval(() => {
      const now = Date.now();
      const delta = now - lastTickRef.current;
      lastTickRef.current = now;
      tickTime(delta);
    }, 100);
    
    return () => clearInterval(interval);
  }, [isActive, tickTime]);
  
  // Determine styling based on time remaining
  const isLowTime = timeMs < 30000;
  const isCriticalTime = timeMs < 10000;
  
  const bgStyle = isActive
    ? isCriticalTime 
      ? 'bg-player-red animate-pulse' 
      : isLowTime 
        ? 'bg-carrot-orange'
        : 'bg-candy-green'
    : 'bg-forest-light';
  
  return (
    <motion.div
      className={`
        px-4 py-2 rounded-2xl
        font-display text-xl sm:text-2xl text-white
        shadow-cute min-w-[100px] text-center
        ${bgStyle}
        ${className}
      `}
      animate={isActive && isCriticalTime ? {
        scale: [1, 1.02, 1],
      } : undefined}
      transition={isActive && isCriticalTime ? {
        duration: 0.5,
        repeat: Infinity,
      } : undefined}
    >
      {formatGameTime(timeMs)}
    </motion.div>
  );
}

export default Timer;
