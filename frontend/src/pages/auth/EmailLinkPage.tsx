import { useEffect, useState } from 'react';
import { Link, useLocation, useNavigate } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { AuthLayout } from './AuthLayout';
import { CuteButton } from '@/components/ui';
import { routes } from '@/router/routes';
import { navigateAfterAuth } from '@/utils';
import { useAuthStore } from '@/stores';

export function EmailLinkPage() {
  const { t } = useTranslation();
  const location = useLocation();
  const navigate = useNavigate();
  const completeLoginWithLink = useAuthStore(state => state.completeLoginWithLink);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    const emailForSignIn = window.localStorage.getItem('emailForSignIn');
    if (!emailForSignIn) {
      navigate(routes.login, { replace: true });
      return;
    }
    const email = emailForSignIn;

    let cancelled = false;

    async function completeSignIn() {
      try {
        await completeLoginWithLink(email, window.location.href);
        if (!cancelled) {
          navigateAfterAuth();
        }
      } catch (err: unknown) {
        if (!cancelled) {
          setError(err instanceof Error ? err.message : t('common.error'));
        }
      }
    }

    const searchParams = new URLSearchParams(location.search);
    if (searchParams.has('apiKey') && searchParams.has('oobCode')) {
      void completeSignIn();
      return () => {
        cancelled = true;
      };
    }

    navigate(routes.login, { replace: true });
    return () => {
      cancelled = true;
    };
  }, [completeLoginWithLink, location.search, navigate, t]);

  return (
    <AuthLayout
      title={t('auth.login')}
      subtitle={t('common.loading')}
    >
      {error ? (
        <div className="space-y-4 text-center">
          <div className="rounded-xl bg-player-red/10 p-3 text-sm font-bold text-player-red">
            {error}
          </div>
          <CuteButton fullWidth onClick={() => navigate(routes.login, { replace: true })}>
            {t('auth.backToLogin', 'Back to Login')}
          </CuteButton>
        </div>
      ) : (
        <div className="space-y-4 text-center">
          <p className="text-sm text-forest-light">{t('common.loading')}</p>
          <Link className="text-sm font-bold text-candy-green hover:underline" to={routes.login} replace>
            {t('auth.backToLogin', 'Back to Login')}
          </Link>
        </div>
      )}
    </AuthLayout>
  );
}

export default EmailLinkPage;
