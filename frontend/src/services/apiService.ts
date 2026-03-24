import { useAuthStore } from '@/stores';
import type {
    ActiveMatchResponse,
    CreateLobbyRequest,
    LobbyActionResponse,
    MatchAnalysisResponse,
    MatchmakingResponse,
    MyProfileDto,
    PrivateLobbyDto,
} from '@/types';

const API_BASE = '/api';

interface ApiError {
    error: string;
    message: string;
}

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

    private async handleResponse<T>(response: Response): Promise<T> {
        if (!response.ok) {
            const error: ApiError = await response.json().catch(() => ({
                error: 'unknown',
                message: response.statusText,
            }));
            throw new Error(error.message || 'Request failed');
        }

        // Handle 204 No Content
        if (response.status === 204) {
            return null as T;
        }

        return response.json();
    }

    // === Matchmaking ===

    async joinQueue(timeControl: 'Blitz' | 'Rapid' | 'Classical'): Promise<MatchmakingResponse> {
        const response = await fetch(`${API_BASE}/matchmaking/queue`, {
            method: 'POST',
            headers: this.getHeaders(),
            body: JSON.stringify({ time_control: timeControl }),
        });
        return this.handleResponse(response);
    }

    async leaveQueue(): Promise<{ success: boolean }> {
        const response = await fetch(`${API_BASE}/matchmaking/queue`, {
            method: 'DELETE',
            headers: this.getHeaders(),
        });
        return this.handleResponse(response);
    }

    async getActiveMatch(): Promise<ActiveMatchResponse | null> {
        const response = await fetch(`${API_BASE}/match/active`, {
            method: 'GET',
            headers: this.getHeaders(),
        });

        if (response.status === 204) {
            return null;
        }

        return this.handleResponse(response);
    }

    async getMatchAnalysis(matchId: string): Promise<MatchAnalysisResponse> {
        const response = await fetch(`${API_BASE}/match/${matchId}/analysis`, {
            method: 'GET',
            headers: this.getHeaders(),
        });
        return this.handleResponse(response);
    }

    // === Private Lobby ===

    async createLobby(payload: CreateLobbyRequest): Promise<LobbyActionResponse> {
        const response = await fetch(`${API_BASE}/lobbies`, {
            method: 'POST',
            headers: this.getHeaders(),
            body: JSON.stringify(payload),
        });
        return this.handleResponse(response);
    }

    async getLobby(lobbyId: string): Promise<PrivateLobbyDto> {
        const response = await fetch(`${API_BASE}/lobbies/${lobbyId}`, {
            method: 'GET',
            headers: this.getHeaders(),
        });
        return this.handleResponse(response);
    }

    async joinLobby(lobbyId: string): Promise<LobbyActionResponse> {
        const response = await fetch(`${API_BASE}/lobbies/${lobbyId}/join`, {
            method: 'POST',
            headers: this.getHeaders(),
        });
        return this.handleResponse(response);
    }

    async leaveLobby(lobbyId: string): Promise<LobbyActionResponse> {
        const response = await fetch(`${API_BASE}/lobbies/${lobbyId}/leave`, {
            method: 'POST',
            headers: this.getHeaders(),
        });
        return this.handleResponse(response);
    }

    async setLobbyReady(lobbyId: string, ready: boolean): Promise<LobbyActionResponse> {
        const response = await fetch(`${API_BASE}/lobbies/${lobbyId}/ready`, {
            method: 'POST',
            headers: this.getHeaders(),
            body: JSON.stringify({ ready }),
        });
        return this.handleResponse(response);
    }

    async startLobby(lobbyId: string): Promise<LobbyActionResponse> {
        const response = await fetch(`${API_BASE}/lobbies/${lobbyId}/start`, {
            method: 'POST',
            headers: this.getHeaders(),
        });
        return this.handleResponse(response);
    }

    async cancelLobbyStart(lobbyId: string): Promise<LobbyActionResponse> {
        const response = await fetch(`${API_BASE}/lobbies/${lobbyId}/cancel-start`, {
            method: 'POST',
            headers: this.getHeaders(),
        });
        return this.handleResponse(response);
    }

    // === User ===

    async getMyProfile(): Promise<MyProfileDto> {
        const response = await fetch(`${API_BASE}/auth/profile`, {
            method: 'GET',
            headers: this.getHeaders(),
        });
        return this.handleResponse(response);
    }

    async updateMyProfile(payload: { username: string }): Promise<MyProfileDto> {
        const response = await fetch(`${API_BASE}/auth/profile`, {
            method: 'PATCH',
            headers: this.getHeaders(),
            body: JSON.stringify(payload),
        });
        return this.handleResponse(response);
    }
}

export const apiService = new ApiService();
export default apiService;
