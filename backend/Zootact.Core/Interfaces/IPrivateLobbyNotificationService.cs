using Zootact.Core.Domain;

namespace Zootact.Core.Interfaces;

public interface IPrivateLobbyNotificationService
{
    Task SendLobbyUpdatedAsync(PrivateLobby lobby);
    Task SendLobbyCountdownStartedAsync(PrivateLobby lobby);
    Task SendLobbyCountdownCanceledAsync(PrivateLobby lobby);
    Task SendLobbyClosedAsync(Guid lobbyId, string reason);
}
