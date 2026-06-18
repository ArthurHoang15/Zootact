import { create } from 'zustand';
import { devtools } from 'zustand/middleware';
import type {
    LobbyClosedDto,
    LobbyCountdownStartedDto,
    LobbyRole,
    PrivateLobbyDto,
} from '@/types';

interface LobbyState {
    lobby: PrivateLobbyDto | null;
    isLoading: boolean;
    error: string | null;
    closedReason: string | null;
}

interface LobbyActions {
    setLobby: (lobby: PrivateLobbyDto | null) => void;
    setLoading: (isLoading: boolean) => void;
    setError: (error: string | null) => void;
    applyLobbyUpdate: (lobby: PrivateLobbyDto) => void;
    applyCountdownStarted: (event: LobbyCountdownStartedDto) => void;
    setLobbyClosed: (event: LobbyClosedDto) => void;
    clearLobby: () => void;
}

const initialState: LobbyState = {
    lobby: null,
    isLoading: false,
    error: null,
    closedReason: null,
};

export const useLobbyStore = create<LobbyState & LobbyActions>()(
    devtools((set, get) => ({
        ...initialState,

        setLobby: lobby => set({ lobby, error: null, closedReason: null, isLoading: false }),
        setLoading: isLoading => set({ isLoading }),
        setError: error => set({ error, isLoading: false }),
        applyLobbyUpdate: lobby => {
            const currentLobby = get().lobby;
            const currentRole = currentLobby?.current_user_role;
            const nextRole: LobbyRole =
                lobby.current_user_role === 'Unknown' && currentRole && currentLobby?.lobby_id === lobby.lobby_id
                    ? currentRole
                    : lobby.current_user_role;

            set({
                lobby: {
                    ...lobby,
                    current_user_role: nextRole,
                },
                error: null,
                closedReason: null,
                isLoading: false,
            });
        },
        applyCountdownStarted: event =>
            set(state => ({
                lobby: state.lobby && state.lobby.lobby_id === event.lobby_id
                    ? {
                        ...state.lobby,
                        countdown_active: true,
                        countdown_end_at: event.countdown_end_at,
                        countdown_seconds_remaining: event.seconds_remaining,
                    }
                    : state.lobby,
            })),
        setLobbyClosed: event =>
            set(state => ({
                lobby: state.lobby?.lobby_id === event.lobby_id ? null : state.lobby,
                closedReason: state.lobby?.lobby_id === event.lobby_id ? event.reason : state.closedReason,
                isLoading: false,
            })),
        clearLobby: () => set(initialState),
    }), { name: 'lobby-store' })
);
