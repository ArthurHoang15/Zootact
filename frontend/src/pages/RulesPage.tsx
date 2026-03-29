import { useTranslation } from 'react-i18next';
import { RulesGuide } from '@/components/game';
import { CuteButton, LanguageSwitcher } from '@/components/ui';

export function RulesPage() {
    const { t } = useTranslation();

    return (
        <div className="min-h-screen bg-cream">
            <header className="relative overflow-hidden bg-gradient-to-br from-sky-blue via-candy-green to-cream px-4 py-8">
                <div className="mx-auto flex max-w-6xl items-start justify-between gap-4">
                    <div>
                        <button className="text-sm font-bold text-white/90" onClick={() => { window.location.hash = '#/'; }}>
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
                        <CuteButton variant="ghost" onClick={() => { window.location.hash = '#/'; }}>
                            {t('rules.backToHome')}
                        </CuteButton>
                    )}
                />
            </main>
        </div>
    );
}

export default RulesPage;
