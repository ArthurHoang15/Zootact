import { useEffect, useState } from 'react';
import { motion } from 'framer-motion';
import { useTranslation } from 'react-i18next';
import { useNavigate } from 'react-router-dom';
import { Board, GameEndModal, MoveHistory, PlayerInfo, RulesModal } from '@/components/game';
import { CuteButton, LanguageSwitcher } from '@/components/ui';
import { routes } from '@/router/routes';
import { apiService, signalRService } from '@/services';
import { useAuthStore, useGameStore } from '@/stores';
import { sanitizeText } from '@/utils';
import type { PositionDto } from '@/types';

export function GamePage() {
    const { t } = useTranslation();
    const navigate = useNavigate();
    const backendStatus = useAuthStore(state => state.backendStatus);
    const matchId = useGameStore(state => state.matchId);
    const board = useGameStore(state => state.board);
    const chatMessages = useGameStore(state => state.chatMessages);
    const analysis = useGameStore(state => state.analysis);
    const analysisStatus = useGameStore(state => state.analysisStatus);
    const isGameOver = useGameStore(state => state.isGameOver);
    const isOpponentDisconnected = useGameStore(state => state.isOpponentDisconnected);
    const disconnectCountdown = useGameStore(state => state.disconnectCountdown);
    const drawOfferedBy = useGameStore(state => state.drawOfferedBy);
    const setDrawOffered = useGameStore(state => state.setDrawOffered);
    const setAnalysis = useGameStore(state => state.setAnalysis);
    const setAnalysisStatus = useGameStore(state => state.setAnalysisStatus);
    const updateDisconnectCountdown = useGameStore(state => state.updateDisconnectCountdown);
    const [chatInput, setChatInput] = useState('');
    const [isResigning, setIsResigning] = useState(false);
    const [isRulesOpen, setIsRulesOpen] = useState(false);
    const hasActiveBoard = Boolean(matchId && board);
    const canUseLiveActions = hasActiveBoard && backendStatus === 'healthy';

    useEffect(() => {
        if (!matchId) {
            return;
        }

        const handleBlur = () => {
            void signalRService.reportWindowFocus(false);
        };
        const handleFocus = () => {
            void signalRService.reportWindowFocus(true);
        };

        window.addEventListener('blur', handleBlur);
        window.addEventListener('focus', handleFocus);

        return () => {
            window.removeEventListener('blur', handleBlur);
            window.removeEventListener('focus', handleFocus);
        };
    }, [matchId]);

    useEffect(() => {
        if (!isGameOver || !matchId) {
            return;
        }

        setAnalysisStatus('Pending');
        void apiService.getMatchAnalysis(matchId)
            .then(response => setAnalysis(response))
            .catch(() => setAnalysisStatus('Failed'));
    }, [isGameOver, matchId, setAnalysis, setAnalysisStatus]);

    useEffect(() => {
        if (!isOpponentDisconnected || disconnectCountdown <= 0 || isGameOver) {
            return;
        }

        const timeout = window.setTimeout(() => {
            updateDisconnectCountdown(disconnectCountdown - 1);
        }, 1000);

        return () => window.clearTimeout(timeout);
    }, [disconnectCountdown, isGameOver, isOpponentDisconnected, updateDisconnectCountdown]);

    async function handleMove(from: PositionDto, to: PositionDto) {
        if (!canUseLiveActions) {
            return;
        }

        const result = await signalRService.makeMove({
            from_row: from.row,
            from_col: from.col,
            to_row: to.row,
            to_col: to.col,
        });

        if (!result.success) {
            console.error('Move failed', result.error_message);
        }
    }

    async function handleChatSubmit() {
        if (!canUseLiveActions) {
            return;
        }

        const message = sanitizeText(chatInput).trim();
        if (!message) {
            return;
        }

        await signalRService.sendChat(message);
        setChatInput('');
    }

    async function handleResign() {
        if (isResigning) {
            return;
        }

        if (!canUseLiveActions) {
            return;
        }

        const confirmed = window.confirm(t('game.confirmResign'));
        if (!confirmed) {
            return;
        }

        setIsResigning(true);
        try {
            await signalRService.resign();
        } finally {
            setIsResigning(false);
        }
    }

    return (
        <div className="min-h-screen bg-cream">
            <header className="bg-gradient-to-r from-candy-green to-candy-green-light shadow-cute">
                <div className="mx-auto flex max-w-7xl items-center justify-between px-4 py-3">
                    <button className="font-display text-2xl text-white" onClick={() => navigate(routes.home)}>
                        Zootact
                    </button>
                    <div className="flex items-center gap-2">
                        <button
                            className="flex h-11 min-w-11 items-center justify-center rounded-2xl bg-white/90 px-3 font-display text-xl text-forest-dark shadow-cute"
                            aria-label={t('rules.helpIconLabel')}
                            onClick={() => setIsRulesOpen(true)}
                        >
                            <span>?</span>
                            <span className="ml-2 hidden text-sm font-bold md:inline">{t('rules.panelTitle')}</span>
                        </button>
                        <LanguageSwitcher />
                    </div>
                </div>
            </header>

            <main className="mx-auto flex max-w-7xl flex-col gap-4 p-4 lg:flex-row">
                <aside className="hidden w-80 space-y-4 lg:block">
                    <PlayerInfo player="opponent" />
                    <MoveHistory />
                </aside>

                <section className="flex flex-1 flex-col items-center gap-4">
                    <div className="w-full max-w-sm lg:hidden">
                        <PlayerInfo player="opponent" />
                    </div>

                    <div className="w-full max-w-md">
                        <Board onMove={canUseLiveActions ? handleMove : undefined} />
                    </div>

                    <div className="w-full max-w-sm lg:hidden">
                        <PlayerInfo player="me" />
                    </div>

                    {isOpponentDisconnected && (
                        <motion.div
                            className="rounded-2xl bg-carrot-orange px-4 py-2 text-center text-white shadow-cute"
                            initial={{ scale: 0.96, opacity: 0 }}
                            animate={{ scale: 1, opacity: 1 }}
                        >
                            {t('game.waitingReconnect', { seconds: disconnectCountdown })}
                        </motion.div>
                    )}

                    {drawOfferedBy && (
                        <div className="rounded-2xl bg-white p-4 shadow-cute">
                            <p className="font-display">{t('game.drawOffered', { player: drawOfferedBy })}</p>
                            <div className="mt-3 flex gap-2">
                                <CuteButton size="sm" onClick={() => void signalRService.acceptDraw().then(() => setDrawOffered(null))} disabled={!canUseLiveActions}>
                                    {t('game.acceptDraw')}
                                </CuteButton>
                                <CuteButton size="sm" variant="ghost" onClick={() => void signalRService.declineDraw().then(() => setDrawOffered(null))} disabled={!canUseLiveActions}>
                                    {t('game.declineDraw')}
                                </CuteButton>
                            </div>
                        </div>
                    )}

                    {!isGameOver && hasActiveBoard && (
                        <div className="flex gap-3">
                            <CuteButton size="sm" variant="ghost" onClick={() => void signalRService.offerDraw()} disabled={!canUseLiveActions}>
                                {t('game.offerDraw')}
                            </CuteButton>
                            <CuteButton size="sm" variant="danger" onClick={() => void handleResign()} disabled={isResigning || !canUseLiveActions}>
                                {t('game.resign')}
                            </CuteButton>
                        </div>
                    )}
                </section>

                <aside className="w-full space-y-4 lg:w-80">
                    <PlayerInfo player="me" />
                    <div className="rounded-2xl bg-white p-4 shadow-cute">
                        <h3 className="font-display text-xl text-forest-dark">Chat</h3>
                        <div className="mt-3 max-h-48 space-y-2 overflow-y-auto">
                            {chatMessages.map(message => (
                                <div key={`${message.timestamp}-${message.sender_id}`} className="rounded-xl bg-cream px-3 py-2 text-sm">
                                    <strong>{message.sender_username}:</strong> {message.message}
                                </div>
                            ))}
                        </div>
                        <div className="mt-3 flex gap-2">
                            <input
                                className="flex-1 rounded-2xl border border-forest-light/20 px-3 py-2"
                                value={chatInput}
                                disabled={!canUseLiveActions}
                                onChange={event => setChatInput(event.target.value)}
                                onKeyDown={event => {
                                    if (event.key === 'Enter') {
                                        void handleChatSubmit();
                                    }
                                }}
                            />
                            <CuteButton size="sm" onClick={() => void handleChatSubmit()} disabled={!canUseLiveActions}>
                                Send
                            </CuteButton>
                        </div>
                        {!canUseLiveActions && (
                            <p className="mt-3 text-sm text-forest-light">{t('status.actionsDisabled')}</p>
                        )}
                    </div>

                    <div className="rounded-2xl bg-white p-4 shadow-cute">
                        <h3 className="font-display text-xl text-forest-dark">{t('game.smartReplay')}</h3>
                        {!isGameOver && <p className="mt-2 text-sm text-forest-light">{t('game.waiting')}</p>}
                        {isGameOver && analysisStatus === 'Pending' && (
                            <p className="mt-2 text-sm text-forest-light">{t('common.loading')}</p>
                        )}
                        {isGameOver && analysisStatus === 'Failed' && (
                            <p className="mt-2 text-sm text-player-red">{t('common.error')}</p>
                        )}
                        {analysis?.summary && (
                            <div className="mt-3 space-y-3 text-sm">
                                <p>Blue accuracy: {analysis.summary.accuracy_blue}%</p>
                                <p>Red accuracy: {analysis.summary.accuracy_red}%</p>
                                {analysis.moves.slice(0, 6).map(move => (
                                    <div key={move.move_number} className="rounded-xl bg-cream px-3 py-2">
                                        <strong>{move.move_number}.</strong> {move.classification} - {move.cute_label}
                                    </div>
                                ))}
                                {analysis.anti_cheat.map(item => (
                                    <div key={item.user_id} className="rounded-xl bg-cream px-3 py-2">
                                        <strong>{item.suspicion_level}</strong> · blur {item.blur_count} · confidence {Math.round(item.confidence_score * 100)}%
                                    </div>
                                ))}
                            </div>
                        )}
                    </div>
                </aside>
            </main>

            <GameEndModal onNewGame={() => navigate(routes.home)} />
            <RulesModal
                open={isRulesOpen}
                onClose={() => setIsRulesOpen(false)}
                onOpenPage={() => navigate(routes.rules)}
            />
        </div>
    );
}

export default GamePage;
