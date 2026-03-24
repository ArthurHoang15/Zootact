using Microsoft.Extensions.Logging.Abstractions;
using Zootact.Core.Domain;
using Zootact.Core.DTOs;
using Zootact.Core.Interfaces;
using Zootact.Infrastructure.Services;

namespace Zootact.Tests.Services;

public sealed class PrivateLobbyServiceTests
{
    [Theory]
    [InlineData(TimeControlPreset.Blitz)]
    [InlineData(TimeControlPreset.Rapid)]
    [InlineData(TimeControlPreset.Classical)]
    public async Task CreateLobby_UsesRequestedPreset(TimeControlPreset preset)
    {
        var fixture = new PrivateLobbyFixture();

        var lobby = await fixture.Service.CreateLobbyAsync(fixture.HostId, preset);

        Assert.Equal(preset, lobby.Preset);
        Assert.Equal(fixture.HostId, lobby.HostUserId);
        Assert.Null(lobby.GuestUserId);
        Assert.False(lobby.GuestReady);
    }

    [Fact]
    public async Task JoinLobby_AssignsGuestAndDefaultsReady()
    {
        var fixture = new PrivateLobbyFixture();
        var lobby = await fixture.Service.CreateLobbyAsync(fixture.HostId, TimeControlPreset.Blitz);

        var updatedLobby = await fixture.Service.JoinLobbyAsync(lobby.LobbyId, fixture.GuestId);

        Assert.Equal(fixture.GuestId, updatedLobby.GuestUserId);
        Assert.True(updatedLobby.GuestReady);
        Assert.Equal(lobby.LobbyId, await fixture.PrivateLobbyRepository.GetPlayerActiveLobbyAsync(fixture.GuestId));
    }

    [Fact]
    public async Task JoinLobby_RejectsThirdPlayer()
    {
        var fixture = new PrivateLobbyFixture();
        var lobby = await fixture.Service.CreateLobbyAsync(fixture.HostId, TimeControlPreset.Blitz);
        await fixture.Service.JoinLobbyAsync(lobby.LobbyId, fixture.GuestId);

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            fixture.Service.JoinLobbyAsync(lobby.LobbyId, fixture.ThirdPlayerId));

        Assert.Equal("Lobby is already full.", error.Message);
    }

    [Fact]
    public async Task HostCanReopenOwnLobbyLink()
    {
        var fixture = new PrivateLobbyFixture();
        var lobby = await fixture.Service.CreateLobbyAsync(fixture.HostId, TimeControlPreset.Rapid);

        var reopenedLobby = await fixture.Service.JoinLobbyAsync(lobby.LobbyId, fixture.HostId);

        Assert.Equal(lobby.LobbyId, reopenedLobby.LobbyId);
        Assert.Equal(fixture.HostId, reopenedLobby.HostUserId);
    }

    [Fact]
    public async Task StartCountdown_RequiresGuestToJoin()
    {
        var fixture = new PrivateLobbyFixture();
        var lobby = await fixture.Service.CreateLobbyAsync(fixture.HostId, TimeControlPreset.Blitz);

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            fixture.Service.StartCountdownAsync(lobby.LobbyId, fixture.HostId));

        Assert.Equal("Waiting for the second player to join.", error.Message);
    }

    [Fact]
    public async Task StartCountdown_RequiresGuestReady()
    {
        var fixture = new PrivateLobbyFixture();
        var lobby = await fixture.Service.CreateLobbyAsync(fixture.HostId, TimeControlPreset.Blitz);
        await fixture.Service.JoinLobbyAsync(lobby.LobbyId, fixture.GuestId);
        await fixture.Service.SetGuestReadyAsync(lobby.LobbyId, fixture.GuestId, false);

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            fixture.Service.StartCountdownAsync(lobby.LobbyId, fixture.HostId));

        Assert.Equal("The second player must be ready first.", error.Message);
    }

    [Fact]
    public async Task StartCountdown_OnlyHostCanStart()
    {
        var fixture = new PrivateLobbyFixture();
        var lobby = await fixture.Service.CreateLobbyAsync(fixture.HostId, TimeControlPreset.Blitz);
        await fixture.Service.JoinLobbyAsync(lobby.LobbyId, fixture.GuestId);

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            fixture.Service.StartCountdownAsync(lobby.LobbyId, fixture.GuestId));

        Assert.Equal("Only the host can start the match.", error.Message);
    }

    [Fact]
    public async Task CancelCountdown_ResetsLobbyState()
    {
        var fixture = new PrivateLobbyFixture();
        var lobby = await fixture.Service.CreateLobbyAsync(fixture.HostId, TimeControlPreset.Blitz);
        await fixture.Service.JoinLobbyAsync(lobby.LobbyId, fixture.GuestId);
        await fixture.Service.StartCountdownAsync(lobby.LobbyId, fixture.HostId);

        var updatedLobby = await fixture.Service.CancelCountdownAsync(lobby.LobbyId, fixture.GuestId);

        Assert.False(updatedLobby.CountdownActive);
        Assert.Null(updatedLobby.CountdownEndAt);
        Assert.True(updatedLobby.GuestReady);
        Assert.Equal(1, fixture.PrivateLobbyNotifications.CountdownCanceledCount);
    }

    [Fact]
    public async Task GuestLeavingDuringCountdown_CancelsCountdownAndKeepsHostInLobby()
    {
        var fixture = new PrivateLobbyFixture();
        var lobby = await fixture.Service.CreateLobbyAsync(fixture.HostId, TimeControlPreset.Blitz);
        await fixture.Service.JoinLobbyAsync(lobby.LobbyId, fixture.GuestId);
        await fixture.Service.StartCountdownAsync(lobby.LobbyId, fixture.HostId);

        var updatedLobby = await fixture.Service.LeaveLobbyAsync(lobby.LobbyId, fixture.GuestId);

        Assert.NotNull(updatedLobby);
        Assert.Null(updatedLobby!.GuestUserId);
        Assert.False(updatedLobby.GuestReady);
        Assert.False(updatedLobby.CountdownActive);
        Assert.Equal(lobby.LobbyId, await fixture.PrivateLobbyRepository.GetPlayerActiveLobbyAsync(fixture.HostId));
        Assert.Null(await fixture.PrivateLobbyRepository.GetPlayerActiveLobbyAsync(fixture.GuestId));
        Assert.Equal(1, fixture.PrivateLobbyNotifications.CountdownCanceledCount);
    }

    [Fact]
    public async Task TryStartMatch_CompletesCountdownCreatesMatchAndClearsLobbyMappings()
    {
        var fixture = new PrivateLobbyFixture();
        var lobby = await fixture.Service.CreateLobbyAsync(fixture.HostId, TimeControlPreset.Classical);
        await fixture.Service.JoinLobbyAsync(lobby.LobbyId, fixture.GuestId);
        var countdownLobby = await fixture.Service.StartCountdownAsync(lobby.LobbyId, fixture.HostId);
        countdownLobby.CountdownEndAt = DateTimeOffset.UtcNow.AddSeconds(-1);
        await fixture.PrivateLobbyRepository.SaveLobbyAsync(countdownLobby);
        await fixture.PrivateLobbyRepository.ScheduleCountdownAsync(countdownLobby.LobbyId, countdownLobby.CountdownEndAt.Value);

        var matchId = await fixture.Service.TryStartMatchAsync(lobby.LobbyId);

        Assert.NotNull(matchId);
        Assert.Null(await fixture.PrivateLobbyRepository.GetLobbyAsync(lobby.LobbyId));
        Assert.Null(await fixture.PrivateLobbyRepository.GetPlayerActiveLobbyAsync(fixture.HostId));
        Assert.Null(await fixture.PrivateLobbyRepository.GetPlayerActiveLobbyAsync(fixture.GuestId));
        Assert.NotNull(await fixture.GameStateRepository.GetGameStateAsync(matchId!.Value));
        Assert.Contains(matchId.Value, fixture.MatchNotifications.StartedMatches);
        Assert.Equal(MatchMode.Friendly, fixture.MatchmakingService.CreatedMatchTypes[matchId.Value]);
    }

    [Fact]
    public async Task CreateLobby_FailsWhenUserAlreadyInActiveMatch()
    {
        var fixture = new PrivateLobbyFixture();
        await fixture.GameStateRepository.SetPlayerActiveMatchAsync(fixture.HostId, Guid.NewGuid());

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            fixture.Service.CreateLobbyAsync(fixture.HostId, TimeControlPreset.Blitz));

        Assert.Equal("You are already in an active match.", error.Message);
    }

    [Fact]
    public async Task CreateLobby_ReleasesExistingLobbyBeforeCreatingNewOne()
    {
        var fixture = new PrivateLobbyFixture();
        var firstLobby = await fixture.Service.CreateLobbyAsync(fixture.HostId, TimeControlPreset.Blitz);

        var secondLobby = await fixture.Service.CreateLobbyAsync(fixture.HostId, TimeControlPreset.Rapid);

        Assert.NotEqual(firstLobby.LobbyId, secondLobby.LobbyId);
        Assert.Null(await fixture.PrivateLobbyRepository.GetLobbyAsync(firstLobby.LobbyId));
        Assert.Equal(secondLobby.LobbyId, await fixture.PrivateLobbyRepository.GetPlayerActiveLobbyAsync(fixture.HostId));
    }

    private sealed class PrivateLobbyFixture
    {
        public Guid HostId { get; } = Guid.NewGuid();
        public Guid GuestId { get; } = Guid.NewGuid();
        public Guid ThirdPlayerId { get; } = Guid.NewGuid();
        public InMemoryPrivateLobbyRepository PrivateLobbyRepository { get; } = new();
        public InMemoryGameStateRepository GameStateRepository { get; } = new();
        public FakeMatchmakingService MatchmakingService { get; }
        public FakeMatchNotificationService MatchNotifications { get; } = new();
        public FakePrivateLobbyNotificationService PrivateLobbyNotifications { get; } = new();
        public PrivateLobbyService Service { get; }

        public PrivateLobbyFixture()
        {
            MatchmakingService = new FakeMatchmakingService(GameStateRepository);
            Service = new PrivateLobbyService(
                PrivateLobbyRepository,
                GameStateRepository,
                MatchmakingService,
                MatchNotifications,
                PrivateLobbyNotifications,
                NullLogger<PrivateLobbyService>.Instance);
        }
    }

    private sealed class InMemoryPrivateLobbyRepository : IPrivateLobbyRepository
    {
        private readonly Dictionary<Guid, PrivateLobby> _lobbies = [];
        private readonly Dictionary<Guid, Guid> _activeLobbies = [];
        private readonly Dictionary<Guid, DateTimeOffset> _countdowns = [];

        public Task<PrivateLobby?> GetLobbyAsync(Guid lobbyId) =>
            Task.FromResult(_lobbies.TryGetValue(lobbyId, out var lobby) ? lobby : null);

        public Task SaveLobbyAsync(PrivateLobby lobby)
        {
            _lobbies[lobby.LobbyId] = lobby;
            return Task.CompletedTask;
        }

        public Task DeleteLobbyAsync(Guid lobbyId)
        {
            _lobbies.Remove(lobbyId);
            _countdowns.Remove(lobbyId);
            return Task.CompletedTask;
        }

        public Task<Guid?> GetPlayerActiveLobbyAsync(Guid userId) =>
            Task.FromResult(_activeLobbies.TryGetValue(userId, out var lobbyId) ? (Guid?)lobbyId : null);

        public Task SetPlayerActiveLobbyAsync(Guid userId, Guid lobbyId)
        {
            _activeLobbies[userId] = lobbyId;
            return Task.CompletedTask;
        }

        public Task ClearPlayerActiveLobbyAsync(Guid userId)
        {
            _activeLobbies.Remove(userId);
            return Task.CompletedTask;
        }

        public Task ScheduleCountdownAsync(Guid lobbyId, DateTimeOffset dueAt)
        {
            _countdowns[lobbyId] = dueAt;
            return Task.CompletedTask;
        }

        public Task ClearCountdownAsync(Guid lobbyId)
        {
            _countdowns.Remove(lobbyId);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<Guid>> GetDueCountdownLobbyIdsAsync(DateTimeOffset now, int take)
        {
            IReadOnlyList<Guid> result = _countdowns
                .Where(pair => pair.Value <= now)
                .Select(pair => pair.Key)
                .Take(take)
                .ToArray();

            return Task.FromResult(result);
        }
    }

    private sealed class InMemoryGameStateRepository : IGameStateRepository
    {
        private readonly Dictionary<Guid, GameState> _gameStates = [];
        private readonly Dictionary<Guid, Guid> _activeMatches = [];

        public Task<GameState?> GetGameStateAsync(Guid matchId) =>
            Task.FromResult(_gameStates.TryGetValue(matchId, out var state) ? state : null);

        public Task SaveGameStateAsync(GameState gameState)
        {
            _gameStates[gameState.MatchId] = gameState;
            return Task.CompletedTask;
        }

        public Task DeleteGameStateAsync(Guid matchId)
        {
            _gameStates.Remove(matchId);
            return Task.CompletedTask;
        }

        public Task<Guid?> GetPlayerActiveMatchAsync(Guid userId) =>
            Task.FromResult(_activeMatches.TryGetValue(userId, out var matchId) ? (Guid?)matchId : null);

        public Task SetPlayerActiveMatchAsync(Guid userId, Guid matchId)
        {
            _activeMatches[userId] = matchId;
            return Task.CompletedTask;
        }

        public Task ClearPlayerActiveMatchAsync(Guid userId)
        {
            _activeMatches.Remove(userId);
            return Task.CompletedTask;
        }

        public Task SetPlayerDisconnectedAsync(Guid matchId, Guid userId, TimeSpan timeout) => Task.CompletedTask;
        public Task ClearPlayerDisconnectedAsync(Guid matchId, Guid userId) => Task.CompletedTask;
        public Task<bool> IsPlayerDisconnectedAsync(Guid matchId, Guid userId) => Task.FromResult(false);
        public Task<PlayerDisconnectInfo?> GetPlayerDisconnectInfoAsync(Guid matchId, Guid userId) => Task.FromResult<PlayerDisconnectInfo?>(null);
        public Task SetPlayerConnectionAsync(Guid userId, string connectionId) => Task.CompletedTask;
        public Task<string?> GetPlayerConnectionAsync(Guid userId) => Task.FromResult<string?>(null);
        public Task ClearPlayerConnectionAsync(Guid userId) => Task.CompletedTask;
    }

    private sealed class FakeMatchmakingService(InMemoryGameStateRepository gameStateRepository) : IMatchmakingService
    {
        public List<Guid> QueueLeaves { get; } = [];
        public Dictionary<Guid, MatchMode> CreatedMatchTypes { get; } = [];

        public Task<Guid?> JoinQueueAsync(Guid userId, TimeControlPreset preset) => Task.FromResult<Guid?>(null);

        public Task LeaveQueueAsync(Guid userId)
        {
            QueueLeaves.Add(userId);
            return Task.CompletedTask;
        }

        public Task<int> GetQueuePositionAsync(Guid userId, TimeControlPreset preset) => Task.FromResult(0);

        public async Task<Guid> CreateMatchAsync(Guid bluePlayerId, Guid redPlayerId, TimeControlPreset preset, MatchMode matchMode = MatchMode.Rated)
        {
            var matchId = Guid.NewGuid();
            var state = GameState.Create(matchId, bluePlayerId, redPlayerId, preset);
            await gameStateRepository.SaveGameStateAsync(state);
            await gameStateRepository.SetPlayerActiveMatchAsync(bluePlayerId, matchId);
            await gameStateRepository.SetPlayerActiveMatchAsync(redPlayerId, matchId);
            CreatedMatchTypes[matchId] = matchMode;
            return matchId;
        }
    }

    private sealed class FakeMatchNotificationService : IMatchNotificationService
    {
        public List<Guid> StartedMatches { get; } = [];

        public Task SendMatchStartedAsync(GameState gameState)
        {
            StartedMatches.Add(gameState.MatchId);
            return Task.CompletedTask;
        }

        public Task SendGameEndedAsync(FinalizedMatchDto finalizedMatch) => Task.CompletedTask;
    }

    private sealed class FakePrivateLobbyNotificationService : IPrivateLobbyNotificationService
    {
        public int CountdownCanceledCount { get; private set; }

        public Task SendLobbyUpdatedAsync(PrivateLobby lobby) => Task.CompletedTask;

        public Task SendLobbyCountdownStartedAsync(PrivateLobby lobby) => Task.CompletedTask;

        public Task SendLobbyCountdownCanceledAsync(PrivateLobby lobby)
        {
            CountdownCanceledCount++;
            return Task.CompletedTask;
        }

        public Task SendLobbyClosedAsync(Guid lobbyId, string reason) => Task.CompletedTask;
    }
}
