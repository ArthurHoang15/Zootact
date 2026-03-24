using Microsoft.AspNetCore.SignalR;
using Zootact.API.Hubs;
using Zootact.Core.DTOs;
using Zootact.Core.Interfaces;

namespace Zootact.API.Services;

public sealed class SignalRMatchNotificationService(
    IHubContext<GameHub> hubContext,
    IGameStateRepository gameStateRepository) : IMatchNotificationService
{
    public async Task SendGameEndedAsync(FinalizedMatchDto finalizedMatch)
    {
        var blueConnection = await gameStateRepository.GetPlayerConnectionAsync(finalizedMatch.Blue.UserId);
        var redConnection = await gameStateRepository.GetPlayerConnectionAsync(finalizedMatch.Red.UserId);

        if (blueConnection is not null)
        {
            await hubContext.Clients.Client(blueConnection).SendAsync("OnGameEnded", new GameEndedDto
            {
                Result = finalizedMatch.Result,
                Reason = finalizedMatch.Reason,
                YourNewElo = finalizedMatch.Blue.NewElo,
                EloChange = finalizedMatch.Blue.EloChange
            });
        }

        if (redConnection is not null)
        {
            await hubContext.Clients.Client(redConnection).SendAsync("OnGameEnded", new GameEndedDto
            {
                Result = finalizedMatch.Result,
                Reason = finalizedMatch.Reason,
                YourNewElo = finalizedMatch.Red.NewElo,
                EloChange = finalizedMatch.Red.EloChange
            });
        }
    }
}
