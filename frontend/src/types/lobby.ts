export type LobbyRole = 'Host' | 'Guest' | 'Unknown';

export interface LobbyPlayerDto {
    id: string;
    username: string;
    avatar_url: string | null;
    forest_points: number;
    is_host: boolean;
    is_ready: boolean;
}

export interface PrivateLobbyDto {
    lobby_id: string;
    mode: string;
    host: LobbyPlayerDto;
    guest: LobbyPlayerDto | null;
    current_user_role: LobbyRole;
    countdown_active: boolean;
    countdown_end_at: string | null;
    countdown_seconds_remaining: number;
    can_start: boolean;
}

export interface LobbyActionResponse {
    success: boolean;
    message: string;
    lobby: PrivateLobbyDto | null;
}

export interface LobbyCountdownStartedDto {
    lobby_id: string;
    countdown_end_at: string;
    seconds_remaining: number;
}

export interface LobbyClosedDto {
    lobby_id: string;
    reason: string;
}
