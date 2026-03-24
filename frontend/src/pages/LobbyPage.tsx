import { useEffect, useMemo, useState } from 'react';
import { motion } from 'framer-motion';
import { useTranslation } from 'react-i18next';
import { Avatar, Card, CuteButton, LanguageSwitcher } from '@/components/ui';
import { apiService, signalRService } from '@/services';
import { useAuthStore, useLobbyStore } from '@/stores';

interface LobbyPageProps {
    lobbyId: string;
}

function buildInviteLink(lobbyId: string) {
    return `${window.location.origin}${window.location.pathname}#/lobby/${lobbyId}`;
}

export function LobbyPage({ lobbyId }: LobbyPageProps) {
    const { t } = useTranslation();
    const firebaseToken = useAuthStore(state => state.firebaseToken);
    const lobby = useLobbyStore(state => state.lobby);
    const isLoading = useLobbyStore(state => state.isLoading);
    const error = useLobbyStore(state => state.error);
    const closedReason = useLobbyStore(state => state.closedReason);
    const setLobby = useLobbyStore(state => state.setLobby);
    const setLoading = useLobbyStore(state => state.setLoading);
    const setError = useLobbyStore(state => state.setError);
    const clearLobby = useLobbyStore(state => state.clearLobby);
    const [pendingAction, setPendingAction] = useState<string | null>(null);
    const [copied, setCopied] = useState(false);
    const [now, setNow] = useState(Date.now());

    useEffect(() => {
        if (!firebaseToken) {
            return;
        }

        let cancelled = false;

        async function bootstrapLobby() {
            setLoading(true);
            setError(null);

            try {
                if (!firebaseToken) {
                    throw new Error(t('lobby.connectionError'));
                }

                const connected = await signalRService.connect(firebaseToken);
                if (!connected) {
                    throw new Error(t('lobby.connectionError'));
                }

                const response = await apiService.joinLobby(lobbyId);
                if (cancelled) {
                    return;
                }

                if (response.lobby) {
                    setLobby(response.lobby);
                } else {
                    const fallbackLobby = await apiService.getLobby(lobbyId);
                    if (!cancelled) {
                        setLobby(fallbackLobby);
                    }
                }

                await signalRService.joinLobby(lobbyId);
            } catch (err: unknown) {
                if (!cancelled) {
                    setError(err instanceof Error ? err.message : t('common.error'));
                }
            } finally {
                if (!cancelled) {
                    setLoading(false);
                }
            }
        }

        void bootstrapLobby();

        return () => {
            cancelled = true;
        };
    }, [firebaseToken, lobbyId, setError, setLoading, setLobby, t]);

    useEffect(() => {
        if (!closedReason) {
            return;
        }

        clearLobby();
        window.location.hash = '#/';
    }, [clearLobby, closedReason]);

    useEffect(() => {
        if (!lobby?.countdown_active || !lobby.countdown_end_at) {
            return;
        }

        setNow(Date.now());
        const interval = window.setInterval(() => setNow(Date.now()), 250);
        return () => window.clearInterval(interval);
    }, [lobby?.countdown_active, lobby?.countdown_end_at]);

    const countdownRemaining = useMemo(() => {
        if (!lobby?.countdown_active || !lobby.countdown_end_at) {
            return 0;
        }

        return Math.max(0, Math.ceil((new Date(lobby.countdown_end_at).getTime() - now) / 1000));
    }, [lobby?.countdown_active, lobby?.countdown_end_at, now]);

    const isHost = lobby?.current_user_role === 'Host';
    const isGuest = lobby?.current_user_role === 'Guest';
    const guestReady = lobby?.guest?.is_ready ?? false;
    const canStart = Boolean(lobby && isHost && lobby.can_start && !lobby.countdown_active);
    const inviteLink = buildInviteLink(lobbyId);

    async function handleCopyLink() {
        try {
            await navigator.clipboard.writeText(inviteLink);
            setCopied(true);
            window.setTimeout(() => setCopied(false), 2000);
        } catch {
            setError(t('lobby.copyFailed'));
        }
    }

    async function handleLeaveLobby() {
        setPendingAction('leave');

        try {
            await apiService.leaveLobby(lobbyId);
            await signalRService.leaveLobby(lobbyId);
            clearLobby();
            window.location.hash = '#/';
        } catch (err: unknown) {
            setError(err instanceof Error ? err.message : t('common.error'));
        } finally {
            setPendingAction(null);
        }
    }

    async function handleToggleReady() {
        if (!lobby || !isGuest) {
            return;
        }

        setPendingAction('ready');

        try {
            const response = await apiService.setLobbyReady(lobbyId, !guestReady);
            if (response.lobby) {
                setLobby(response.lobby);
            }
        } catch (err: unknown) {
            setError(err instanceof Error ? err.message : t('common.error'));
        } finally {
            setPendingAction(null);
        }
    }

    async function handleStart() {
        setPendingAction('start');

        try {
            const response = await apiService.startLobby(lobbyId);
            if (response.lobby) {
                setLobby(response.lobby);
            }
        } catch (err: unknown) {
            setError(err instanceof Error ? err.message : t('common.error'));
        } finally {
            setPendingAction(null);
        }
    }

    async function handleCancelCountdown() {
        setPendingAction('cancel');

        try {
            const response = await apiService.cancelLobbyStart(lobbyId);
            if (response.lobby) {
                setLobby(response.lobby);
            }
        } catch (err: unknown) {
            setError(err instanceof Error ? err.message : t('common.error'));
        } finally {
            setPendingAction(null);
        }
    }

    return (
        <div className="min-h-screen bg-cream">
            <header className="relative overflow-hidden bg-gradient-to-br from-carrot-orange via-candy-green to-cream px-4 py-8">
                <div className="mx-auto flex max-w-5xl items-start justify-between gap-4">
                    <div>
                        <button className="text-sm font-bold text-white/90" onClick={() => { window.location.hash = '#/'; }}>
                            {t('common.back')}
                        </button>
                        <h1 className="mt-3 font-display text-5xl text-white">{t('lobby.title')}</h1>
                        <p className="mt-2 text-white/90">{t('lobby.subtitle')}</p>
                    </div>
                    <LanguageSwitcher />
                </div>
            </header>

            <main className="mx-auto max-w-5xl px-4 py-10">
                <div className="grid gap-6 lg:grid-cols-[1.3fr_0.7fr]">
                    <section className="space-y-6">
                        <motion.div initial={{ opacity: 0, y: 18 }} animate={{ opacity: 1, y: 0 }}>
                            <Card padding="lg" className="space-y-5">
                                <div className="flex flex-wrap items-center justify-between gap-4">
                                    <div>
                                        <p className="text-sm font-bold uppercase tracking-[0.12em] text-forest-light">
                                            {t('matchmaking.selectMode')}
                                        </p>
                                        <h2 className="mt-2 font-display text-3xl text-forest-dark">{lobby?.preset ?? '...'}</h2>
                                    </div>
                                    <div className="rounded-full bg-cream px-4 py-2 text-sm font-bold text-forest-dark">
                                        ID: {lobbyId.slice(0, 8)}
                                    </div>
                                </div>

                                {error && (
                                    <div className="rounded-2xl bg-player-red/10 px-4 py-3 text-sm font-bold text-player-red">
                                        {error}
                                    </div>
                                )}

                                {lobby?.countdown_active && (
                                    <div className="rounded-3xl bg-carrot-orange/10 px-5 py-4 text-center">
                                        <p className="text-sm font-bold uppercase tracking-[0.12em] text-carrot-orange-dark">
                                            {t('lobby.countdownLabel')}
                                        </p>
                                        <p className="mt-2 font-display text-5xl text-carrot-orange-dark">{countdownRemaining}</p>
                                        <p className="mt-2 text-sm text-forest-dark">{t('lobby.countdownHint')}</p>
                                    </div>
                                )}

                                <div className="grid gap-4 md:grid-cols-2">
                                    <Card padding="md" className="bg-cream">
                                        <p className="text-sm font-bold uppercase tracking-[0.12em] text-forest-light">{t('lobby.host')}</p>
                                        <div className="mt-4 flex items-center gap-4">
                                            <Avatar src={lobby?.host.avatar_url} alt={lobby?.host.username ?? 'Host'} avatarSize="lg" />
                                            <div>
                                                <p className="font-display text-2xl text-forest-dark">{lobby?.host.username ?? '...'}</p>
                                                <p className="text-sm text-forest-light">{t('lobby.hostStatus')}</p>
                                            </div>
                                        </div>
                                    </Card>

                                    <Card padding="md" className="bg-cream">
                                        <p className="text-sm font-bold uppercase tracking-[0.12em] text-forest-light">{t('lobby.guest')}</p>
                                        {lobby?.guest ? (
                                            <div className="mt-4 flex items-center gap-4">
                                                <Avatar src={lobby.guest.avatar_url} alt={lobby.guest.username} avatarSize="lg" />
                                                <div>
                                                    <p className="font-display text-2xl text-forest-dark">{lobby.guest.username}</p>
                                                    <p className={`text-sm font-bold ${lobby.guest.is_ready ? 'text-candy-green-dark' : 'text-player-red-dark'}`}>
                                                        {lobby.guest.is_ready ? t('lobby.ready') : t('lobby.notReady')}
                                                    </p>
                                                </div>
                                            </div>
                                        ) : (
                                            <div className="mt-4 rounded-2xl border-2 border-dashed border-forest-light/25 px-4 py-6 text-center text-sm text-forest-light">
                                                {t('lobby.waitingGuest')}
                                            </div>
                                        )}
                                    </Card>
                                </div>
                            </Card>
                        </motion.div>
                    </section>

                    <motion.aside initial={{ opacity: 0, y: 18 }} animate={{ opacity: 1, y: 0 }}>
                        <Card padding="lg" className="space-y-5">
                            <div>
                                <h3 className="font-display text-2xl text-forest-dark">{t('lobby.inviteTitle')}</h3>
                                <p className="mt-2 text-sm text-forest-light">{t('lobby.inviteHint')}</p>
                            </div>

                            <div className="rounded-2xl bg-cream px-4 py-4 text-sm text-forest-dark break-all">
                                {inviteLink}
                            </div>

                            <CuteButton fullWidth variant={copied ? 'accent' : 'secondary'} onClick={() => void handleCopyLink()}>
                                {copied ? t('lobby.copied') : t('lobby.copyLink')}
                            </CuteButton>

                            <div className="space-y-3">
                                {lobby?.countdown_active ? (
                                    <CuteButton
                                        fullWidth
                                        variant="danger"
                                        onClick={() => void handleCancelCountdown()}
                                        isLoading={pendingAction === 'cancel'}
                                    >
                                        {t('lobby.cancelStart')}
                                    </CuteButton>
                                ) : isHost ? (
                                    <CuteButton
                                        fullWidth
                                        onClick={() => void handleStart()}
                                        isLoading={pendingAction === 'start'}
                                        disabled={!canStart}
                                    >
                                        {t('lobby.start')}
                                    </CuteButton>
                                ) : (
                                    <CuteButton
                                        fullWidth
                                        variant={guestReady ? 'accent' : 'secondary'}
                                        onClick={() => void handleToggleReady()}
                                        isLoading={pendingAction === 'ready'}
                                        disabled={!isGuest}
                                    >
                                        {guestReady ? t('lobby.unready') : t('lobby.ready')}
                                    </CuteButton>
                                )}

                                <CuteButton
                                    fullWidth
                                    variant="ghost"
                                    onClick={() => void handleLeaveLobby()}
                                    isLoading={pendingAction === 'leave'}
                                >
                                    {t('lobby.leave')}
                                </CuteButton>
                            </div>

                            <div className="rounded-2xl bg-white px-4 py-4 text-sm text-forest-light">
                                {isLoading ? t('common.loading') : isHost ? t('lobby.hostHelp') : t('lobby.guestHelp')}
                            </div>
                        </Card>
                    </motion.aside>
                </div>
            </main>
        </div>
    );
}

export default LobbyPage;
