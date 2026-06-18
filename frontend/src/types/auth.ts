/**
 * Authentication Types for Zootact
 */

export type AuthProvider = 'Firebase' | 'Google';

export interface UserDto {
    id: string;
    username: string;
    email: string;
    avatar_url: string | null;
    forest_points: number;
    auth_provider: AuthProvider;
}

export interface UserStatsDto {
    total_games: number;
    wins: number;
    losses: number;
    draws: number;
    win_rate: number;
    current_streak: number;
    best_streak: number;
    avg_move_time_ms: number | null;
    total_play_time_ms: number;
}

export interface RecentProfileMatchDto {
    match_id: string;
    match_type: 'Friendly' | 'Rated';
    time_control: string;
    outcome: 'Win' | 'Loss' | 'Draw';
    result_reason: string;
    opponent_username: string;
    opponent_avatar_url: string | null;
    ended_at: string | null;
    elo_change: number;
}

export interface MyProfileDto {
    user: UserDto;
    stats: UserStatsDto;
    friendly_stats: UserStatsDto;
    recent_matches: RecentProfileMatchDto[];
}

export interface AuthResponse {
    user: UserDto;
    access_token: string;
    refresh_token: string;
    expires_in: number;
    is_new_user?: boolean;
}

export interface RegisterRequest {
    username: string;
    email: string;
    password: string;
}

export interface LoginRequest {
    email: string;
    password: string;
}

export interface GoogleAuthRequest {
    code: string;
}

export interface ForgotPasswordRequest {
    email: string;
}

export interface ResetPasswordRequest {
    token: string;
    new_password: string;
}

export interface AuthError {
    error: string;
    message: string;
}
