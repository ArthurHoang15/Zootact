using System.Text.Json.Serialization;

namespace Zootact.Core.DTOs;

/// <summary>
/// User data transfer object.
/// </summary>
public record UserDto
{
    [JsonPropertyName("id")]
    public required string Id { get; init; }
    
    [JsonPropertyName("username")]
    public required string Username { get; init; }
    
    [JsonPropertyName("email")]
    public required string Email { get; init; }
    
    [JsonPropertyName("avatar_url")]
    public string? AvatarUrl { get; init; }
    
    [JsonPropertyName("forest_points")]
    public int ForestPoints { get; init; }
    
    [JsonPropertyName("auth_provider")]
    public required string AuthProvider { get; init; }
}

/// <summary>
/// Current user statistics data transfer object.
/// </summary>
public record UserStatsDto
{
    [JsonPropertyName("total_games")]
    public int TotalGames { get; init; }

    [JsonPropertyName("wins")]
    public int Wins { get; init; }

    [JsonPropertyName("losses")]
    public int Losses { get; init; }

    [JsonPropertyName("draws")]
    public int Draws { get; init; }

    [JsonPropertyName("win_rate")]
    public decimal WinRate { get; init; }

    [JsonPropertyName("current_streak")]
    public int CurrentStreak { get; init; }

    [JsonPropertyName("best_streak")]
    public int BestStreak { get; init; }

    [JsonPropertyName("avg_move_time_ms")]
    public decimal? AvgMoveTimeMs { get; init; }

    [JsonPropertyName("total_play_time_ms")]
    public long TotalPlayTimeMs { get; init; }
}

/// <summary>
/// Recent match summary for the current user profile.
/// </summary>
public record RecentProfileMatchDto
{
    [JsonPropertyName("match_id")]
    public required string MatchId { get; init; }

    [JsonPropertyName("match_type")]
    public required string MatchType { get; init; }

    [JsonPropertyName("time_control")]
    public required string TimeControl { get; init; }

    [JsonPropertyName("outcome")]
    public required string Outcome { get; init; }

    [JsonPropertyName("result_reason")]
    public required string ResultReason { get; init; }

    [JsonPropertyName("opponent_username")]
    public required string OpponentUsername { get; init; }

    [JsonPropertyName("opponent_avatar_url")]
    public string? OpponentAvatarUrl { get; init; }

    [JsonPropertyName("ended_at")]
    public DateTimeOffset? EndedAt { get; init; }

    [JsonPropertyName("elo_change")]
    public int EloChange { get; init; }
}

/// <summary>
/// Full current user profile data.
/// </summary>
public record MyProfileDto
{
    [JsonPropertyName("user")]
    public required UserDto User { get; init; }

    [JsonPropertyName("stats")]
    public required UserStatsDto Stats { get; init; }

    [JsonPropertyName("friendly_stats")]
    public required UserStatsDto FriendlyStats { get; init; }

    [JsonPropertyName("recent_matches")]
    public required IReadOnlyList<RecentProfileMatchDto> RecentMatches { get; init; }
}

/// <summary>
/// Authentication response with tokens.
/// </summary>
public record AuthResponse
{
    [JsonPropertyName("user")]
    public required UserDto User { get; init; }
    
    [JsonPropertyName("access_token")]
    public required string AccessToken { get; init; }
    
    [JsonPropertyName("refresh_token")]
    public required string RefreshToken { get; init; }
    
    [JsonPropertyName("expires_in")]
    public int ExpiresIn { get; init; }
    
    [JsonPropertyName("is_new_user")]
    public bool IsNewUser { get; init; }
}

/// <summary>
/// Registration request.
/// </summary>
public record RegisterRequest
{
    [JsonPropertyName("username")]
    public required string Username { get; init; }
    
    [JsonPropertyName("email")]
    public required string Email { get; init; }
    
    [JsonPropertyName("password")]
    public required string Password { get; init; }
}

/// <summary>
/// Login request.
/// </summary>
public record LoginRequest
{
    [JsonPropertyName("email")]
    public required string Email { get; init; }
    
    [JsonPropertyName("password")]
    public required string Password { get; init; }
}

/// <summary>
/// Google OAuth request.
/// </summary>
public record GoogleAuthRequest
{
    [JsonPropertyName("id_token")]
    public required string IdToken { get; init; }
}

/// <summary>
/// Forgot password request.
/// </summary>
public record ForgotPasswordRequest
{
    [JsonPropertyName("email")]
    public required string Email { get; init; }
}

/// <summary>
/// Reset password request.
/// </summary>
public record ResetPasswordRequest
{
    [JsonPropertyName("token")]
    public required string Token { get; init; }
    
    [JsonPropertyName("new_password")]
    public required string NewPassword { get; init; }
}

/// <summary>
/// Generic message response.
/// </summary>
public record MessageResponse
{
    [JsonPropertyName("message")]
    public required string Message { get; init; }
    
    [JsonPropertyName("success")]
    public bool Success { get; init; } = true;
}

/// <summary>
/// Error response.
/// </summary>
public record ErrorResponse
{
    [JsonPropertyName("error")]
    public required string Error { get; init; }
    
    [JsonPropertyName("message")]
    public required string Message { get; init; }
}
