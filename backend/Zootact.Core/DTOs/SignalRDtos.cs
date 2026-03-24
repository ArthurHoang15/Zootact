using System.Text.Json.Serialization;

namespace Zootact.Core.DTOs;

/// <summary>
/// Request to make a move.
/// </summary>
public record MakeMoveRequest
{
    [JsonPropertyName("from_row")]
    public int FromRow { get; init; }
    
    [JsonPropertyName("from_col")]
    public int FromCol { get; init; }
    
    [JsonPropertyName("to_row")]
    public int ToRow { get; init; }
    
    [JsonPropertyName("to_col")]
    public int ToCol { get; init; }
}

/// <summary>
/// Response from making a move.
/// </summary>
public record MoveResultDto
{
    [JsonPropertyName("success")]
    public bool Success { get; init; }
    
    [JsonPropertyName("error_code")]
    public string? ErrorCode { get; init; }
    
    [JsonPropertyName("error_message")]
    public string? ErrorMessage { get; init; }
}

/// <summary>
/// Match start event sent to clients.
/// </summary>
public record MatchStartDto
{
    [JsonPropertyName("match_id")]
    public required string MatchId { get; init; }
    
    [JsonPropertyName("opponent")]
    public required OpponentDto Opponent { get; init; }
    
    [JsonPropertyName("your_color")]
    public required string YourColor { get; init; }
    
    [JsonPropertyName("time_control")]
    public required TimeControlDto TimeControl { get; init; }
    
    [JsonPropertyName("initial_board")]
    public required BoardDto InitialBoard { get; init; }
}

/// <summary>
/// Move made event sent to clients.
/// </summary>
public record MoveMadeDto
{
    [JsonPropertyName("player_color")]
    public required string PlayerColor { get; init; }
    
    [JsonPropertyName("from")]
    public required PositionDto From { get; init; }
    
    [JsonPropertyName("to")]
    public required PositionDto To { get; init; }
    
    [JsonPropertyName("captured_piece")]
    public string? CapturedPiece { get; init; }
    
    [JsonPropertyName("board_after")]
    public required BoardDto BoardAfter { get; init; }
    
    [JsonPropertyName("blue_time_remaining_ms")]
    public long BlueTimeRemainingMs { get; init; }
    
    [JsonPropertyName("red_time_remaining_ms")]
    public long RedTimeRemainingMs { get; init; }
    
    [JsonPropertyName("move_number")]
    public int MoveNumber { get; init; }
}

/// <summary>
/// Game ended event sent to clients.
/// </summary>
public record GameEndedDto
{
    [JsonPropertyName("result")]
    public required string Result { get; init; }
    
    [JsonPropertyName("reason")]
    public required string Reason { get; init; }
    
    [JsonPropertyName("your_new_elo")]
    public int YourNewElo { get; init; }
    
    [JsonPropertyName("elo_change")]
    public int EloChange { get; init; }
}

/// <summary>
/// Time sync event sent to clients.
/// </summary>
public record TimeSyncDto
{
    [JsonPropertyName("blue_time_remaining_ms")]
    public long BlueTimeRemainingMs { get; init; }
    
    [JsonPropertyName("red_time_remaining_ms")]
    public long RedTimeRemainingMs { get; init; }
    
    [JsonPropertyName("server_timestamp")]
    public required string ServerTimestamp { get; init; }
}

/// <summary>
/// Chat message DTO.
/// </summary>
public record ChatMessageDto
{
    [JsonPropertyName("sender_id")]
    public required string SenderId { get; init; }
    
    [JsonPropertyName("sender_username")]
    public required string SenderUsername { get; init; }
    
    [JsonPropertyName("message")]
    public required string Message { get; init; }
    
    [JsonPropertyName("timestamp")]
    public required string Timestamp { get; init; }
}
