import { useEffect, useState } from 'react';
import { motion } from 'framer-motion';
import { useTranslation } from 'react-i18next';
import { useNavigate } from 'react-router-dom';
import { Avatar, Card, CuteButton, CuteInput, LanguageSwitcher } from '@/components/ui';
import { isBackendUnavailableError } from '@/services/apiErrors';
import { apiService } from '@/services';
import { routes } from '@/router/routes';
import { useAuthStore } from '@/stores';
import type { MyProfileDto, RecentProfileMatchDto, UserStatsDto } from '@/types';
import type { TFunction } from 'i18next';

const emptyStats: UserStatsDto = {
    total_games: 0,
    wins: 0,
    losses: 0,
    draws: 0,
    win_rate: 0,
    current_streak: 0,
    best_streak: 0,
    avg_move_time_ms: null,
    total_play_time_ms: 0,
};

function formatAverageMoveTime(value: number | null) {
    if (value == null) {
        return '-';
    }

    if (value < 1000) {
        return `${Math.round(value)} ms`;
    }

    return `${(value / 1000).toFixed(1)} s`;
}

function formatPlayTime(totalMs: number) {
    const totalSeconds = Math.floor(totalMs / 1000);
    const hours = Math.floor(totalSeconds / 3600);
    const minutes = Math.floor((totalSeconds % 3600) / 60);

    if (hours > 0) {
        return `${hours}h ${minutes}m`;
    }

    return `${minutes}m`;
}

function formatMatchDate(value: string | null) {
    if (!value) {
        return '-';
    }

    return new Intl.DateTimeFormat(undefined, {
        day: '2-digit',
        month: 'short',
        year: 'numeric',
    }).format(new Date(value));
}

function formatMatchTimeControl(value: string, t: TFunction) {
    switch (value) {
        case 'Blitz':
            return t('matchmaking.blitz');
        case 'Rapid':
            return t('matchmaking.rapid');
        case 'Classical':
            return t('matchmaking.classical');
        case 'Untimed':
            return t('lobby.untimedMode');
        default:
            return value;
    }
}

function getErrorMessage(error: unknown, fallback: string) {
    return error instanceof Error ? error.message : fallback;
}

function outcomeClasses(outcome: RecentProfileMatchDto['outcome']) {
    switch (outcome) {
        case 'Win':
            return 'bg-candy-green/15 text-candy-green-dark';
        case 'Loss':
            return 'bg-player-red/15 text-player-red-dark';
        case 'Draw':
        default:
            return 'bg-sky-blue/15 text-sky-blue-dark';
    }
}

function matchTypeClasses(matchType: RecentProfileMatchDto['match_type']) {
    return matchType === 'Friendly'
        ? 'bg-carrot-orange/15 text-carrot-orange-dark'
        : 'bg-forest-light/10 text-forest-dark';
}

export function ProfilePage() {
    const { t } = useTranslation();
    const navigate = useNavigate();
    const isAuthenticated = useAuthStore(state => state.isAuthenticated);
    const backendStatus = useAuthStore(state => state.backendStatus);
    const markBackendDegraded = useAuthStore(state => state.markBackendDegraded);
    const authUser = useAuthStore(state => state.user);
    const logout = useAuthStore(state => state.logout);
    const setUser = useAuthStore(state => state.setUser);
    const [profile, setProfile] = useState<MyProfileDto | null>(null);
    const [username, setUsername] = useState(authUser?.username ?? '');
    const [isLoading, setIsLoading] = useState(true);
    const [isSaving, setIsSaving] = useState(false);
    const [error, setError] = useState<string | null>(null);
    const [success, setSuccess] = useState<string | null>(null);

    useEffect(() => {
        if (!isAuthenticated) {
            navigate(routes.login, { replace: true });
            return;
        }

        let cancelled = false;

        async function loadProfile() {
            setIsLoading(true);
            setError(null);

            try {
                const response = await apiService.getMyProfile();
                if (cancelled) {
                    return;
                }

                setProfile(response);
                setUsername(response.user.username);
                setUser(response.user);
            } catch (err: unknown) {
                if (!cancelled) {
                    if (isBackendUnavailableError(err)) {
                        markBackendDegraded(getErrorMessage(err, t('status.degradedBody')));
                    }
                    setError(getErrorMessage(err, t('common.error')));
                }
            } finally {
                if (!cancelled) {
                    setIsLoading(false);
                }
            }
        }

        void loadProfile();

        return () => {
            cancelled = true;
        };
    }, [isAuthenticated, markBackendDegraded, navigate, setUser, t]);

    async function handleSaveProfile() {
        if (!username.trim()) {
            setError(t('profile.usernameRequired'));
            return;
        }

        setIsSaving(true);
        setError(null);
        setSuccess(null);

        try {
            const response = await apiService.updateMyProfile({ username: username.trim() });
            setProfile(response);
            setUsername(response.user.username);
            setUser(response.user);
            setSuccess(t('profile.saveSuccess'));
        } catch (err: unknown) {
            if (isBackendUnavailableError(err)) {
                markBackendDegraded(getErrorMessage(err, t('status.degradedBody')));
            }
            setError(getErrorMessage(err, t('common.error')));
        } finally {
            setIsSaving(false);
        }
    }

    if (!isAuthenticated) {
        return null;
    }

    const currentUser = profile?.user ?? authUser;
    const stats = profile?.stats ?? emptyStats;
    const friendlyStats = profile?.friendly_stats ?? emptyStats;
    const recentMatches = profile?.recent_matches ?? [];

    return (
        <div className="min-h-screen bg-cream">
            <header className="relative overflow-hidden bg-gradient-to-br from-sky-blue via-candy-green to-cream px-4 py-8">
                <div className="mx-auto flex max-w-6xl items-start justify-between gap-4">
                    <div>
                        <button className="text-sm font-bold text-white/90" onClick={() => navigate(routes.home)}>
                            {t('profile.goHome')}
                        </button>
                        <h1 className="mt-3 font-display text-5xl text-white">{t('nav.profile')}</h1>
                        <p className="mt-2 max-w-2xl text-white/90">{t('profile.subtitle')}</p>
                    </div>
                    <div className="flex items-center gap-3">
                        <LanguageSwitcher />
                        <CuteButton size="sm" variant="ghost" onClick={() => void logout()}>
                            {t('auth.logout')}
                        </CuteButton>
                    </div>
                </div>
            </header>

            <main className="mx-auto max-w-6xl px-4 py-10">
                {isLoading ? (
                    <div className="rounded-3xl bg-white p-8 text-center shadow-cute">
                        <p className="font-display text-2xl text-forest-dark">{t('common.loading')}</p>
                    </div>
                ) : (
                    <div className="grid gap-6 lg:grid-cols-[1.1fr_0.9fr]">
                        <motion.section initial={{ opacity: 0, y: 18 }} animate={{ opacity: 1, y: 0 }} className="space-y-6">
                            <Card padding="lg" className="overflow-hidden bg-gradient-to-br from-white to-cream-dark">
                                <div className="flex flex-col gap-6 md:flex-row md:items-center">
                                    <Avatar
                                        src={currentUser?.avatar_url}
                                        alt={currentUser?.username ?? 'User'}
                                        avatarSize="xl"
                                        className="shrink-0"
                                    />
                                    <div className="flex-1">
                                        <div className="flex flex-wrap items-center gap-3">
                                            <h2 className="font-display text-4xl text-forest-dark">
                                                {currentUser?.username}
                                            </h2>
                                            <span className="rounded-full bg-carrot-orange/15 px-3 py-1 text-sm font-bold text-carrot-orange-dark">
                                                {t('profile.rating')}: {currentUser?.forest_points ?? 1200}
                                            </span>
                                        </div>
                                        <p className="mt-3 text-sm text-forest-light">{currentUser?.email}</p>
                                        <p className="mt-2 text-sm text-forest-light">
                                            {t('profile.provider')}: {currentUser?.auth_provider}
                                        </p>
                                    </div>
                                </div>
                            </Card>

                            <Card padding="lg">
                                <div className="flex items-center justify-between gap-4">
                                    <div>
                                        <h3 className="font-display text-2xl text-forest-dark">{t('profile.editProfile')}</h3>
                                        <p className="mt-1 text-sm text-forest-light">{t('profile.editHint')}</p>
                                    </div>
                                </div>

                                <div className="mt-5 space-y-4">
                                    <CuteInput
                                        fullWidth
                                        label={t('auth.username')}
                                        value={username}
                                        onChange={event => setUsername(event.target.value)}
                                        placeholder={t('profile.usernamePlaceholder')}
                                    />
                                    {error && <p className="text-sm font-bold text-player-red">{error}</p>}
                                    {success && <p className="text-sm font-bold text-candy-green-dark">{success}</p>}
                                    <div className="flex gap-3">
                                        <CuteButton onClick={() => void handleSaveProfile()} isLoading={isSaving} disabled={backendStatus !== 'healthy'}>
                                            {t('common.save')}
                                        </CuteButton>
                                        <CuteButton
                                            variant="ghost"
                                            onClick={() => {
                                                setUsername(profile?.user.username ?? authUser?.username ?? '');
                                                setError(null);
                                                setSuccess(null);
                                            }}
                                        >
                                            {t('common.cancel')}
                                        </CuteButton>
                                    </div>
                                </div>
                            </Card>

                            <div className="space-y-4">
                                <div>
                                    <h3 className="font-display text-2xl text-forest-dark">{t('profile.stats')}</h3>
                                    <p className="mt-1 text-sm text-forest-light">{t('profile.ratedStatsHint', 'Rated match performance affects your Forest Points.')}</p>
                                </div>

                                <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-3">
                                    {[
                                        [t('profile.totalGames'), stats.total_games],
                                        [t('profile.wins'), stats.wins],
                                        [t('profile.losses'), stats.losses],
                                        [t('profile.draws'), stats.draws],
                                        [t('profile.winRate'), `${stats.win_rate}%`],
                                        [t('profile.currentStreak'), stats.current_streak],
                                        [t('profile.bestStreak'), stats.best_streak],
                                        [t('profile.avgMoveTime'), formatAverageMoveTime(stats.avg_move_time_ms)],
                                        [t('profile.totalPlayTime'), formatPlayTime(stats.total_play_time_ms)],
                                    ].map(([label, value]) => (
                                        <Card key={label as string} padding="md" className="bg-white">
                                            <p className="text-sm font-bold uppercase tracking-[0.12em] text-forest-light">{label}</p>
                                            <p className="mt-3 font-display text-3xl text-forest-dark">{value as string | number}</p>
                                        </Card>
                                    ))}
                                </div>
                            </div>

                            <div className="space-y-4">
                                <div>
                                    <h3 className="font-display text-2xl text-forest-dark">{t('profile.friendlyStatsTitle', 'Friendly Match Stats')}</h3>
                                    <p className="mt-1 text-sm text-forest-light">{t('profile.friendlyStatsHint', 'Friendly matches are tracked in history but do not change your Elo.')}</p>
                                </div>

                                <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-3">
                                    {[
                                        [t('profile.totalGames'), friendlyStats.total_games],
                                        [t('profile.wins'), friendlyStats.wins],
                                        [t('profile.losses'), friendlyStats.losses],
                                        [t('profile.draws'), friendlyStats.draws],
                                        [t('profile.winRate'), `${friendlyStats.win_rate}%`],
                                        [t('profile.currentStreak'), friendlyStats.current_streak],
                                        [t('profile.bestStreak'), friendlyStats.best_streak],
                                        [t('profile.avgMoveTime'), formatAverageMoveTime(friendlyStats.avg_move_time_ms)],
                                        [t('profile.totalPlayTime'), formatPlayTime(friendlyStats.total_play_time_ms)],
                                    ].map(([label, value]) => (
                                        <Card key={`friendly-${label as string}`} padding="md" className="bg-carrot-orange/5">
                                            <p className="text-sm font-bold uppercase tracking-[0.12em] text-carrot-orange-dark">{label}</p>
                                            <p className="mt-3 font-display text-3xl text-forest-dark">{value as string | number}</p>
                                        </Card>
                                    ))}
                                </div>
                            </div>
                        </motion.section>

                        <motion.aside initial={{ opacity: 0, y: 18 }} animate={{ opacity: 1, y: 0 }}>
                            <Card padding="lg" className="h-full">
                                <div className="flex items-center justify-between gap-4">
                                    <div>
                                        <h3 className="font-display text-2xl text-forest-dark">{t('profile.recentGames')}</h3>
                                        <p className="mt-1 text-sm text-forest-light">{t('profile.recentGamesHint')}</p>
                                    </div>
                                </div>

                                <div className="mt-5 space-y-4">
                                    {recentMatches.length === 0 && (
                                        <div className="rounded-2xl bg-cream px-4 py-5 text-sm text-forest-light">
                                            {t('profile.recentEmpty')}
                                        </div>
                                    )}

                                    {recentMatches.map(match => (
                                        <div key={match.match_id} className="rounded-3xl bg-cream p-4">
                                            <div className="flex items-start justify-between gap-3">
                                                <div className="flex items-center gap-3">
                                                    <Avatar
                                                        src={match.opponent_avatar_url}
                                                        alt={match.opponent_username}
                                                        avatarSize="md"
                                                    />
                                                    <div>
                                                        <p className="font-display text-xl text-forest-dark">{match.opponent_username}</p>
                                                        <p className="text-sm text-forest-light">
                                                            {formatMatchTimeControl(match.time_control, t)} · {formatMatchDate(match.ended_at)}
                                                        </p>
                                                    </div>
                                                </div>
                                                <div className="flex flex-col items-end gap-2">
                                                    <span className={`rounded-full px-3 py-1 text-xs font-bold ${matchTypeClasses(match.match_type)}`}>
                                                        {match.match_type === 'Friendly'
                                                            ? t('profile.friendlyMatch', 'Friendly')
                                                            : t('profile.ratedMatch', 'Rated')}
                                                    </span>
                                                    <span className={`rounded-full px-3 py-1 text-xs font-bold ${outcomeClasses(match.outcome)}`}>
                                                        {t(`profile.outcome${match.outcome}`)}
                                                    </span>
                                                </div>
                                            </div>

                                            <div className="mt-4 grid grid-cols-2 gap-3 text-sm">
                                                <div className="rounded-2xl bg-white px-3 py-2">
                                                    <p className="text-forest-light">
                                                        {match.match_type === 'Friendly'
                                                            ? t('profile.friendlyEloLabel', 'Elo impact')
                                                            : t('profile.eloChange')}
                                                    </p>
                                                    <p className={`mt-1 font-bold ${match.match_type === 'Friendly' || match.elo_change >= 0 ? 'text-candy-green-dark' : 'text-player-red-dark'}`}>
                                                        {match.match_type === 'Friendly'
                                                            ? t('profile.noEloChange', 'No Elo change')
                                                            : `${match.elo_change >= 0 ? '+' : ''}${match.elo_change}`}
                                                    </p>
                                                </div>
                                                <div className="rounded-2xl bg-white px-3 py-2">
                                                    <p className="text-forest-light">{t('profile.result')}</p>
                                                    <p className="mt-1 font-bold text-forest-dark">
                                                        {(() => {
                                                            const key = `endReason.${match.result_reason}`;
                                                            const translated = t(key);
                                                            return translated === key ? match.result_reason : translated;
                                                        })()}
                                                    </p>
                                                </div>
                                            </div>
                                        </div>
                                    ))}
                                </div>
                            </Card>
                        </motion.aside>
                    </div>
                )}
            </main>
        </div>
    );
}

export default ProfilePage;
