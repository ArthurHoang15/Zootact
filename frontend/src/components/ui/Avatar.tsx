import { type ImgHTMLAttributes, useState } from 'react';
import { motion } from 'framer-motion';

type AvatarSize = 'xs' | 'sm' | 'md' | 'lg' | 'xl';

type AvatarProps = Omit<ImgHTMLAttributes<HTMLImageElement>, 'src' | 'alt'> & {
  src?: string | null;
  alt: string;
  avatarSize?: AvatarSize;
  fallbackEmoji?: string;
  online?: boolean;
  rank?: number;
};

const sizeStyles: Record<AvatarSize, { container: string; text: string; badge: string }> = {
  xs: { container: 'w-6 h-6', text: 'text-xs', badge: 'w-2 h-2' },
  sm: { container: 'w-8 h-8', text: 'text-sm', badge: 'w-2.5 h-2.5' },
  md: { container: 'w-12 h-12', text: 'text-lg', badge: 'w-3 h-3' },
  lg: { container: 'w-16 h-16', text: 'text-2xl', badge: 'w-4 h-4' },
  xl: { container: 'w-24 h-24', text: 'text-4xl', badge: 'w-5 h-5' },
};

// Generate a consistent color from username
function getColorFromString(str: string): string {
  const colors = [
    'bg-candy-green',
    'bg-sky-blue',
    'bg-carrot-orange',
    'bg-player-blue',
    'bg-player-red',
  ];
  let hash = 0;
  for (let i = 0; i < str.length; i++) {
    hash = str.charCodeAt(i) + ((hash << 5) - hash);
  }
  return colors[Math.abs(hash) % colors.length]!;
}

// Get initials from name
function getInitials(name: string): string {
  return name
    .split(' ')
    .map(word => word[0])
    .join('')
    .toUpperCase()
    .slice(0, 2);
}

export function Avatar({
  src,
  alt,
  avatarSize = 'md',
  fallbackEmoji = '🐾',
  online,
  rank,
  className = '',
  ...props
}: AvatarProps) {
  const [imageError, setImageError] = useState(false);
  const styles = sizeStyles[avatarSize];
  const showFallback = !src || imageError;
  const bgColor = getColorFromString(alt);
  const initials = getInitials(alt);

  return (
    <div className={`relative inline-flex ${className}`}>
      <motion.div
        className={`
          ${styles.container}
          rounded-full overflow-hidden
          ring-2 ring-white shadow-cute
          flex items-center justify-center
          ${showFallback ? bgColor : 'bg-cream'}
        `}
        whileHover={{ scale: 1.05 }}
        transition={{ type: 'spring', stiffness: 400, damping: 17 }}
      >
        {showFallback ? (
          <span className={`${styles.text} text-white font-display`}>
            {initials || fallbackEmoji}
          </span>
        ) : (
          <img
            src={src ?? undefined}
            alt={alt}
            className="w-full h-full object-cover"
            onError={() => setImageError(true)}
            {...props}
          />
        )}
      </motion.div>

      {/* Online indicator */}
      {online !== undefined && (
        <span
          className={`
            absolute bottom-0 right-0
            ${styles.badge}
            rounded-full ring-2 ring-white
            ${online ? 'bg-candy-green' : 'bg-forest-light'}
          `}
        />
      )}

      {/* Rank badge */}
      {rank !== undefined && (
        <span
          className={`
            absolute -bottom-1 -right-1
            px-1.5 py-0.5 text-xs font-bold
            bg-carrot-orange text-white
            rounded-full ring-2 ring-white
            shadow-cute
          `}
        >
          #{rank}
        </span>
      )}
    </div>
  );
}

export default Avatar;
