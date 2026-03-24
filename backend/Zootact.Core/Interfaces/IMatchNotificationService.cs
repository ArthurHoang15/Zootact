using Zootact.Core.DTOs;

namespace Zootact.Core.Interfaces;

public interface IMatchNotificationService
{
    Task SendGameEndedAsync(FinalizedMatchDto finalizedMatch);
}
