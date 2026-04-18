import { useAuthStore } from '@/stores';
import { fetchJson, isUnauthorizedApiError } from './apiErrors';
import type {
    ActiveMatchResponse,
    LobbyActionResponse,
    MatchAnalysisResponse,
    MatchmakingResponse,
    MyProfileDto,
    PrivateLobbyDto,
} from '@/types';

const API_BASE = '/api';

class ApiService {
    private getHeaders(): HeadersInit {
        const headers: HeadersInit = {
            'Content-Type': 'application/json',
        };

        // Use firebaseToken instead of accessToken
        const token = useAuthStore.getState().firebaseToken;
        if (token) {
            headers['Authorization'] = `Bearer ${token}`;
        }

        return headers;
    }

    private async request<T>(input: RequestInfo | URL, init?: RequestInit): Promise<T> {
        try {
            return await fetchJson<T>(input, init);
        } catch (error) {
            if (isUnauthorizedApiError(error)) {
                void useAuthStore.getState().logout();
            }

            throw error;
        }
    }

    // === Matchmaking ===

    async joinQueue(timeControl: 'Blitz' | 'Rapid' | 'Classical'): Promise<MatchmakingResponse> {
        return this.request<MatchmakingResponse>(`${API_BASE}/matchmaking/queue`, {
            method: 'POST',
            headers: this.getHeaders(),
            body: JSON.stringify({ time_control: timeControl }),
        });
    }

    async leaveQueue(): Promise<{ success: boolean }> {
        return this.request<{ success: boolean }>(`${API_BASE}/matchmaking/queue`, {
            method: 'DELETE',
            headers: this.getHeaders(),
        });
    }

    async getActiveMatch(): Promise<ActiveMatchResponse | null> {
        return this.request<ActiveMatchResponse | null>(`${API_BASE}/match/active`, {
            method: 'GET',
            headers: this.getHeaders(),
        });
    }

    async getMatchAnalysis(matchId: string): Promise<MatchAnalysisResponse> {
        return this.request<MatchAnalysisResponse>(`${API_BASE}/match/${matchId}/analysis`, {
            method: 'GET',
            headers: this.getHeaders(),
        });
    }

    // === Private Lobby ===

    async createLobby(): Promise<LobbyActionResponse> {
        return this.request<LobbyActionResponse>(`${API_BASE}/lobbies`, {
            method: 'POST',
            headers: this.getHeaders(),
        });
    }

    async getLobby(lobbyId: string): Promise<PrivateLobbyDto> {
        return this.request<PrivateLobbyDto>(`${API_BASE}/lobbies/${lobbyId}`, {
            method: 'GET',
            headers: this.getHeaders(),
        });
    }

    async getActiveLobby(): Promise<PrivateLobbyDto | null> {
        return this.request<PrivateLobbyDto | null>(`${API_BASE}/lobbies/active`, {
            method: 'GET',
            headers: this.getHeaders(),
        });
    }

    async joinLobby(lobbyId: string): Promise<LobbyActionResponse> {
        return this.request<LobbyActionResponse>(`${API_BASE}/lobbies/${lobbyId}/join`, {
            method: 'POST',
            headers: this.getHeaders(),
        });
    }

    async leaveLobby(lobbyId: string): Promise<LobbyActionResponse> {
        return this.request<LobbyActionResponse>(`${API_BASE}/lobbies/${lobbyId}/leave`, {
            method: 'POST',
            headers: this.getHeaders(),
        });
    }

    async setLobbyReady(lobbyId: string, ready: boolean): Promise<LobbyActionResponse> {
        return this.request<LobbyActionResponse>(`${API_BASE}/lobbies/${lobbyId}/ready`, {
            method: 'POST',
            headers: this.getHeaders(),
            body: JSON.stringify({ ready }),
        });
    }

    async startLobby(lobbyId: string): Promise<LobbyActionResponse> {
        return this.request<LobbyActionResponse>(`${API_BASE}/lobbies/${lobbyId}/start`, {
            method: 'POST',
            headers: this.getHeaders(),
        });
    }

    async cancelLobbyStart(lobbyId: string): Promise<LobbyActionResponse> {
        return this.request<LobbyActionResponse>(`${API_BASE}/lobbies/${lobbyId}/cancel-start`, {
            method: 'POST',
            headers: this.getHeaders(),
        });
    }

    // === User ===

    async getMyProfile(): Promise<MyProfileDto> {
        return this.request<MyProfileDto>(`${API_BASE}/auth/profile`, {
            method: 'GET',
            headers: this.getHeaders(),
        });
    }

    async updateMyProfile(payload: { username: string }): Promise<MyProfileDto> {
        return this.request<MyProfileDto>(`${API_BASE}/auth/profile`, {
            method: 'PATCH',
            headers: this.getHeaders(),
            body: JSON.stringify(payload),
        });
    }
}

export const apiService = new ApiService();
export default apiService;
