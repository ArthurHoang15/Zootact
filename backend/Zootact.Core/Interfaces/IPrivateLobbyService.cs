using Zootact.Core.Domain;

namespace Zootact.Core.Interfaces;

public interface IPrivateLobbyService
{
    Task<PrivateLobby> CreateLobbyAsync(Guid userId, TimeControlPreset preset);
    Task<PrivateLobby?> GetActiveLobbyAsync(Guid userId);
    Task<PrivateLobby?> GetLobbyAsync(Guid lobbyId);
    Task<PrivateLobby> JoinLobbyAsync(Guid lobbyId, Guid userId);
    Task<PrivateLobby?> LeaveLobbyAsync(Guid lobbyId, Guid userId);
    Task<PrivateLobby> SetGuestReadyAsync(Guid lobbyId, Guid userId, bool ready);
    Task<PrivateLobby> StartCountdownAsync(Guid lobbyId, Guid userId);
    Task<PrivateLobby> CancelCountdownAsync(Guid lobbyId, Guid userId);
    Task<Guid?> TryStartMatchAsync(Guid lobbyId, CancellationToken cancellationToken = default);
}
