import { forwardRef, type ButtonHTMLAttributes, type ReactNode } from 'react';
import { motion, type HTMLMotionProps } from 'framer-motion';

type ButtonVariant = 'primary' | 'secondary' | 'accent' | 'ghost' | 'danger';
type ButtonSize = 'sm' | 'md' | 'lg';

interface CuteButtonProps extends Omit<ButtonHTMLAttributes<HTMLButtonElement>, 'ref'> {
  variant?: ButtonVariant;
  size?: ButtonSize;
  isLoading?: boolean;
  leftIcon?: ReactNode;
  rightIcon?: ReactNode;
  fullWidth?: boolean;
  children: ReactNode;
}

const variantStyles: Record<ButtonVariant, string> = {
  primary: `
    bg-candy-green text-white 
    border-b-4 border-candy-green-dark
    hover:bg-candy-green-light
    active:border-b-2 active:translate-y-[2px]
  `,
  secondary: `
    bg-sky-blue text-white 
    border-b-4 border-sky-blue-dark
    hover:bg-sky-blue-light
    active:border-b-2 active:translate-y-[2px]
  `,
  accent: `
    bg-carrot-orange text-white 
    border-b-4 border-carrot-orange-dark
    hover:bg-carrot-orange-light
    active:border-b-2 active:translate-y-[2px]
  `,
  ghost: `
    bg-transparent text-forest-dark 
    border-2 border-forest-light
    hover:bg-cream-dark
    active:bg-cream-dark
  `,
  danger: `
    bg-player-red text-white 
    border-b-4 border-player-red-dark
    hover:bg-player-red-light
    active:border-b-2 active:translate-y-[2px]
  `,
};

const sizeStyles: Record<ButtonSize, string> = {
  sm: 'px-4 py-2 text-sm rounded-xl',
  md: 'px-6 py-3 text-base rounded-2xl',
  lg: 'px-8 py-4 text-lg rounded-2xl',
};

// Animation variants for hover/tap
const buttonMotion: HTMLMotionProps<'button'> = {
  whileHover: { scale: 1.02 },
  whileTap: { scale: 0.98 },
  transition: { type: 'spring', stiffness: 400, damping: 17 },
};

export const CuteButton = forwardRef<HTMLButtonElement, CuteButtonProps>(
  (
    {
      variant = 'primary',
      size = 'md',
      isLoading = false,
      leftIcon,
      rightIcon,
      fullWidth = false,
      children,
      className = '',
      disabled,
      ...props
    },
    ref
  ) => {
    const isDisabled = disabled || isLoading;

    return (
      <motion.button
        ref={ref}
        className={`
          inline-flex items-center justify-center gap-2
          font-display font-bold tracking-wide
          transition-all duration-150 ease-out
          disabled:opacity-50 disabled:cursor-not-allowed disabled:transform-none
          ${variantStyles[variant]}
          ${sizeStyles[size]}
          ${fullWidth ? 'w-full' : ''}
          ${className}
        `}
        disabled={isDisabled}
        {...buttonMotion}
        {...(props as HTMLMotionProps<'button'>)}
      >
        {isLoading ? (
          <LoadingSpinner />
        ) : (
          <>
            {leftIcon && <span className="flex-shrink-0">{leftIcon}</span>}
            <span>{children}</span>
            {rightIcon && <span className="flex-shrink-0">{rightIcon}</span>}
          </>
        )}
      </motion.button>
    );
  }
);

CuteButton.displayName = 'CuteButton';

// Loading spinner component
function LoadingSpinner() {
  return (
    <svg
      className="animate-spin h-5 w-5"
      xmlns="http://www.w3.org/2000/svg"
      fill="none"
      viewBox="0 0 24 24"
    >
      <circle
        className="opacity-25"
        cx="12"
        cy="12"
        r="10"
        stroke="currentColor"
        strokeWidth="4"
      />
      <path
        className="opacity-75"
        fill="currentColor"
        d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4zm2 5.291A7.962 7.962 0 014 12H0c0 3.042 1.135 5.824 3 7.938l3-2.647z"
      />
    </svg>
  );
}

export default CuteButton;
