import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { sendPasswordResetEmail } from 'firebase/auth';
import { auth } from '@/config/firebase';
import { AuthLayout } from './AuthLayout';
import { CuteInput, CuteButton } from '@/components/ui';

export function ForgotPasswordPage() {
  const { t } = useTranslation();
  const [email, setEmail] = useState('');
  const [isLoading, setIsLoading] = useState(false);
  const [message, setMessage] = useState('');
  const [error, setError] = useState('');

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setIsLoading(true);
    setError('');
    setMessage('');

    try {
      await sendPasswordResetEmail(auth, email);
      setMessage(t('auth.emailSentDesc', 'If an account exists, we sent a reset link!'));
    } catch (err: any) {
      setError(err.message || t('common.error'));
    } finally {
      setIsLoading(false);
    }
  };

  return (
    <AuthLayout 
      title={t('auth.forgotPassword')} 
      subtitle={t('auth.forgotPasswordSubtitle', "Don't worry, we'll help you recover it.")}
    >
      {message ? (
        <div className="text-center py-8">
          <div className="text-4xl mb-4">📧</div>
          <p className="text-forest-dark font-medium mb-4">{message}</p>
          <a href="#/login" className="text-candy-green font-bold hover:underline">
            {t('auth.backToLogin', 'Back to Login')}
          </a>
        </div>
      ) : (
        <form onSubmit={handleSubmit} className="flex flex-col gap-4">
          <p className="text-sm text-forest-light mb-2">
            {t('auth.enterEmail', "Enter your email address and we'll send you a link to reset your password.")}
          </p>
          
          <CuteInput
            label={t('auth.email')}
            type="email"
            value={email}
            onChange={(e) => setEmail(e.target.value)}
            required
            placeholder="owl@zootact.com"
          />

          {error && (
            <div className="bg-player-red/10 text-player-red p-3 rounded-xl text-sm font-bold text-center">
              {error}
            </div>
          )}

          <CuteButton 
            type="submit" 
            variant="primary" 
            fullWidth 
            isLoading={isLoading}
          >
            {t('auth.sendResetLink', 'Send Reset Link')}
          </CuteButton>

          <div className="text-center mt-4">
            <a href="#/login" className="text-sm text-forest-light hover:text-candy-green hover:underline font-bold">
              {t('auth.backToLogin', 'Back to Login')}
            </a>
          </div>
        </form>
      )}
    </AuthLayout>
  );
}

export default ForgotPasswordPage;
