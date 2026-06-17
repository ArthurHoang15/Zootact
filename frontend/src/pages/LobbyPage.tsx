import { useEffect, useMemo, useState } from 'react';
import { motion } from 'framer-motion';
import { useTranslation } from 'react-i18next';
import { useNavigate } from 'react-router-dom';
import { getPublicAppUrl } from '@/config/runtime';
import { Avatar, Card, CuteButton, LanguageSwitcher } from '@/components/ui';
import { buildLobbyPath, routes } from '@/router/routes';
import { isBackendUnavailableError } from '@/services/apiErrors';
import { apiService, signalRService } from '@/services';
import { useAuthStore, useLobbyStore } from '@/stores';

interface LobbyPageProps {
    lobbyId: string;
}

function buildInviteLink(lobbyId: string) {
    return `${getPublicAppUrl()}${buildLobbyPath(lobbyId)}`;
}

function getErrorMessage(error: unknown, fallback: string): string {
    return error instanceof Error ? error.message : fallback;
}

export function LobbyPage({ lobbyId }: LobbyPageProps) {
    const { t } = useTranslation();
    const navigate = useNavigate();
    const firebaseToken = useAuthStore(state => state.firebaseToken);
    const backendStatus = useAuthStore(state => state.backendStatus);
    const markBackendDegraded = useAuthStore(state => state.markBackendDegraded);
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

        if (backendStatus === 'degraded') {
            setLoading(false);
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

                const connection = await signalRService.connect(firebaseToken);
                if (!connection.connected) {
                    if (connection.reason === 'unavailable') {
                        markBackendDegraded(t('status.degradedBody'));
                    }
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
                    if (isBackendUnavailableError(err)) {
                        markBackendDegraded(getErrorMessage(err, t('status.degradedBody')));
                    }
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
    }, [backendStatus, firebaseToken, lobbyId, markBackendDegraded, setError, setLoading, setLobby, t]);

    useEffect(() => {
        if (!closedReason) {
            return;
        }

        clearLobby();
        navigate(routes.home, { replace: true });
    }, [clearLobby, closedReason, navigate]);

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
    const actionsDisabled = backendStatus !== 'healthy';
    const inviteLink = buildInviteLink(lobbyId);
    const lobbyModeLabel = lobby?.mode === 'FriendlyUntimed'
        ? t('lobby.untimedMode')
        : lobby?.mode ?? '...';

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
        } catch (err: unknown) {
            if (isBackendUnavailableError(err)) {
                markBackendDegraded(getErrorMessage(err, t('status.degradedBody')));
            }
            setError(err instanceof Error ? err.message : t('common.error'));
            setPendingAction(null);
            return;
        }

        try {
            await signalRService.leaveLobby(lobbyId);
        } catch (err) {
            console.warn('Failed to leave lobby group', err);
        } finally {
            clearLobby();
            navigate(routes.home, { replace: true });
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
            if (isBackendUnavailableError(err)) {
                markBackendDegraded(getErrorMessage(err, t('status.degradedBody')));
            }
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
            if (isBackendUnavailableError(err)) {
                markBackendDegraded(getErrorMessage(err, t('status.degradedBody')));
            }
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
            if (isBackendUnavailableError(err)) {
                markBackendDegraded(getErrorMessage(err, t('status.degradedBody')));
            }
            setError(err instanceof Error ? err.message : t('common.error'));
        } finally {
            setPendingAction(null);
        }
    }

    return (
        <div className="min-h-screen bg-cream">
            <header className="relative overflow-hidden bg-gradient-to-br from-carrot-orange via-candy-green to-cream px-4 py-6 sm:py-8">
                <div className="mx-auto flex max-w-6xl flex-wrap items-start justify-between gap-4">
                    <div>
                        <button
                            className="text-sm font-bold text-white/90 disabled:cursor-not-allowed disabled:text-white/50"
                            onClick={() => void handleLeaveLobby()}
                            disabled={actionsDisabled || pendingAction === 'leave'}
                        >
                            {t('common.back')}
                        </button>
                        <h1 className="mt-3 font-display text-4xl text-white sm:text-5xl">{t('lobby.title')}</h1>
                        <p className="mt-2 max-w-2xl text-sm text-white/90 sm:text-base">{t('lobby.subtitle')}</p>
                    </div>
                    <LanguageSwitcher />
                </div>
            </header>

            <main className="mx-auto max-w-6xl px-4 py-6 sm:py-10">
                <div className="grid gap-6 xl:grid-cols-[minmax(0,1.35fr)_minmax(320px,0.65fr)] xl:items-start">
                    <section className="space-y-6">
                        <motion.div initial={{ opacity: 0, y: 18 }} animate={{ opacity: 1, y: 0 }}>
                            <Card padding="lg" className="space-y-5">
                                <div className="flex flex-wrap items-center justify-between gap-4">
                                    <div>
                                        <p className="text-sm font-bold uppercase tracking-[0.12em] text-forest-light">
                                            {t('lobby.modeLabel')}
                                        </p>
                                        <h2 className="mt-2 font-display text-3xl text-forest-dark sm:text-4xl">{lobbyModeLabel}</h2>
                                        <p className="mt-2 text-sm text-forest-light">{t('lobby.untimedModeHint')}</p>
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
                                    <div className="rounded-3xl bg-carrot-orange/10 px-4 py-4 text-center sm:px-5">
                                        <p className="text-sm font-bold uppercase tracking-[0.12em] text-carrot-orange-dark">
                                            {t('lobby.countdownLabel')}
                                        </p>
                                        <p className="mt-2 font-display text-4xl text-carrot-orange-dark sm:text-5xl">{countdownRemaining}</p>
                                        <p className="mt-2 text-sm text-forest-dark">{t('lobby.countdownHint')}</p>
                                    </div>
                                )}

                                <div className="grid gap-4 lg:grid-cols-2">
                                    <Card padding="md" className="bg-cream">
                                        <p className="text-sm font-bold uppercase tracking-[0.12em] text-forest-light">{t('lobby.host')}</p>
                                        <div className="mt-4 flex items-center gap-4 sm:gap-5">
                                            <Avatar
                                                src={lobby?.host.avatar_url}
                                                alt={lobby?.host.username ?? 'Host'}
                                                avatarSize="lg"
                                                className="shrink-0"
                                            />
                                            <div className="min-w-0">
                                                <p className="truncate font-display text-xl text-forest-dark sm:text-2xl">{lobby?.host.username ?? '...'}</p>
                                                <p className="text-sm text-forest-light">{t('lobby.hostStatus')}</p>
                                            </div>
                                        </div>
                                    </Card>

                                    <Card padding="md" className="bg-cream">
                                        <p className="text-sm font-bold uppercase tracking-[0.12em] text-forest-light">{t('lobby.guest')}</p>
                                        {lobby?.guest ? (
                                            <div className="mt-4 flex items-center gap-4 sm:gap-5">
                                                <Avatar
                                                    src={lobby.guest.avatar_url}
                                                    alt={lobby.guest.username}
                                                    avatarSize="lg"
                                                    className="shrink-0"
                                                />
                                                <div className="min-w-0">
                                                    <p className="truncate font-display text-xl text-forest-dark sm:text-2xl">{lobby.guest.username}</p>
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

                    <motion.aside initial={{ opacity: 0, y: 18 }} animate={{ opacity: 1, y: 0 }} className="xl:sticky xl:top-6">
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
                                        disabled={actionsDisabled}
                                    >
                                        {t('lobby.cancelStart')}
                                    </CuteButton>
                                ) : isHost ? (
                                    <CuteButton
                                        fullWidth
                                        onClick={() => void handleStart()}
                                        isLoading={pendingAction === 'start'}
                                        disabled={!canStart || actionsDisabled}
                                    >
                                        {t('lobby.start')}
                                    </CuteButton>
                                ) : (
                                    <CuteButton
                                        fullWidth
                                        variant={guestReady ? 'accent' : 'secondary'}
                                        onClick={() => void handleToggleReady()}
                                        isLoading={pendingAction === 'ready'}
                                        disabled={!isGuest || actionsDisabled}
                                    >
                                        {guestReady ? t('lobby.unready') : t('lobby.ready')}
                                    </CuteButton>
                                )}

                                <CuteButton
                                    fullWidth
                                    variant="ghost"
                                    onClick={() => void handleLeaveLobby()}
                                    isLoading={pendingAction === 'leave'}
                                    disabled={actionsDisabled}
                                >
                                    {t('lobby.leave')}
                                </CuteButton>
                            </div>

                            <div className="rounded-2xl bg-white px-4 py-4 text-sm text-forest-light">
                                {isLoading
                                    ? t('common.loading')
                                    : actionsDisabled
                                        ? t('status.actionsDisabled')
                                        : isHost
                                            ? t('lobby.hostHelp')
                                            : t('lobby.guestHelp')}
                            </div>
                        </Card>
                    </motion.aside>
                </div>
            </main>
        </div>
    );
}

export default LobbyPage;
