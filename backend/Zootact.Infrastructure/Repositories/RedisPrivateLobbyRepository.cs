using StackExchange.Redis;
using Zootact.Core.Domain;
using Zootact.Core.Interfaces;

namespace Zootact.Infrastructure.Repositories;

public sealed class RedisPrivateLobbyRepository(IConnectionMultiplexer redis) : IPrivateLobbyRepository
{
    private IDatabase Db => redis.GetDatabase();
    private static readonly TimeSpan LobbyTtl = TimeSpan.FromHours(6);

    private static string LobbyKey(Guid lobbyId) => $"lobby:{lobbyId}";
    private static string PlayerActiveLobbyKey(Guid userId) => $"player:{userId}:active_lobby";
    private const string CountdownIndexKey = "private-lobbies:countdown";

    public async Task<PrivateLobby?> GetLobbyAsync(Guid lobbyId)
    {
        var hash = await Db.HashGetAllAsync(LobbyKey(lobbyId));
        if (hash.Length == 0)
        {
            return null;
        }

        var dict = hash.ToDictionary(x => x.Name.ToString(), x => x.Value.ToString());
        return DeserializeLobby(dict);
    }

    public async Task SaveLobbyAsync(PrivateLobby lobby)
    {
        var key = LobbyKey(lobby.LobbyId);
        await Db.HashSetAsync(key, SerializeLobby(lobby));
        await Db.KeyExpireAsync(key, LobbyTtl);
    }

    public async Task DeleteLobbyAsync(Guid lobbyId)
    {
        await Db.KeyDeleteAsync(LobbyKey(lobbyId));
        await ClearCountdownAsync(lobbyId);
    }

    public async Task<Guid?> GetPlayerActiveLobbyAsync(Guid userId)
    {
        var value = await Db.StringGetAsync(PlayerActiveLobbyKey(userId));
        return value.IsNullOrEmpty ? null : Guid.TryParse(value.ToString(), out var lobbyId) ? lobbyId : null;
    }

    public Task SetPlayerActiveLobbyAsync(Guid userId, Guid lobbyId) =>
        Db.StringSetAsync(PlayerActiveLobbyKey(userId), lobbyId.ToString(), LobbyTtl);

    public Task ClearPlayerActiveLobbyAsync(Guid userId) =>
        Db.KeyDeleteAsync(PlayerActiveLobbyKey(userId));

    public Task ScheduleCountdownAsync(Guid lobbyId, DateTimeOffset dueAt) =>
        Db.SortedSetAddAsync(CountdownIndexKey, lobbyId.ToString(), dueAt.ToUnixTimeMilliseconds());

    public Task ClearCountdownAsync(Guid lobbyId) =>
        Db.SortedSetRemoveAsync(CountdownIndexKey, lobbyId.ToString());

    public async Task<IReadOnlyList<Guid>> GetDueCountdownLobbyIdsAsync(DateTimeOffset now, int take)
    {
        var values = await Db.SortedSetRangeByScoreAsync(
            CountdownIndexKey,
            stop: now.ToUnixTimeMilliseconds(),
            take: take);

        return values
            .Select(value => Guid.TryParse(value.ToString(), out var lobbyId) ? lobbyId : Guid.Empty)
            .Where(id => id != Guid.Empty)
            .ToArray();
    }

    private static HashEntry[] SerializeLobby(PrivateLobby lobby)
    {
        return
        [
            new HashEntry("lobby_id", lobby.LobbyId.ToString()),
            new HashEntry("host_user_id", lobby.HostUserId.ToString()),
            new HashEntry("guest_user_id", lobby.GuestUserId?.ToString() ?? string.Empty),
            new HashEntry("preset", lobby.Preset.ToString()),
            new HashEntry("host_ready", SerializeBoolean(lobby.HostReady)),
            new HashEntry("guest_ready", SerializeBoolean(lobby.GuestReady)),
            new HashEntry("countdown_active", SerializeBoolean(lobby.CountdownActive)),
            new HashEntry("countdown_end_at", lobby.CountdownEndAt?.ToString("O") ?? string.Empty),
            new HashEntry("created_at", lobby.CreatedAt.ToString("O")),
            new HashEntry("updated_at", lobby.UpdatedAt.ToString("O"))
        ];
    }

    private static PrivateLobby DeserializeLobby(IReadOnlyDictionary<string, string> dict)
    {
        return new PrivateLobby
        {
            LobbyId = Guid.Parse(dict["lobby_id"]),
            HostUserId = Guid.Parse(dict["host_user_id"]),
            GuestUserId = Guid.TryParse(dict["guest_user_id"], out var guestUserId) ? guestUserId : null,
            Preset = Enum.Parse<TimeControlPreset>(dict["preset"]),
            HostReady = ParseBoolean(dict["host_ready"]),
            GuestReady = ParseBoolean(dict["guest_ready"]),
            CountdownActive = ParseBoolean(dict["countdown_active"]),
            CountdownEndAt = DateTimeOffset.TryParse(dict["countdown_end_at"], out var countdownEndAt) ? countdownEndAt : null,
            CreatedAt = DateTimeOffset.Parse(dict["created_at"]),
            UpdatedAt = DateTimeOffset.Parse(dict["updated_at"])
        };
    }

    private static string SerializeBoolean(bool value) => value ? "true" : "false";

    private static bool ParseBoolean(string value)
    {
        return value switch
        {
            "1" => true,
            "0" => false,
            _ => bool.Parse(value)
        };
    }
}
