import { type InputHTMLAttributes, forwardRef } from 'react';

interface CuteInputProps extends InputHTMLAttributes<HTMLInputElement> {
  label?: string;
  error?: string;
  fullWidth?: boolean;
}

export const CuteInput = forwardRef<HTMLInputElement, CuteInputProps>(
  ({ label, error, fullWidth = false, className = '', ...props }, ref) => {
    return (
      <div className={`flex flex-col gap-2 ${fullWidth ? 'w-full' : ''} ${className}`}>
        {label && (
          <label className="font-display text-forest-dark text-sm ml-2">
            {label}
          </label>
        )}
        <input
          ref={ref}
          className={`
            px-4 py-3 rounded-2xl
            bg-white border-2 border-forest-light/20
            text-forest-dark placeholder:text-forest-light/50
            focus:outline-none focus:border-candy-green focus:ring-4 focus:ring-candy-green/20
            transition-all duration-200
            ${error ? 'border-player-red focus:border-player-red focus:ring-player-red/20' : ''}
            ${fullWidth ? 'w-full' : ''}
          `}
          {...props}
        />
        {error && (
          <p className="text-player-red text-xs font-bold ml-2">
            {error}
          </p>
        )}
      </div>
    );
  }
);

export default CuteInput;
