import { create } from 'zustand';
import { devtools } from 'zustand/middleware';
import type {
    ActiveMatchResponse,
    ChatMessageDto,
    GameEndedDto,
    GameEndReason,
    GameMove,
    MatchAnalysisResponse,
    MatchStartDto,
    MoveMadeDto,
    OpponentDto,
    PieceDto,
    Player,
    PositionDto,
    BoardDto,
    GameResult,
} from '@/types';
import { getValidMoves } from '@/utils/moveValidator';

interface GameState {
    matchId: string | null;
    myColor: Player | null;
    me: OpponentDto | null;
    opponent: OpponentDto | null;
    board: BoardDto | null;
    currentTurn: Player;
    myTimeMs: number;
    opponentTimeMs: number;
    lastTimeSync: number;
    selectedPiece: PositionDto | null;
    validMoves: PositionDto[];
    moveHistory: GameMove[];
    chatMessages: ChatMessageDto[];
    analysis: MatchAnalysisResponse | null;
    analysisStatus: string | null;
    clockMode: 'countdown' | 'countup';
    isUntimed: boolean;
    isGameOver: boolean;
    result: GameResult;
    endReason: GameEndReason | null;
    eloChange: number;
    isConnected: boolean;
    isOpponentDisconnected: boolean;
    disconnectCountdown: number;
    pendingDrawOffer: boolean;
    drawOfferedBy: string | null;
}

interface GameActions {
    initMatch: (data: MatchStartDto) => void;
    hydrateActiveMatch: (data: ActiveMatchResponse) => void;
    endGame: (data: GameEndedDto) => void;
    resetGame: () => void;
    selectPiece: (position: PositionDto) => void;
    clearSelection: () => void;
    applyMove: (data: MoveMadeDto) => void;
    syncTime: (blueMs: number, redMs: number) => void;
    tickTime: (deltaMs: number) => void;
    setConnected: (connected: boolean) => void;
    setOpponentDisconnected: (disconnected: boolean, countdown?: number) => void;
    updateDisconnectCountdown: (seconds: number) => void;
    setDrawOffered: (offeredBy: string | null) => void;
    setPendingDrawOffer: (pending: boolean) => void;
    addChatMessage: (message: ChatMessageDto) => void;
    setAnalysis: (analysis: MatchAnalysisResponse | null) => void;
    setAnalysisStatus: (status: string | null) => void;
}

const initialState: GameState = {
    matchId: null,
    myColor: null,
    me: null,
    opponent: null,
    board: null,
    currentTurn: 'Blue',
    myTimeMs: 0,
    opponentTimeMs: 0,
    lastTimeSync: Date.now(),
    selectedPiece: null,
    validMoves: [],
    moveHistory: [],
    chatMessages: [],
    analysis: null,
    analysisStatus: null,
    clockMode: 'countdown',
    isUntimed: false,
    isGameOver: false,
    result: 'InProgress',
    endReason: null,
    eloChange: 0,
    isConnected: false,
    isOpponentDisconnected: false,
    disconnectCountdown: 0,
    pendingDrawOffer: false,
    drawOfferedBy: null,
};

export const useGameStore = create<GameState & GameActions>()(
    devtools((set, get) => ({
        ...initialState,

        initMatch: data => {
            set({
                matchId: data.match_id,
                myColor: data.your_color,
                opponent: data.opponent,
                board: data.initial_board,
                currentTurn: 'Blue',
                myTimeMs: data.time_control.initial_time_ms,
                opponentTimeMs: data.time_control.initial_time_ms,
                lastTimeSync: Date.now(),
                moveHistory: [],
                chatMessages: [],
                analysis: null,
                analysisStatus: null,
                clockMode: data.time_control.clock_mode,
                isUntimed: data.time_control.is_untimed,
                isGameOver: false,
                result: 'InProgress',
                endReason: null,
                eloChange: 0,
                selectedPiece: null,
                validMoves: [],
                isConnected: true,
                isOpponentDisconnected: false,
                disconnectCountdown: 0,
                pendingDrawOffer: false,
                drawOfferedBy: null,
            });
        },

        hydrateActiveMatch: data => {
            const state = data.game_state;
            const me = state.your_color === 'Blue' ? state.blue_player : state.red_player;
            const opponent = state.your_color === 'Blue' ? state.red_player : state.blue_player;

            set({
                matchId: data.match_id,
                myColor: state.your_color,
                me,
                opponent,
                board: state.board,
                currentTurn: state.current_turn,
                myTimeMs: state.your_color === 'Blue' ? state.blue_time_remaining_ms : state.red_time_remaining_ms,
                opponentTimeMs: state.your_color === 'Blue' ? state.red_time_remaining_ms : state.blue_time_remaining_ms,
                lastTimeSync: Date.now(),
                moveHistory: [],
                chatMessages: [],
                analysis: null,
                analysisStatus: null,
                clockMode: state.time_control.clock_mode,
                isUntimed: state.time_control.is_untimed,
                isGameOver: state.result !== 'InProgress',
                result: state.result,
                endReason: state.result_reason,
                selectedPiece: null,
                validMoves: [],
                isConnected: true,
                isOpponentDisconnected: false,
                disconnectCountdown: 0,
                drawOfferedBy: null,
            });
        },

    endGame: data => {
        set({
            isGameOver: true,
            result: data.result,
            endReason: data.reason,
            eloChange: data.elo_change,
            selectedPiece: null,
            validMoves: [],
            isOpponentDisconnected: false,
            disconnectCountdown: 0,
        });
    },

        resetGame: () => set(initialState),

        selectPiece: position => {
            const { board, myColor, currentTurn, isGameOver } = get();
            if (isGameOver || currentTurn !== myColor || !board) {
                return;
            }

            const piece = board.cells[position.row]?.[position.col];
            if (!piece || piece.owner !== myColor) {
                const { validMoves, selectedPiece } = get();
                const isValidMove = validMoves.some(move => move.row === position.row && move.col === position.col);
                if (!isValidMove || !selectedPiece) {
                    set({ selectedPiece: null, validMoves: [] });
                }
                return;
            }

            set({
                selectedPiece: position,
                validMoves: getValidMoves(board, position, piece),
            });
        },

        clearSelection: () => set({ selectedPiece: null, validMoves: [] }),

        applyMove: data => {
            const { myColor, moveHistory, board } = get();
            const movedPiece = board?.cells[data.from.row]?.[data.from.col];

            const move: GameMove = {
                from: data.from,
                to: data.to,
                piece: movedPiece as PieceDto,
                capturedPiece: data.captured_piece ? (board?.cells[data.to.row]?.[data.to.col] as PieceDto) : null,
                timestamp: Date.now(),
                moveNumber: data.move_number,
            };

            set({
                board: data.board_after,
                currentTurn: data.player_color === 'Blue' ? 'Red' : 'Blue',
                myTimeMs: myColor === 'Blue' ? data.blue_time_remaining_ms : data.red_time_remaining_ms,
                opponentTimeMs: myColor === 'Blue' ? data.red_time_remaining_ms : data.blue_time_remaining_ms,
                lastTimeSync: Date.now(),
                moveHistory: [...moveHistory, move],
                selectedPiece: null,
                validMoves: [],
            });
        },

        syncTime: (blueMs, redMs) => {
            const { myColor } = get();
            set({
                myTimeMs: myColor === 'Blue' ? blueMs : redMs,
                opponentTimeMs: myColor === 'Blue' ? redMs : blueMs,
                lastTimeSync: Date.now(),
            });
        },

        tickTime: deltaMs => {
            const { currentTurn, myColor, isGameOver } = get();
            if (isGameOver) {
                return;
            }

            if (get().clockMode === 'countup') {
                if (currentTurn === myColor) {
                    set(state => ({ myTimeMs: state.myTimeMs + deltaMs }));
                } else {
                    set(state => ({ opponentTimeMs: state.opponentTimeMs + deltaMs }));
                }
            } else {
                if (currentTurn === myColor) {
                    set(state => ({ myTimeMs: Math.max(0, state.myTimeMs - deltaMs) }));
                } else {
                    set(state => ({ opponentTimeMs: Math.max(0, state.opponentTimeMs - deltaMs) }));
                }
            }
        },

        setConnected: connected => set({ isConnected: connected }),

        setOpponentDisconnected: (disconnected, countdown = 60) =>
            set({
                isOpponentDisconnected: disconnected,
                disconnectCountdown: disconnected ? countdown : 0,
            }),

        updateDisconnectCountdown: seconds => set({ disconnectCountdown: Math.max(0, seconds) }),
        setDrawOffered: offeredBy => set({ drawOfferedBy: offeredBy }),
        setPendingDrawOffer: pending => set({ pendingDrawOffer: pending }),
        addChatMessage: message => set(state => ({ chatMessages: [...state.chatMessages, message] })),
        setAnalysis: analysis => set({ analysis, analysisStatus: analysis?.status ?? null }),
        setAnalysisStatus: status => set({ analysisStatus: status }),
    }), { name: 'game-store' })
);

export const selectIsMyTurn = (state: GameState) =>
    state.currentTurn === state.myColor && !state.isGameOver;

export const selectCanSelect = (state: GameState) =>
    state.currentTurn === state.myColor && !state.isGameOver && state.isConnected;

export const selectGameStatus = (state: GameState) => ({
    isGameOver: state.isGameOver,
    result: state.result,
    endReason: state.endReason,
    eloChange: state.eloChange,
});
