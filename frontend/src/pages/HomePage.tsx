import { useEffect, useState } from 'react';
import { motion } from 'framer-motion';
import { useTranslation } from 'react-i18next';
import { useNavigate } from 'react-router-dom';
import { Avatar, Card, CuteButton, LanguageSwitcher } from '@/components/ui';
import { buildLobbyPath, routes } from '@/router/routes';
import { apiService } from '@/services';
import { useAuthStore, useGameStore } from '@/stores';
import type { TimeControlPreset } from '@/types';

type MatchmakingPreset = Exclude<TimeControlPreset, 'Untimed'>;

const presets: [MatchmakingPreset, string, string][] = [
    ['Blitz', 'Blitz', 'matchmaking.blitz'],
    ['Rapid', 'Rapid', 'matchmaking.rapid'],
    ['Classical', 'Classical', 'matchmaking.classical'],
];

export function HomePage() {
    const { t } = useTranslation();
    const navigate = useNavigate();
    const isAuthenticated = useAuthStore(state => state.isAuthenticated);
    const user = useAuthStore(state => state.user);
    const logout = useAuthStore(state => state.logout);
    const hydrateActiveMatch = useGameStore(state => state.hydrateActiveMatch);
    const [isCreatingLobby, setIsCreatingLobby] = useState(false);
    const [queueState, setQueueState] = useState<{ searching: boolean; timeControl: MatchmakingPreset | null; position: number | null }>({
        searching: false,
        timeControl: null,
        position: null,
    });

    useEffect(() => {
        if (!queueState.searching || !isAuthenticated) {
            return;
        }

        let cancelled = false;

        const interval = window.setInterval(() => {
            void apiService.getActiveMatch().then(activeMatch => {
                if (cancelled || !activeMatch) {
                    return;
                }

                hydrateActiveMatch(activeMatch);
                setQueueState({ searching: false, timeControl: null, position: null });
                navigate(routes.game);
            }).catch(error => {
                console.error('Failed to poll active match', error);
            });
        }, 1000);

        return () => {
            cancelled = true;
            window.clearInterval(interval);
        };
    }, [hydrateActiveMatch, isAuthenticated, navigate, queueState.searching]);

    async function handleQueueJoin(timeControl: MatchmakingPreset) {
        if (!isAuthenticated) {
            navigate(routes.login);
            return;
        }

        const result = await apiService.joinQueue(timeControl);
        if (result.match_found) {
            const activeMatch = await apiService.getActiveMatch();
            if (activeMatch) {
                hydrateActiveMatch(activeMatch);
            }
            setQueueState({ searching: false, timeControl: null, position: null });
            navigate(routes.game);
            return;
        }

        setQueueState({
            searching: true,
            timeControl,
            position: result.queue_position ?? null,
        });
    }

    async function handleQueueCancel() {
        await apiService.leaveQueue();
        setQueueState({ searching: false, timeControl: null, position: null });
    }

    async function handleCreateLobby() {
        if (!isAuthenticated) {
            navigate(routes.login);
            return;
        }

        setIsCreatingLobby(true);

        try {
            const response = await apiService.createLobby();
            if (response.lobby) {
                navigate(buildLobbyPath(response.lobby.lobby_id));
            }
        } catch (error) {
            console.error('Failed to create lobby', error);
            alert(error instanceof Error ? error.message : t('common.error'));
        } finally {
            setIsCreatingLobby(false);
        }
    }

    return (
        <div className="min-h-screen bg-cream">
            <header className="relative overflow-hidden bg-gradient-to-b from-candy-green via-candy-green-light to-cream px-4 py-8">
                <div className="mx-auto flex max-w-5xl items-center justify-between">
                    <div>
                        <h1 className="font-display text-5xl text-white">Zootact</h1>
                        <p className="mt-2 text-lg text-white/85">{t('home.subtitle')}</p>
                    </div>
                    <div className="flex items-center gap-3">
                        <LanguageSwitcher />
                        {isAuthenticated ? (
                            <>
                                <button
                                    className="flex items-center gap-3 rounded-2xl bg-white/90 px-3 py-2 text-left text-sm font-bold text-forest-dark transition hover:bg-white"
                                    onClick={() => navigate(routes.profile)}
                                >
                                    <Avatar
                                        src={user?.avatar_url}
                                        alt={user?.username ?? 'User'}
                                        avatarSize="sm"
                                    />
                                    <span>{user?.username}</span>
                                </button>
                                <CuteButton size="sm" variant="ghost" onClick={() => void logout()}>
                                    {t('auth.logout')}
                                </CuteButton>
                            </>
                        ) : (
                            <CuteButton size="sm" variant="primary" onClick={() => navigate(routes.login)}>
                                {t('auth.loginButton')}
                            </CuteButton>
                        )}
                    </div>
                </div>
            </header>

            <main className="mx-auto max-w-5xl px-4 py-12">
                <section className="grid gap-6 md:grid-cols-3">
                    {presets.map(([preset, title, translationKey]) => (
                        <Card key={preset} padding="lg" className="text-center">
                            <h2 className="font-display text-2xl text-forest-dark">{title}</h2>
                            <p className="mb-4 mt-2 text-sm text-forest-light">{t(translationKey)}</p>
                            <CuteButton
                                fullWidth
                                variant={queueState.timeControl === preset ? 'accent' : 'primary'}
                                onClick={() => void handleQueueJoin(preset)}
                            >
                                {queueState.searching && queueState.timeControl === preset
                                    ? t('matchmaking.searching')
                                    : t('nav.play')}
                            </CuteButton>
                        </Card>
                    ))}
                </section>

                <section className="mt-8">
                    <Card padding="lg" className="overflow-hidden bg-gradient-to-r from-white to-cream-dark">
                        <div className="flex flex-col gap-6 lg:flex-row lg:items-center lg:justify-between">
                            <div className="max-w-2xl">
                                <p className="text-sm font-bold uppercase tracking-[0.12em] text-carrot-orange-dark">
                                    {t('home.playWithFriend')}
                                </p>
                                <h2 className="mt-2 font-display text-4xl text-forest-dark">{t('lobby.homeTitle')}</h2>
                                <p className="mt-3 text-forest-light">{t('lobby.homeSubtitle')}</p>
                            </div>
                            <CuteButton
                                size="lg"
                                variant="accent"
                                onClick={() => {
                                    if (!isAuthenticated) {
                                        navigate(routes.login);
                                        return;
                                    }

                                    void handleCreateLobby();
                                }}
                                isLoading={isCreatingLobby}
                            >
                                {t('home.playWithFriend')}
                            </CuteButton>
                        </div>
                    </Card>
                </section>

                <section className="mt-8">
                    <Card padding="lg" className="bg-white">
                        <div className="flex flex-col gap-4 md:flex-row md:items-center md:justify-between">
                            <div className="max-w-2xl">
                                <p className="text-sm font-bold uppercase tracking-[0.12em] text-sky-blue-dark">
                                    {t('rules.homeEyebrow')}
                                </p>
                                <h2 className="mt-2 font-display text-3xl text-forest-dark">{t('rules.homeTitle')}</h2>
                                <p className="mt-2 text-forest-light">{t('rules.homeSubtitle')}</p>
                            </div>
                            <CuteButton
                                size="lg"
                                variant="secondary"
                                onClick={() => navigate(routes.rules)}
                            >
                                {t('rules.openFullPage')}
                            </CuteButton>
                        </div>
                    </Card>
                </section>

                {queueState.searching && (
                    <motion.section
                        className="mt-8 rounded-3xl bg-white p-6 shadow-cute"
                        initial={{ opacity: 0, y: 16 }}
                        animate={{ opacity: 1, y: 0 }}
                    >
                        <p className="font-display text-xl text-forest-dark">{t('matchmaking.searching')}</p>
                        <p className="mt-2 text-forest-light">
                            {queueState.position ? `Queue position: ${queueState.position}` : t('game.waiting')}
                        </p>
                        <div className="mt-4">
                            <CuteButton variant="danger" onClick={() => void handleQueueCancel()}>
                                {t('matchmaking.cancel')}
                            </CuteButton>
                        </div>
                    </motion.section>
                )}
            </main>
        </div>
    );
}

export default HomePage;
