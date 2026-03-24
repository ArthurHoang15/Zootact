using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Zootact.Core.Interfaces;
using Zootact.Infrastructure.Data;

namespace Zootact.Infrastructure.Services;

/// <summary>
/// Safety-net service that finalizes any completed Redis matches that were not persisted inline.
/// </summary>
public sealed class MatchPersistenceService(
    IServiceProvider serviceProvider,
    ILogger<MatchPersistenceService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Match Persistence Service started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessCompletedMatchesAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error in Match Persistence Service");
            }

            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
        }
    }

    private async Task ProcessCompletedMatchesAsync(CancellationToken cancellationToken)
    {
        using var scope = serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ZootactDbContext>();
        var gameStateRepo = scope.ServiceProvider.GetRequiredService<IGameStateRepository>();
        var lifecycleService = scope.ServiceProvider.GetRequiredService<IMatchLifecycleService>();

        var matchIds = await dbContext.Matches
            .Where(m => m.Status == "InProgress")
            .Select(m => m.Id)
            .ToListAsync(cancellationToken);

        foreach (var matchId in matchIds)
        {
            var state = await gameStateRepo.GetGameStateAsync(matchId);
            if (state?.Result is not null && state.Result.ToString() != "InProgress")
            {
                await lifecycleService.FinalizeMatchAsync(matchId);
            }
        }
    }
}
