import { useTranslation } from 'react-i18next';
import { useAuthStore, useGameStore } from '@/stores';
import { Avatar } from '@/components/ui';
import { Timer } from './Timer';

interface PlayerInfoProps {
  player: 'me' | 'opponent';
  className?: string;
}

export function PlayerInfo({ player, className = '' }: PlayerInfoProps) {
  const { t } = useTranslation();
  const myColor = useGameStore(state => state.myColor);
  const currentTurn = useGameStore(state => state.currentTurn);
  const opponent = useGameStore(state => state.opponent);
  const user = useAuthStore(state => state.user);

  const isOpponent = player === 'opponent';
  const username = isOpponent ? opponent?.username ?? t('game.waiting') : user?.username ?? 'You';
  const avatarUrl = isOpponent ? opponent?.avatar_url : user?.avatar_url ?? null;
  const forestPoints = isOpponent ? opponent?.forest_points : user?.forest_points ?? 1200;
  
  const playerColor = isOpponent 
    ? (myColor === 'Blue' ? 'Red' : 'Blue')
    : myColor;
  
  const isActive = currentTurn === playerColor;
  
  const colorBadge = playerColor === 'Blue'
    ? 'bg-player-blue'
    : 'bg-player-red';

  return (
    <div className={`
      flex items-center gap-3 p-3
      bg-white rounded-2xl shadow-cute
      ${isActive ? 'ring-2 ring-candy-green' : ''}
      ${className}
    `}>
      {/* Avatar with color indicator */}
      <div className="relative">
        <Avatar
          src={avatarUrl}
          alt={username}
          avatarSize="md"
          online={true}
        />
        <span className={`
          absolute -bottom-1 -right-1
          w-4 h-4 rounded-full
          ${colorBadge}
          ring-2 ring-white
        `} />
      </div>
      
      {/* Info */}
      <div className="flex-1 min-w-0">
        <p className="font-display text-lg truncate">
          {username}
        </p>
        <p className="text-sm text-forest-light flex items-center gap-1">
          <span>🌿</span>
          {forestPoints}
        </p>
      </div>
      
      {/* Timer */}
      <Timer player={player} />
    </div>
  );
}

export default PlayerInfo;
