using System.Text.Json.Serialization;

namespace Zootact.Core.DTOs;

/// <summary>
/// Position DTO for API responses.
/// </summary>
public record PositionDto
{
    [JsonPropertyName("row")]
    public int Row { get; init; }
    
    [JsonPropertyName("col")]
    public int Col { get; init; }
}

/// <summary>
/// Piece DTO for API responses.
/// </summary>
public record PieceDto
{
    [JsonPropertyName("type")]
    public required string Type { get; init; }
    
    [JsonPropertyName("owner")]
    public required string Owner { get; init; }
    
    [JsonPropertyName("rank")]
    public int Rank { get; init; }
}

/// <summary>
/// Board DTO representing the full board state.
/// </summary>
public record BoardDto
{
    [JsonPropertyName("cells")]
    public required PieceDto?[][] Cells { get; init; }
}

/// <summary>
/// Time control DTO.
/// </summary>
public record TimeControlDto
{
    [JsonPropertyName("preset")]
    public required string Preset { get; init; }

    [JsonPropertyName("is_untimed")]
    public bool IsUntimed { get; init; }

    [JsonPropertyName("clock_mode")]
    public required string ClockMode { get; init; }
    
    [JsonPropertyName("initial_time_ms")]
    public int InitialTimeMs { get; init; }
    
    [JsonPropertyName("increment_ms")]
    public int IncrementMs { get; init; }
}

/// <summary>
/// Opponent DTO.
/// </summary>
public record OpponentDto
{
    [JsonPropertyName("id")]
    public required string Id { get; init; }
    
    [JsonPropertyName("username")]
    public required string Username { get; init; }
    
    [JsonPropertyName("avatar_url")]
    public string? AvatarUrl { get; init; }
    
    [JsonPropertyName("forest_points")]
    public int ForestPoints { get; init; }
}

/// <summary>
/// Game state DTO for API responses.
/// </summary>
public record GameStateDto
{
    [JsonPropertyName("match_id")]
    public required string MatchId { get; init; }
    
    [JsonPropertyName("blue_player")]
    public required OpponentDto BluePlayer { get; init; }
    
    [JsonPropertyName("red_player")]
    public required OpponentDto RedPlayer { get; init; }
    
    [JsonPropertyName("your_color")]
    public required string YourColor { get; init; }
    
    [JsonPropertyName("current_turn")]
    public required string CurrentTurn { get; init; }
    
    [JsonPropertyName("board")]
    public required BoardDto Board { get; init; }
    
    [JsonPropertyName("time_control")]
    public required TimeControlDto TimeControl { get; init; }
    
    [JsonPropertyName("blue_time_remaining_ms")]
    public long BlueTimeRemainingMs { get; init; }
    
    [JsonPropertyName("red_time_remaining_ms")]
    public long RedTimeRemainingMs { get; init; }
    
    [JsonPropertyName("move_count")]
    public int MoveCount { get; init; }
    
    [JsonPropertyName("status")]
    public required string Status { get; init; }

    [JsonPropertyName("result")]
    public required string Result { get; init; }

    [JsonPropertyName("result_reason")]
    public string? ResultReason { get; init; }

    [JsonPropertyName("move_history")]
    public required List<string> MoveHistory { get; init; }
}

/// <summary>
/// Active match response.
/// </summary>
public record ActiveMatchResponse
{
    [JsonPropertyName("match_id")]
    public required string MatchId { get; init; }
    
    [JsonPropertyName("game_state")]
    public required GameStateDto GameState { get; init; }
}
