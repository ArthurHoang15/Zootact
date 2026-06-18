using Microsoft.AspNetCore.SignalR;
using Zootact.API.Hubs;
using Zootact.Core.Domain;
using Zootact.Core.DTOs;
using Zootact.Core.Interfaces;
using Zootact.Infrastructure.Data;

namespace Zootact.API.Services;

public sealed class SignalRPrivateLobbyNotificationService(
    IHubContext<GameHub> hubContext,
    ZootactDbContext dbContext) : IPrivateLobbyNotificationService
{
    public async Task SendLobbyUpdatedAsync(PrivateLobby lobby)
    {
        var dto = await BuildLobbyDtoAsync(lobby);
        await hubContext.Clients.Group(GameHubGroups.Lobby(lobby.LobbyId)).SendAsync("OnLobbyUpdated", dto);
    }

    public async Task SendLobbyCountdownStartedAsync(PrivateLobby lobby)
    {
        if (!lobby.CountdownEndAt.HasValue)
        {
            return;
        }

        await hubContext.Clients.Group(GameHubGroups.Lobby(lobby.LobbyId)).SendAsync("OnLobbyCountdownStarted", new LobbyCountdownStartedDto
        {
            LobbyId = lobby.LobbyId.ToString(),
            CountdownEndAt = lobby.CountdownEndAt.Value,
            SecondsRemaining = Math.Max(0, (int)Math.Ceiling((lobby.CountdownEndAt.Value - DateTimeOffset.UtcNow).TotalSeconds))
        });
    }

    public async Task SendLobbyCountdownCanceledAsync(PrivateLobby lobby)
    {
        var dto = await BuildLobbyDtoAsync(lobby);
        await hubContext.Clients.Group(GameHubGroups.Lobby(lobby.LobbyId)).SendAsync("OnLobbyCountdownCanceled", dto);
    }

    public Task SendLobbyClosedAsync(Guid lobbyId, string reason) =>
        hubContext.Clients.Group(GameHubGroups.Lobby(lobbyId)).SendAsync("OnLobbyClosed", new LobbyClosedDto
        {
            LobbyId = lobbyId.ToString(),
            Reason = reason
        });

    private async Task<PrivateLobbyDto> BuildLobbyDtoAsync(PrivateLobby lobby)
    {
        var hostUser = await dbContext.Users.FindAsync(lobby.HostUserId)
            ?? throw new InvalidOperationException($"Host user {lobby.HostUserId} not found.");

        var guestUser = lobby.GuestUserId.HasValue
            ? await dbContext.Users.FindAsync(lobby.GuestUserId.Value)
            : null;

        return new PrivateLobbyDto
        {
            LobbyId = lobby.LobbyId.ToString(),
            Mode = PrivateLobby.FriendlyUntimedMode,
            Host = new LobbyPlayerDto
            {
                Id = hostUser.Id.ToString(),
                Username = hostUser.Username,
                AvatarUrl = hostUser.AvatarUrl,
                ForestPoints = hostUser.ForestPoints,
                IsHost = true,
                IsReady = lobby.HostReady
            },
            Guest = guestUser is null ? null : new LobbyPlayerDto
            {
                Id = guestUser.Id.ToString(),
                Username = guestUser.Username,
                AvatarUrl = guestUser.AvatarUrl,
                ForestPoints = guestUser.ForestPoints,
                IsHost = false,
                IsReady = lobby.GuestReady
            },
            CurrentUserRole = "Unknown",
            CountdownActive = lobby.CountdownActive,
            CountdownEndAt = lobby.CountdownEndAt,
            CountdownSecondsRemaining = lobby.CountdownEndAt.HasValue
                ? Math.Max(0, (int)Math.Ceiling((lobby.CountdownEndAt.Value - DateTimeOffset.UtcNow).TotalSeconds))
                : 0,
            CanStart = lobby.CanStart
        };
    }
}
