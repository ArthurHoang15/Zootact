using System.Text.Json.Serialization;

namespace Zootact.Core.DTOs;

public record LobbyPlayerDto
{
    [JsonPropertyName("id")]
    public required string Id { get; init; }

    [JsonPropertyName("username")]
    public required string Username { get; init; }

    [JsonPropertyName("avatar_url")]
    public string? AvatarUrl { get; init; }

    [JsonPropertyName("forest_points")]
    public int ForestPoints { get; init; }

    [JsonPropertyName("is_host")]
    public bool IsHost { get; init; }

    [JsonPropertyName("is_ready")]
    public bool IsReady { get; init; }
}

public record PrivateLobbyDto
{
    [JsonPropertyName("lobby_id")]
    public required string LobbyId { get; init; }

    [JsonPropertyName("mode")]
    public required string Mode { get; init; }

    [JsonPropertyName("host")]
    public required LobbyPlayerDto Host { get; init; }

    [JsonPropertyName("guest")]
    public LobbyPlayerDto? Guest { get; init; }

    [JsonPropertyName("current_user_role")]
    public required string CurrentUserRole { get; init; }

    [JsonPropertyName("countdown_active")]
    public bool CountdownActive { get; init; }

    [JsonPropertyName("countdown_end_at")]
    public DateTimeOffset? CountdownEndAt { get; init; }

    [JsonPropertyName("countdown_seconds_remaining")]
    public int CountdownSecondsRemaining { get; init; }

    [JsonPropertyName("can_start")]
    public bool CanStart { get; init; }
}

public record LobbyActionResponse
{
    [JsonPropertyName("success")]
    public bool Success { get; init; } = true;

    [JsonPropertyName("message")]
    public required string Message { get; init; }

    [JsonPropertyName("lobby")]
    public PrivateLobbyDto? Lobby { get; init; }
}

public record CreateLobbyRequest
{ }

public record LobbyReadyRequest
{
    [JsonPropertyName("ready")]
    public bool Ready { get; init; }
}

public record LobbyCountdownStartedDto
{
    [JsonPropertyName("lobby_id")]
    public required string LobbyId { get; init; }

    [JsonPropertyName("countdown_end_at")]
    public required DateTimeOffset CountdownEndAt { get; init; }

    [JsonPropertyName("seconds_remaining")]
    public int SecondsRemaining { get; init; }
}

public record LobbyClosedDto
{
    [JsonPropertyName("lobby_id")]
    public required string LobbyId { get; init; }

    [JsonPropertyName("reason")]
    public required string Reason { get; init; }
}
