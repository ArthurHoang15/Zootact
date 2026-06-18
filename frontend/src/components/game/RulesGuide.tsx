import { useMemo, type ReactNode } from 'react';
import { useTranslation } from 'react-i18next';
import { Card, CuteButton } from '@/components/ui';

interface RuleSection {
    title: string;
    bullets: string[];
}

interface CaptureMatrixRow {
    piece: string;
    captures: string[];
}

interface RulesGuideProps {
    title?: string;
    subtitle?: string;
    compact?: boolean;
    actions?: ReactNode;
    className?: string;
}

function useRuleSections(): { quickFacts: string[]; sections: RuleSection[] } {
    const { t } = useTranslation();

    return useMemo(() => ({
        quickFacts: [
            t('rules.quickFacts.0'),
            t('rules.quickFacts.1'),
            t('rules.quickFacts.2'),
            t('rules.quickFacts.3'),
            t('rules.quickFacts.4'),
        ],
        sections: [
            {
                title: t('rules.sections.movement.title'),
                bullets: [
                    t('rules.sections.movement.bullets.0'),
                    t('rules.sections.movement.bullets.1'),
                    t('rules.sections.movement.bullets.2'),
                    t('rules.sections.movement.bullets.3'),
                ],
            },
            {
                title: t('rules.sections.river.title'),
                bullets: [
                    t('rules.sections.river.bullets.0'),
                    t('rules.sections.river.bullets.1'),
                    t('rules.sections.river.bullets.2'),
                    t('rules.sections.river.bullets.3'),
                ],
            },
            {
                title: t('rules.sections.capture.title'),
                bullets: [
                    t('rules.sections.capture.bullets.0'),
                    t('rules.sections.capture.bullets.1'),
                    t('rules.sections.capture.bullets.2'),
                    t('rules.sections.capture.bullets.3'),
                    t('rules.sections.capture.bullets.4'),
                ],
            },
            {
                title: t('rules.sections.endings.title'),
                bullets: [
                    t('rules.sections.endings.bullets.0'),
                    t('rules.sections.endings.bullets.1'),
                    t('rules.sections.endings.bullets.2'),
                ],
            },
        ],
    }), [t]);
}

function useCaptureMatrix(): { title: string; subtitle: string; note: string; rows: CaptureMatrixRow[] } {
    const { t } = useTranslation();

    return useMemo(() => ({
        title: t('rules.captureMatrix.title'),
        subtitle: t('rules.captureMatrix.subtitle'),
        note: t('rules.captureMatrix.note'),
        rows: [
            { piece: t('pieces.Rat'), captures: [t('pieces.Rat'), t('pieces.Elephant')] },
            { piece: t('pieces.Cat'), captures: [t('pieces.Rat'), t('pieces.Cat')] },
            { piece: t('pieces.Dog'), captures: [t('pieces.Rat'), t('pieces.Cat'), t('pieces.Dog')] },
            { piece: t('pieces.Wolf'), captures: [t('pieces.Rat'), t('pieces.Cat'), t('pieces.Dog'), t('pieces.Wolf')] },
            { piece: t('pieces.Leopard'), captures: [t('pieces.Rat'), t('pieces.Cat'), t('pieces.Dog'), t('pieces.Wolf'), t('pieces.Leopard')] },
            { piece: t('pieces.Tiger'), captures: [t('pieces.Rat'), t('pieces.Cat'), t('pieces.Dog'), t('pieces.Wolf'), t('pieces.Leopard'), t('pieces.Tiger')] },
            { piece: t('pieces.Lion'), captures: [t('pieces.Rat'), t('pieces.Cat'), t('pieces.Dog'), t('pieces.Wolf'), t('pieces.Leopard'), t('pieces.Tiger'), t('pieces.Lion')] },
            { piece: t('pieces.Elephant'), captures: [t('pieces.Cat'), t('pieces.Dog'), t('pieces.Wolf'), t('pieces.Leopard'), t('pieces.Tiger'), t('pieces.Lion'), t('pieces.Elephant')] },
        ],
    }), [t]);
}

function useQuickPanelData(): { summary: string[]; matrixRows: CaptureMatrixRow[]; note: string } {
    const { t } = useTranslation();

    return useMemo(() => ({
        summary: [
            t('rules.quickPanel.summary.0'),
            t('rules.quickPanel.summary.1'),
            t('rules.quickPanel.summary.2'),
            t('rules.quickPanel.summary.3'),
        ],
        matrixRows: [
            { piece: t('pieces.Rat'), captures: [t('pieces.Rat'), t('pieces.Elephant')] },
            { piece: t('pieces.Cat'), captures: [t('pieces.Rat'), t('pieces.Cat')] },
            { piece: t('pieces.Dog'), captures: [t('pieces.Rat'), t('pieces.Cat'), t('pieces.Dog')] },
            { piece: t('pieces.Wolf'), captures: [t('pieces.Rat'), t('pieces.Cat'), t('pieces.Dog'), t('pieces.Wolf')] },
            { piece: t('pieces.Leopard'), captures: [t('pieces.Rat'), t('pieces.Cat'), t('pieces.Dog'), t('pieces.Wolf'), t('pieces.Leopard')] },
            { piece: t('pieces.Tiger'), captures: [t('pieces.Rat'), t('pieces.Cat'), t('pieces.Dog'), t('pieces.Wolf'), t('pieces.Leopard'), t('pieces.Tiger')] },
            { piece: t('pieces.Lion'), captures: [t('pieces.Rat'), t('pieces.Cat'), t('pieces.Dog'), t('pieces.Wolf'), t('pieces.Leopard'), t('pieces.Tiger'), t('pieces.Lion')] },
            { piece: t('pieces.Elephant'), captures: [t('pieces.Cat'), t('pieces.Dog'), t('pieces.Wolf'), t('pieces.Leopard'), t('pieces.Tiger'), t('pieces.Lion'), t('pieces.Elephant')] },
        ],
        note: t('rules.quickPanel.note'),
    }), [t]);
}

function RulesEssentials({ compact = false }: { compact?: boolean }) {
    const { t } = useTranslation();
    const { quickFacts } = useRuleSections();
    const captureMatrix = useCaptureMatrix();
    const condensedFacts = compact ? quickFacts.slice(0, 4) : quickFacts;
    const condensedMatrix = compact
        ? [
            { piece: t('pieces.Rat'), captures: [t('pieces.Rat'), t('pieces.Elephant')] },
            { piece: t('pieces.Cat'), captures: [t('pieces.Rat'), t('pieces.Cat')] },
            { piece: t('pieces.Dog'), captures: [t('pieces.Rat'), t('pieces.Cat'), t('pieces.Dog')] },
            { piece: t('pieces.Wolf'), captures: [t('pieces.Rat'), t('pieces.Cat'), t('pieces.Dog'), t('pieces.Wolf')] },
            { piece: t('pieces.Leopard'), captures: [t('pieces.Rat'), t('pieces.Cat'), t('pieces.Dog'), t('pieces.Wolf'), t('pieces.Leopard')] },
            { piece: t('pieces.Tiger'), captures: [t('pieces.Rat'), t('pieces.Cat'), t('pieces.Dog'), t('pieces.Wolf'), t('pieces.Leopard'), t('pieces.Tiger')] },
            { piece: t('pieces.Lion'), captures: [t('pieces.Rat'), t('pieces.Cat'), t('pieces.Dog'), t('pieces.Wolf'), t('pieces.Leopard'), t('pieces.Tiger'), t('pieces.Lion')] },
            { piece: t('pieces.Elephant'), captures: [t('pieces.Cat'), t('pieces.Dog'), t('pieces.Wolf'), t('pieces.Leopard'), t('pieces.Tiger'), t('pieces.Lion'), t('pieces.Elephant')] },
        ]
        : captureMatrix.rows;

    return (
        <>
            <div className="rounded-3xl bg-cream p-4">
                <p className="text-xs font-bold uppercase tracking-[0.16em] text-carrot-orange-dark">
                    {t('rules.quickSummaryLabel')}
                </p>
                <div className={`mt-3 grid gap-2 ${compact ? 'grid-cols-1' : 'md:grid-cols-2'}`}>
                    {condensedFacts.map(item => (
                        <div key={item} className={`rounded-2xl bg-white text-forest-dark shadow-cute ${compact ? 'px-3 py-2 text-sm' : 'px-3 py-3 text-sm'}`}>
                            {item}
                        </div>
                    ))}
                </div>
            </div>

            <div className="mt-6 rounded-3xl bg-cream p-4">
                <h4 className="font-display text-xl text-forest-dark">{captureMatrix.title}</h4>
                <p className="mt-2 text-sm text-forest-light">
                    {compact ? t('rules.captureMatrix.compactSubtitle') : captureMatrix.subtitle}
                </p>

                <div className={`mt-4 grid gap-3 ${compact ? 'grid-cols-1' : 'md:grid-cols-2'}`}>
                    {condensedMatrix.map(row => (
                        <div key={row.piece} className={`rounded-2xl bg-white shadow-cute ${compact ? 'px-3 py-2' : 'px-4 py-3'}`}>
                            <p className={`${compact ? 'text-base' : 'text-lg'} font-display text-forest-dark`}>{row.piece}</p>
                            <p className={`mt-1 ${compact ? 'text-xs' : 'text-sm'} text-forest-light`}>
                                {compact ? row.captures.join(' • ') : row.captures.join(', ')}
                            </p>
                        </div>
                    ))}
                </div>

                <p className={`mt-4 font-bold text-carrot-orange-dark ${compact ? 'text-xs' : 'text-sm'}`}>{captureMatrix.note}</p>
            </div>
        </>
    );
}

export function RulesGuide({
    title,
    subtitle,
    compact = false,
    actions,
    className = '',
}: RulesGuideProps) {
    const { t } = useTranslation();
    const { sections } = useRuleSections();

    return (
        <Card padding={compact ? 'md' : 'lg'} className={`bg-white ${className}`}>
            <div className="flex items-start justify-between gap-4">
                <div>
                    <h3 className={`${compact ? 'text-xl' : 'text-3xl'} font-display text-forest-dark`}>
                        {title ?? t('rules.title')}
                    </h3>
                    <p className="mt-2 max-w-3xl text-sm text-forest-light">
                        {subtitle ?? t('rules.subtitle')}
                    </p>
                </div>
                {actions && <div className="shrink-0">{actions}</div>}
            </div>

            <div className="mt-5">
                <RulesEssentials compact={compact} />
            </div>

            <div className={`mt-6 grid gap-4 ${compact ? '' : 'xl:grid-cols-2'}`}>
                {sections.map(section => (
                    <div key={section.title} className="rounded-3xl bg-cream p-4">
                        <h4 className="font-display text-xl text-forest-dark">{section.title}</h4>
                        <ul className="mt-3 space-y-2 text-sm text-forest-light">
                            {section.bullets.map(item => (
                                <li key={item} className="flex items-start gap-2">
                                    <span className="mt-1 h-2 w-2 shrink-0 rounded-full bg-candy-green" />
                                    <span>{item}</span>
                                </li>
                            ))}
                        </ul>
                    </div>
                ))}
            </div>

        </Card>
    );
}

interface RulesQuickPanelProps {
    expanded: boolean;
    onToggle: () => void;
    onOpenModal: () => void;
}

export function RulesQuickPanel({ expanded, onToggle, onOpenModal }: RulesQuickPanelProps) {
    const { t } = useTranslation();
    const quickPanel = useQuickPanelData();

    return (
        <Card padding="md" className="bg-white">
            <div className="flex items-start justify-between gap-3">
                <div>
                    <h3 className="font-display text-xl text-forest-dark">{t('rules.panelTitle')}</h3>
                    <p className="mt-1 text-sm text-forest-light">{t('rules.quickPanelSubtitle')}</p>
                </div>
                <div className="flex shrink-0 items-center gap-2">
                    <CuteButton size="sm" variant="secondary" onClick={onOpenModal}>
                        {t('rules.openModal')}
                    </CuteButton>
                    <CuteButton size="sm" variant="ghost" onClick={onToggle}>
                        {expanded ? t('rules.collapse') : t('rules.expand')}
                    </CuteButton>
                </div>
            </div>

            {expanded && (
                <div className="mt-4 max-h-[50vh] space-y-4 overflow-y-auto pr-1">
                    <div className="rounded-3xl bg-cream p-4">
                        <p className="text-xs font-bold uppercase tracking-[0.16em] text-carrot-orange-dark">
                            {t('rules.quickSummaryLabel')}
                        </p>
                        <div className="mt-3 space-y-2">
                            {quickPanel.summary.map(item => (
                                <div key={item} className="rounded-2xl bg-white px-3 py-2 text-sm text-forest-dark shadow-cute">
                                    {item}
                                </div>
                            ))}
                        </div>
                    </div>

                    <div className="rounded-3xl bg-cream p-4">
                        <h4 className="font-display text-lg text-forest-dark">{t('rules.captureMatrix.title')}</h4>
                        <p className="mt-1 text-xs text-forest-light">{t('rules.quickPanel.matrixSubtitle')}</p>
                        <div className="mt-3 space-y-2">
                            {quickPanel.matrixRows.map(row => (
                                <div key={row.piece} className="rounded-2xl bg-white px-3 py-2 shadow-cute">
                                    <p className="font-display text-base text-forest-dark">{row.piece}</p>
                                    <p className="mt-1 text-xs text-forest-light">{row.captures.join(' • ')}</p>
                                </div>
                            ))}
                        </div>
                        <p className="mt-3 text-xs font-bold text-carrot-orange-dark">{quickPanel.note}</p>
                    </div>
                </div>
            )}
        </Card>
    );
}

interface RulesModalProps {
    open: boolean;
    onClose: () => void;
    onOpenPage: () => void;
}

export function RulesModal({ open, onClose, onOpenPage }: RulesModalProps) {
    const { t } = useTranslation();

    if (!open) {
        return null;
    }

    return (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-forest-dark/50 p-4">
            <button
                aria-label={t('common.close')}
                className="absolute inset-0"
                onClick={onClose}
            />
            <div
                className="relative max-h-[90vh] w-full max-w-4xl overflow-y-auto rounded-[2rem] bg-cream p-4 shadow-cute-lg sm:p-6"
                role="dialog"
                aria-modal="true"
                tabIndex={-1}
                onKeyDown={event => {
                    if (event.key === 'Escape') {
                        onClose();
                    }
                }}
            >
                <div className="flex items-center justify-between gap-3">
                    <h2 className="font-display text-2xl text-forest-dark sm:text-3xl">{t('rules.modalTitle')}</h2>
                    <CuteButton size="sm" variant="ghost" onClick={onClose} autoFocus>
                        {t('common.close')}
                    </CuteButton>
                </div>

                <RulesGuide
                    compact
                    className="mt-4"
                    title={t('rules.modalBodyTitle')}
                    subtitle={t('rules.modalBodySubtitle')}
                    actions={(
                        <CuteButton
                            size="sm"
                            variant="secondary"
                            onClick={() => {
                                onClose();
                                onOpenPage();
                            }}
                        >
                            {t('rules.openFullPage')}
                        </CuteButton>
                    )}
                />
            </div>
        </div>
    );
}

export default RulesGuide;
