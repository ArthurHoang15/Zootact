using Zootact.Core.Domain;

namespace Zootact.Core.Interfaces;

public interface IPrivateLobbyRepository
{
    Task<PrivateLobby?> GetLobbyAsync(Guid lobbyId);
    Task SaveLobbyAsync(PrivateLobby lobby);
    Task DeleteLobbyAsync(Guid lobbyId);
    Task<Guid?> GetPlayerActiveLobbyAsync(Guid userId);
    Task SetPlayerActiveLobbyAsync(Guid userId, Guid lobbyId);
    Task ClearPlayerActiveLobbyAsync(Guid userId);
    Task ScheduleCountdownAsync(Guid lobbyId, DateTimeOffset dueAt);
    Task ClearCountdownAsync(Guid lobbyId);
    Task<IReadOnlyList<Guid>> GetDueCountdownLobbyIdsAsync(DateTimeOffset now, int take);
    Task<bool> TryClaimDueCountdownAsync(Guid lobbyId, DateTimeOffset now);
}
