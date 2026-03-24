import { type ReactNode, forwardRef } from 'react';
import { motion, type HTMLMotionProps } from 'framer-motion';

interface CardProps extends Omit<HTMLMotionProps<'div'>, 'ref'> {
  variant?: 'default' | 'elevated' | 'outlined' | 'glass';
  padding?: 'none' | 'sm' | 'md' | 'lg';
  hover?: boolean;
  children: ReactNode;
}

const variantStyles = {
  default: 'bg-white shadow-cute',
  elevated: 'bg-white shadow-cute-lg',
  outlined: 'bg-transparent border-2 border-forest-light/20',
  glass: 'bg-white/70 backdrop-blur-sm shadow-cute',
};

const paddingStyles = {
  none: '',
  sm: 'p-3',
  md: 'p-5',
  lg: 'p-7',
};

export const Card = forwardRef<HTMLDivElement, CardProps>(
  (
    {
      variant = 'default',
      padding = 'md',
      hover = false,
      children,
      className = '',
      ...props
    },
    ref
  ) => {
    const motionProps = hover
      ? {
          whileHover: { y: -4, boxShadow: '0 8px 16px rgba(0, 0, 0, 0.15)' },
          transition: { type: 'spring', stiffness: 400, damping: 17 },
        }
      : {};

    return (
      <motion.div
        ref={ref}
        className={`
          rounded-3xl
          ${variantStyles[variant]}
          ${paddingStyles[padding]}
          ${className}
        `}
        {...motionProps}
        {...(props as any)}
      >
        {children}
      </motion.div>
    );
  }
);

Card.displayName = 'Card';

// Card sub-components for composition
interface CardHeaderProps {
  children: ReactNode;
  className?: string;
}

export function CardHeader({ children, className = '' }: CardHeaderProps) {
  return (
    <div className={`font-display text-lg mb-3 ${className}`}>
      {children}
    </div>
  );
}

interface CardContentProps {
  children: ReactNode;
  className?: string;
}

export function CardContent({ children, className = '' }: CardContentProps) {
  return <div className={className}>{children}</div>;
}

interface CardFooterProps {
  children: ReactNode;
  className?: string;
}

export function CardFooter({ children, className = '' }: CardFooterProps) {
  return (
    <div className={`mt-4 pt-4 border-t border-forest-light/10 ${className}`}>
      {children}
    </div>
  );
}

export default Card;
