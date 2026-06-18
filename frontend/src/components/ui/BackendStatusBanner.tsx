import { useTranslation } from 'react-i18next';
import { useAuthStore } from '@/stores';

export function BackendStatusBanner() {
  const { t } = useTranslation();
  const backendStatus = useAuthStore(state => state.backendStatus);
  const backendStatusMessage = useAuthStore(state => state.backendStatusMessage);

  if (backendStatus === 'healthy') {
    return null;
  }

  const isRecovering = backendStatus === 'recovering';

  return (
    <div
      className={`sticky top-0 z-50 border-b px-4 py-3 text-sm shadow-sm ${
        isRecovering
          ? 'border-sky-blue-dark/20 bg-sky-blue/15 text-forest-dark'
          : 'border-carrot-orange-dark/20 bg-carrot-orange/15 text-forest-dark'
      }`}
      role="status"
      aria-live="polite"
    >
      <div className="mx-auto flex max-w-6xl items-center gap-3">
        <span className="font-bold">
          {isRecovering ? t('status.recoveringTitle') : t('status.degradedTitle')}
        </span>
        <span className="text-forest-dark/80">
          {isRecovering
            ? t('status.recoveringBody')
            : backendStatusMessage || t('status.degradedBody')}
        </span>
      </div>
    </div>
  );
}
