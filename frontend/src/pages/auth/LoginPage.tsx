import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Link } from 'react-router-dom';
import { AuthLayout } from './AuthLayout';
import { CuteInput, CuteButton } from '@/components/ui';
import { routes } from '@/router/routes';
import { useAuthStore } from '@/stores';
import { navigateAfterAuth } from '@/utils';

export function LoginPage() {
  const { t } = useTranslation();
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  
  const loginWithEmail = useAuthStore(state => state.loginWithEmail);
  const loginWithGoogle = useAuthStore(state => state.loginWithGoogle);
  const sendLoginLink = useAuthStore(state => state.sendLoginLink);
  const isLoading = useAuthStore(state => state.isLoading);
  const error = useAuthStore(state => state.error);
  const resetError = useAuthStore(state => state.resetError);
  
  const [magicLinkSent, setMagicLinkSent] = useState(false);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    try {
      await loginWithEmail(email, password);
      navigateAfterAuth();
    } catch {
      // Error is set in store
    }
  };

  const handleGoogleLogin = async () => {
    try {
      await loginWithGoogle();
      navigateAfterAuth();
    } catch {
      // Error is set in store
    }
  };

  const handleSendLink = async () => {
    if (!email) {
       alert(t('auth.enterEmailFirst', 'Please enter your email first!'));
       return;
    }
    try {
       await sendLoginLink(email);
       setMagicLinkSent(true);
    } catch {
       // Store handles error
    }
  };

  return (
    <AuthLayout 
      title={t('auth.login')} 
      subtitle={t('auth.loginSubtitle', 'Welcome back to the forest!')}
    >
      <form onSubmit={handleSubmit} className="flex flex-col gap-4">
        <CuteInput
          label={t('auth.email', 'Email')}
          type="email"
          value={email}
          onChange={(e) => { setEmail(e.target.value); resetError(); }}
          required
          placeholder="owl@zootact.com"
        />
        
        <div>
          <CuteInput
            label={t('auth.password', 'Password')}
            type="password"
            value={password}
            onChange={(e) => { setPassword(e.target.value); resetError(); }}
            required
            placeholder="••••••••"
          />
          <div className="flex justify-end mt-1">
            <Link to={routes.forgotPassword} className="text-xs text-sky-blue hover:underline font-bold">
              {t('auth.forgotPassword', 'Forgot Password?')}
            </Link>
          </div>
        </div>

        {error && (
          <div className="bg-player-red/10 text-player-red p-3 rounded-xl text-sm font-bold text-center">
            {error}
          </div>
        )}

        {magicLinkSent && (
            <div className="bg-candy-green/10 text-candy-green p-3 rounded-xl text-sm font-bold text-center">
            {t('auth.linkSentInfo', 'Check your email for the login link!')}
            </div>
        )}

        <CuteButton 
          type="submit" 
          variant="primary" 
          fullWidth 
          isLoading={isLoading}
          className="mt-2"
        >
          {t('auth.loginButton', 'Login')}
        </CuteButton>
        
        <CuteButton 
          type="button" 
          variant="accent" 
          fullWidth 
          onClick={handleSendLink}
          isLoading={isLoading}
          className="mt-2"
          disabled={magicLinkSent}
        >
          {magicLinkSent ? t('auth.linkSent', 'Link Sent! Check Email') : t('auth.sendMagicLink', 'Send Login Link')}
        </CuteButton>

        <div className="relative flex items-center py-2">
          <div className="flex-grow border-t border-forest-light/20"></div>
          <span className="flex-shrink-0 mx-4 text-forest-light/60 text-xs font-bold uppercase">
            {t('auth.or', 'Or')}
          </span>
          <div className="flex-grow border-t border-forest-light/20"></div>
        </div>

        <CuteButton 
          type="button"
          variant="secondary" 
          fullWidth 
          onClick={handleGoogleLogin}
          isLoading={isLoading}
          leftIcon={<span className="text-lg">G</span>}
        >
          {t('auth.googleLogin', 'Continue with Google')}
        </CuteButton>

        <div className="text-center mt-4">
          <p className="text-sm text-forest-light">
            {t('auth.noAccount', "Don't have an account?")}{' '}
            <Link to={routes.register} className="text-candy-green hover:underline font-bold">
              {t('auth.registerLink', 'Join Now')}
            </Link>
          </p>
        </div>
      </form>
    </AuthLayout>
  );
}

export default LoginPage;
