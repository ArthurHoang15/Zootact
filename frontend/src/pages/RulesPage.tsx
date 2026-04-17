import { useTranslation } from 'react-i18next';
import { useNavigate } from 'react-router-dom';
import { RulesGuide } from '@/components/game';
import { CuteButton, LanguageSwitcher } from '@/components/ui';
import { routes } from '@/router/routes';

export function RulesPage() {
    const { t } = useTranslation();
    const navigate = useNavigate();

    return (
        <div className="min-h-screen bg-cream">
            <header className="relative overflow-hidden bg-gradient-to-br from-sky-blue via-candy-green to-cream px-4 py-8">
                <div className="mx-auto flex max-w-6xl items-start justify-between gap-4">
                    <div>
                        <button className="text-sm font-bold text-white/90" onClick={() => navigate(routes.home)}>
                            {t('common.back')}
                        </button>
                        <h1 className="mt-3 font-display text-5xl text-white">{t('rules.pageTitle')}</h1>
                        <p className="mt-2 max-w-3xl text-white/90">{t('rules.pageSubtitle')}</p>
                    </div>
                    <LanguageSwitcher />
                </div>
            </header>

            <main className="mx-auto max-w-6xl px-4 py-10">
                <RulesGuide
                    title={t('rules.pageBodyTitle')}
                    subtitle={t('rules.pageBodySubtitle')}
                    actions={(
                        <CuteButton variant="ghost" onClick={() => navigate(routes.home)}>
                            {t('rules.backToHome')}
                        </CuteButton>
                    )}
                />
            </main>
        </div>
    );
}

export default RulesPage;
