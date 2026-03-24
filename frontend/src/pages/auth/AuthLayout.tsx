import { ReactNode } from 'react';
import { motion } from 'framer-motion';
import { Card } from '@/components/ui';


interface AuthLayoutProps {
  children: ReactNode;
  title: string;
  subtitle?: string;
}

export function AuthLayout({ children, title, subtitle }: AuthLayoutProps) {
  return (
    <div className="min-h-screen bg-cream flex items-center justify-center p-4 relative overflow-hidden">
      {/* Background decoration */}
      <div className="absolute inset-0 bg-gradient-to-br from-candy-green-light/20 to-sky-blue-light/20" />
      
      {/* Animated animals */}
      <motion.div 
        className="absolute top-10 left-10 text-6xl opacity-20 hidden md:block"
        animate={{ y: [0, -20, 0], rotate: [0, 10, -10, 0] }}
        transition={{ duration: 5, repeat: Infinity }}
      >
        🦁
      </motion.div>
      <motion.div 
        className="absolute bottom-10 right-10 text-6xl opacity-20 hidden md:block"
        animate={{ y: [0, -20, 0], rotate: [0, -10, 10, 0] }}
        transition={{ duration: 4, repeat: Infinity, delay: 1 }}
      >
        🐘
      </motion.div>

      <motion.div
        initial={{ opacity: 0, scale: 0.9, y: 20 }}
        animate={{ opacity: 1, scale: 1, y: 0 }}
        transition={{ type: 'spring', duration: 0.5 }}
        className="w-full max-w-md relative z-10"
      >
        <div className="text-center mb-6">
          <div className="text-6xl mb-2">🐾</div>
          <h1 className="font-display text-4xl text-forest-dark">Zootact</h1>
        </div>

        <Card variant="elevated" padding="lg">
          <div className="text-center mb-6">
            <h2 className="font-display text-2xl text-candy-green">{title}</h2>
            {subtitle && (
              <p className="text-forest-light text-sm mt-1">{subtitle}</p>
            )}
          </div>
          {children}
        </Card>
      </motion.div>
    </div>
  );
}

export default AuthLayout;
