using Zootact.Core.DTOs;
using Zootact.Core.Domain;

namespace Zootact.Core.Interfaces;

public interface IMatchNotificationService
{
    Task SendMatchStartedAsync(GameState gameState);
    Task SendGameEndedAsync(FinalizedMatchDto finalizedMatch);
}
