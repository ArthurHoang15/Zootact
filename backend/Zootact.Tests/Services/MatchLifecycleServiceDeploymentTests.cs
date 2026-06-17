using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Zootact.Core.Domain;
using Zootact.Infrastructure.Data;
using Zootact.Infrastructure.Data.Entities;
using Zootact.Infrastructure.Services;

namespace Zootact.Tests.Services;

public sealed class MatchLifecycleServiceDeploymentTests
{
    [Fact]
    public async Task GetMatchAnalysis_ReturnsDisabledWhenAiFeatureIsOff()
    {
        await using var dbContext = CreateDbContext();
        var user = new UserEntity
        {
            Id = Guid.NewGuid(),
            FirebaseUid = "firebase-blue",
            Username = "blue",
            Email = "blue@example.com",
        };
        var opponent = new UserEntity
        {
            Id = Guid.NewGuid(),
            FirebaseUid = "firebase-red",
            Username = "red",
            Email = "red@example.com",
        };

        var match = new MatchEntity
        {
            Id = Guid.NewGuid(),
            BluePlayerId = user.Id,
            RedPlayerId = opponent.Id,
            BluePlayer = user,
            RedPlayer = opponent,
            TimeControl = "Blitz",
            InitialTimeMs = 180000,
            IncrementMs = 0,
            Status = "Completed",
            BlueEloBefore = 1200,
            RedEloBefore = 1200,
        };

        dbContext.Users.AddRange(user, opponent);
        dbContext.Matches.Add(match);
        await dbContext.SaveChangesAsync();

        var service = new MatchLifecycleService(
            dbContext,
            new InMemoryGameStateRepository(),
            new AiServiceClient(new HttpClient(), Options.Create(new AiServiceOptions { Enabled = false }), NullLogger<AiServiceClient>.Instance),
            new DummyServiceScopeFactory(),
            Options.Create(new AiServiceOptions { Enabled = false }),
            NullLogger<MatchLifecycleService>.Instance);

        var analysis = await service.GetMatchAnalysisAsync(match.Id, user.Id);

        Assert.NotNull(analysis);
        Assert.Equal("Disabled", analysis.Status);
        Assert.Empty(analysis.Moves);
        Assert.Empty(analysis.AntiCheat);
    }

    private static ZootactDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<ZootactDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new ZootactDbContext(options);
    }

    private sealed class InMemoryGameStateRepository : Zootact.Core.Interfaces.IGameStateRepository
    {
        public Task ClearPlayerActiveMatchAsync(Guid userId) => Task.CompletedTask;
        public Task ClearPlayerConnectionAsync(Guid userId) => Task.CompletedTask;
        public Task ClearPlayerDisconnectedAsync(Guid matchId, Guid userId) => Task.CompletedTask;
        public Task DeleteGameStateAsync(Guid matchId) => Task.CompletedTask;
        public Task<GameState?> GetGameStateAsync(Guid matchId) => Task.FromResult<GameState?>(null);
        public Task<Guid?> GetPlayerActiveMatchAsync(Guid userId) => Task.FromResult<Guid?>(null);
        public Task<string?> GetPlayerConnectionAsync(Guid userId) => Task.FromResult<string?>(null);
        public Task<Zootact.Core.Interfaces.PlayerDisconnectInfo?> GetPlayerDisconnectInfoAsync(Guid matchId, Guid userId) => Task.FromResult<Zootact.Core.Interfaces.PlayerDisconnectInfo?>(null);
        public Task<bool> IsPlayerDisconnectedAsync(Guid matchId, Guid userId) => Task.FromResult(false);
        public Task SaveGameStateAsync(GameState gameState) => Task.CompletedTask;
        public Task SetPlayerActiveMatchAsync(Guid userId, Guid matchId) => Task.CompletedTask;
        public Task SetPlayerConnectionAsync(Guid userId, string connectionId) => Task.CompletedTask;
        public Task SetPlayerDisconnectedAsync(Guid matchId, Guid userId, TimeSpan timeout) => Task.CompletedTask;
    }

    private sealed class DummyServiceScopeFactory : IServiceScopeFactory
    {
        public IServiceScope CreateScope() => throw new NotSupportedException("Background scopes are not used in this test.");
    }
}
