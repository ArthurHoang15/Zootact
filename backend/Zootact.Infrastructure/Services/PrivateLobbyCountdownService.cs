using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Zootact.Core.Interfaces;

namespace Zootact.Infrastructure.Services;

public sealed class PrivateLobbyCountdownService(
    IServiceProvider serviceProvider,
    ILogger<PrivateLobbyCountdownService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = serviceProvider.CreateScope();
                var lobbyRepository = scope.ServiceProvider.GetRequiredService<IPrivateLobbyRepository>();
                var privateLobbyService = scope.ServiceProvider.GetRequiredService<IPrivateLobbyService>();
                var dueLobbyIds = await lobbyRepository.GetDueCountdownLobbyIdsAsync(DateTimeOffset.UtcNow, take: 20);

                foreach (var lobbyId in dueLobbyIds)
                {
                    if (stoppingToken.IsCancellationRequested)
                    {
                        break;
                    }

                    await privateLobbyService.TryStartMatchAsync(lobbyId, stoppingToken);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error while processing private lobby countdowns");
            }

            await Task.Delay(TimeSpan.FromMilliseconds(500), stoppingToken);
        }
    }
}
