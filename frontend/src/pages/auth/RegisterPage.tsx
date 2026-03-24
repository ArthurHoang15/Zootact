import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { AuthLayout } from './AuthLayout';
import { CuteInput, CuteButton } from '@/components/ui';
import { useAuthStore } from '@/stores';

export function RegisterPage() {
  const { t } = useTranslation();
  const [formData, setFormData] = useState({
    username: '',
    email: '',
    password: '',
    confirmPassword: '',
  });
  const [validationError, setValidationError] = useState('');
  
  const registerWithEmail = useAuthStore(state => state.registerWithEmail);
  const isLoading = useAuthStore(state => state.isLoading);
  const storeError = useAuthStore(state => state.error);
  const resetError = useAuthStore(state => state.resetError);

  const handleChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    setFormData(prev => ({ ...prev, [e.target.name]: e.target.value }));
    if (validationError) setValidationError('');
    resetError();
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setValidationError('');

    if (formData.password !== formData.confirmPassword) {
      setValidationError("Passwords don't match!");
      return;
    }

    try {
      await registerWithEmail(formData.email, formData.password, formData.username);
      window.location.hash = '#/';
    } catch (err) {
      // Error is set in store
    }
  };

  const displayError = validationError || storeError;

  return (
    <AuthLayout 
      title={t('auth.register', 'Create Account')} 
      subtitle={t('auth.registerSubtitle', 'Join the Zootact community!')}
    >
      <form onSubmit={handleSubmit} className="flex flex-col gap-4">
        <CuteInput
          label="Username"
          name="username"
          value={formData.username}
          onChange={handleChange}
          required
          placeholder="CoolLion123"
        />
        
        <CuteInput
          label="Email"
          name="email"
          type="email"
          value={formData.email}
          onChange={handleChange}
          required
          placeholder="owl@zootact.com"
        />
        
        <CuteInput
          label="Password"
          name="password"
          type="password"
          value={formData.password}
          onChange={handleChange}
          required
          placeholder="••••••••"
        />
        
        <CuteInput
          label="Confirm Password"
          name="confirmPassword"
          type="password"
          value={formData.confirmPassword}
          onChange={handleChange}
          required
          placeholder="••••••••"
        />

        {displayError && (
          <div className="bg-player-red/10 text-player-red p-3 rounded-xl text-sm font-bold text-center">
            {displayError}
          </div>
        )}

        <CuteButton 
          type="submit" 
          variant="primary" 
          fullWidth 
          isLoading={isLoading}
          className="mt-2"
        >
          {t('auth.registerButton', 'Sign Up')}
        </CuteButton>

        <div className="text-center mt-4">
          <p className="text-sm text-forest-light">
            {t('auth.hasAccount', "Already have an account?")}{' '}
            <a href="#/login" className="text-candy-green hover:underline font-bold">
              {t('auth.loginLink', 'Login')}
            </a>
          </p>
        </div>
      </form>
    </AuthLayout>
  );
}

export default RegisterPage;
