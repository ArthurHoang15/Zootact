using Microsoft.Extensions.Logging;
using Zootact.Core.Domain;
using Zootact.Core.Interfaces;

namespace Zootact.Infrastructure.Services;

public sealed class PrivateLobbyService(
    IPrivateLobbyRepository privateLobbyRepository,
    IGameStateRepository gameStateRepository,
    IMatchmakingService matchmakingService,
    IMatchNotificationService matchNotificationService,
    IPrivateLobbyNotificationService privateLobbyNotificationService,
    ILogger<PrivateLobbyService> logger) : IPrivateLobbyService
{
    private static readonly TimeSpan CountdownDuration = TimeSpan.FromSeconds(5);

    public async Task<PrivateLobby> CreateLobbyAsync(Guid userId, TimeControlPreset preset)
    {
        await EnsureUserCanUsePrivateLobbyAsync(userId);

        var existingLobbyId = await privateLobbyRepository.GetPlayerActiveLobbyAsync(userId);
        if (existingLobbyId.HasValue)
        {
            throw new InvalidOperationException("You are already in a private lobby.");
        }

        await matchmakingService.LeaveQueueAsync(userId);

        var lobby = PrivateLobby.Create(userId, preset);
        await privateLobbyRepository.SaveLobbyAsync(lobby);
        await privateLobbyRepository.SetPlayerActiveLobbyAsync(userId, lobby.LobbyId);

        logger.LogInformation("Private lobby {LobbyId} created by {UserId}", lobby.LobbyId, userId);
        return lobby;
    }

    public Task<PrivateLobby?> GetLobbyAsync(Guid lobbyId) => privateLobbyRepository.GetLobbyAsync(lobbyId);

    public async Task<PrivateLobby> JoinLobbyAsync(Guid lobbyId, Guid userId)
    {
        var activeMatchId = await gameStateRepository.GetPlayerActiveMatchAsync(userId);
        if (activeMatchId.HasValue)
        {
            throw new InvalidOperationException("You are already in an active match.");
        }

        var lobby = await privateLobbyRepository.GetLobbyAsync(lobbyId)
            ?? throw new InvalidOperationException("Lobby not found.");

        var currentLobbyId = await privateLobbyRepository.GetPlayerActiveLobbyAsync(userId);
        if (currentLobbyId.HasValue && currentLobbyId.Value != lobbyId)
        {
            throw new InvalidOperationException("You are already in another private lobby.");
        }

        if (userId == lobby.HostUserId)
        {
            await privateLobbyRepository.SetPlayerActiveLobbyAsync(userId, lobbyId);
            return lobby;
        }

        if (lobby.GuestUserId.HasValue && lobby.GuestUserId.Value != userId)
        {
            throw new InvalidOperationException("Lobby is already full.");
        }

        await matchmakingService.LeaveQueueAsync(userId);

        lobby.GuestUserId = userId;
        lobby.GuestReady = true;
        lobby.Touch();

        await privateLobbyRepository.SaveLobbyAsync(lobby);
        await privateLobbyRepository.SetPlayerActiveLobbyAsync(userId, lobbyId);
        await privateLobbyNotificationService.SendLobbyUpdatedAsync(lobby);

        logger.LogInformation("User {UserId} joined private lobby {LobbyId}", userId, lobbyId);
        return lobby;
    }

    public async Task<PrivateLobby?> LeaveLobbyAsync(Guid lobbyId, Guid userId)
    {
        var lobby = await privateLobbyRepository.GetLobbyAsync(lobbyId)
            ?? throw new InvalidOperationException("Lobby not found.");

        if (!lobby.IsParticipant(userId))
        {
            throw new InvalidOperationException("You are not in this lobby.");
        }

        if (userId == lobby.HostUserId)
        {
            await privateLobbyRepository.DeleteLobbyAsync(lobbyId);
            await privateLobbyRepository.ClearPlayerActiveLobbyAsync(lobby.HostUserId);
            if (lobby.GuestUserId.HasValue)
            {
                await privateLobbyRepository.ClearPlayerActiveLobbyAsync(lobby.GuestUserId.Value);
            }

            await privateLobbyNotificationService.SendLobbyClosedAsync(lobbyId, "host_left");
            logger.LogInformation("Host {UserId} closed private lobby {LobbyId}", userId, lobbyId);
            return null;
        }

        lobby.GuestUserId = null;
        lobby.GuestReady = false;
        var countdownWasActive = lobby.CountdownActive;
        lobby.CountdownActive = false;
        lobby.CountdownEndAt = null;
        lobby.Touch();

        await privateLobbyRepository.ClearPlayerActiveLobbyAsync(userId);
        await privateLobbyRepository.ClearCountdownAsync(lobbyId);
        await privateLobbyRepository.SaveLobbyAsync(lobby);

        await privateLobbyNotificationService.SendLobbyUpdatedAsync(lobby);
        if (countdownWasActive)
        {
            await privateLobbyNotificationService.SendLobbyCountdownCanceledAsync(lobby);
        }

        logger.LogInformation("Guest {UserId} left private lobby {LobbyId}", userId, lobbyId);
        return lobby;
    }

    public async Task<PrivateLobby> SetGuestReadyAsync(Guid lobbyId, Guid userId, bool ready)
    {
        var lobby = await privateLobbyRepository.GetLobbyAsync(lobbyId)
            ?? throw new InvalidOperationException("Lobby not found.");

        if (lobby.GuestUserId != userId)
        {
            throw new InvalidOperationException("Only the guest can change ready state.");
        }

        if (lobby.CountdownActive)
        {
            throw new InvalidOperationException("Cannot change ready state during countdown.");
        }

        lobby.GuestReady = ready;
        lobby.Touch();

        await privateLobbyRepository.SaveLobbyAsync(lobby);
        await privateLobbyNotificationService.SendLobbyUpdatedAsync(lobby);

        return lobby;
    }

    public async Task<PrivateLobby> StartCountdownAsync(Guid lobbyId, Guid userId)
    {
        var lobby = await privateLobbyRepository.GetLobbyAsync(lobbyId)
            ?? throw new InvalidOperationException("Lobby not found.");

        if (lobby.HostUserId != userId)
        {
            throw new InvalidOperationException("Only the host can start the match.");
        }

        if (!lobby.GuestUserId.HasValue)
        {
            throw new InvalidOperationException("Waiting for the second player to join.");
        }

        if (!lobby.GuestReady)
        {
            throw new InvalidOperationException("The second player must be ready first.");
        }

        if (lobby.CountdownActive)
        {
            throw new InvalidOperationException("Countdown is already active.");
        }

        lobby.CountdownActive = true;
        lobby.CountdownEndAt = DateTimeOffset.UtcNow.Add(CountdownDuration);
        lobby.Touch();

        await privateLobbyRepository.SaveLobbyAsync(lobby);
        await privateLobbyRepository.ScheduleCountdownAsync(lobbyId, lobby.CountdownEndAt.Value);
        await privateLobbyNotificationService.SendLobbyUpdatedAsync(lobby);
        await privateLobbyNotificationService.SendLobbyCountdownStartedAsync(lobby);

        return lobby;
    }

    public async Task<PrivateLobby> CancelCountdownAsync(Guid lobbyId, Guid userId)
    {
        var lobby = await privateLobbyRepository.GetLobbyAsync(lobbyId)
            ?? throw new InvalidOperationException("Lobby not found.");

        if (!lobby.IsParticipant(userId))
        {
            throw new InvalidOperationException("You are not in this lobby.");
        }

        if (!lobby.CountdownActive)
        {
            throw new InvalidOperationException("Countdown is not active.");
        }

        lobby.CountdownActive = false;
        lobby.CountdownEndAt = null;
        lobby.Touch();

        await privateLobbyRepository.ClearCountdownAsync(lobbyId);
        await privateLobbyRepository.SaveLobbyAsync(lobby);
        await privateLobbyNotificationService.SendLobbyUpdatedAsync(lobby);
        await privateLobbyNotificationService.SendLobbyCountdownCanceledAsync(lobby);

        return lobby;
    }

    public async Task<Guid?> TryStartMatchAsync(Guid lobbyId, CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return null;
        }

        var lobby = await privateLobbyRepository.GetLobbyAsync(lobbyId);
        if (lobby is null)
        {
            await privateLobbyRepository.ClearCountdownAsync(lobbyId);
            return null;
        }

        if (!lobby.CountdownActive || !lobby.CountdownEndAt.HasValue)
        {
            await privateLobbyRepository.ClearCountdownAsync(lobbyId);
            return null;
        }

        if (lobby.CountdownEndAt.Value > DateTimeOffset.UtcNow)
        {
            return null;
        }

        if (!lobby.GuestUserId.HasValue || !lobby.GuestReady)
        {
            lobby.CountdownActive = false;
            lobby.CountdownEndAt = null;
            lobby.Touch();

            await privateLobbyRepository.ClearCountdownAsync(lobbyId);
            await privateLobbyRepository.SaveLobbyAsync(lobby);
            await privateLobbyNotificationService.SendLobbyUpdatedAsync(lobby);
            await privateLobbyNotificationService.SendLobbyCountdownCanceledAsync(lobby);
            return null;
        }

        await EnsureUserCanUsePrivateLobbyAsync(lobby.HostUserId, allowCurrentLobby: true);
        await EnsureUserCanUsePrivateLobbyAsync(lobby.GuestUserId.Value, allowCurrentLobby: true);
        await matchmakingService.LeaveQueueAsync(lobby.HostUserId);
        await matchmakingService.LeaveQueueAsync(lobby.GuestUserId.Value);

        var isHostBlue = Random.Shared.Next(2) == 0;
        var blueId = isHostBlue ? lobby.HostUserId : lobby.GuestUserId.Value;
        var redId = isHostBlue ? lobby.GuestUserId.Value : lobby.HostUserId;

        var matchId = await matchmakingService.CreateMatchAsync(blueId, redId, lobby.Preset);
        var gameState = await gameStateRepository.GetGameStateAsync(matchId);

        await privateLobbyRepository.DeleteLobbyAsync(lobbyId);
        await privateLobbyRepository.ClearPlayerActiveLobbyAsync(lobby.HostUserId);
        await privateLobbyRepository.ClearPlayerActiveLobbyAsync(lobby.GuestUserId.Value);

        if (gameState is not null)
        {
            await matchNotificationService.SendMatchStartedAsync(gameState);
        }

        logger.LogInformation("Private lobby {LobbyId} started match {MatchId}", lobbyId, matchId);
        return matchId;
    }

    private async Task EnsureUserCanUsePrivateLobbyAsync(Guid userId, bool allowCurrentLobby = false)
    {
        var activeMatchId = await gameStateRepository.GetPlayerActiveMatchAsync(userId);
        if (activeMatchId.HasValue)
        {
            throw new InvalidOperationException("You are already in an active match.");
        }

        if (!allowCurrentLobby)
        {
            var activeLobbyId = await privateLobbyRepository.GetPlayerActiveLobbyAsync(userId);
            if (activeLobbyId.HasValue)
            {
                throw new InvalidOperationException("You are already in a private lobby.");
            }
        }
    }
}
