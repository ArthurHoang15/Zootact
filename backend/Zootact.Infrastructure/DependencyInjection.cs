using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using StackExchange.Redis;
using Zootact.Core.GameLogic;
using Zootact.Core.Interfaces;
using Zootact.Infrastructure.Data;
using Zootact.Infrastructure.Repositories;
using Zootact.Infrastructure.Services;

namespace Zootact.Infrastructure;

/// <summary>
/// Extension methods for registering infrastructure services.
/// </summary>
public static class DependencyInjection
{
    /// <summary>
    /// Adds infrastructure services to the service collection.
    /// </summary>
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // PostgreSQL with EF Core
        var connectionString = configuration.GetConnectionString("PostgreSQL");
        if (!string.IsNullOrEmpty(connectionString))
        {
            services.AddDbContext<ZootactDbContext>(options =>
                options.UseNpgsql(connectionString));
        }
        
        // Redis
        var redisConnectionString = configuration.GetConnectionString("Redis") ?? "localhost:6379";
        services.AddSingleton<IConnectionMultiplexer>(sp =>
            ConnectionMultiplexer.Connect(redisConnectionString));
        
        // Repositories
        services.AddScoped<IGameStateRepository, RedisGameStateRepository>();
        services.AddScoped<IPrivateLobbyRepository, RedisPrivateLobbyRepository>();
        services.AddScoped<IMatchLifecycleService, MatchLifecycleService>();
        
        // Services (Firebase handles auth now, so no AuthService or EmailSender)
        services.AddScoped<IMatchmakingService, MatchmakingService>();
        services.AddScoped<IPrivateLobbyService, PrivateLobbyService>();
        services.AddHttpClient<AiServiceClient>((sp, client) =>
        {
            var baseUrl = configuration["AiService:BaseUrl"] ?? "http://localhost:8001";
            client.BaseAddress = new Uri(baseUrl);
            client.Timeout = TimeSpan.FromSeconds(15);
        });
        
        // Background Services
        services.AddHostedService<MatchPersistenceService>();
        services.AddHostedService<DisconnectTimerService>();
        services.AddHostedService<GameTimeoutService>();
        services.AddHostedService<PrivateLobbyCountdownService>();
        
        return services;
    }
    
    /// <summary>
    /// Adds game logic services to the service collection.
    /// </summary>
    public static IServiceCollection AddGameLogic(this IServiceCollection services)
    {
        // Game logic services
        services.AddSingleton<ZobristHasher>();
        services.AddSingleton<IMoveValidator, MoveValidator>();
        services.AddSingleton<GameResultChecker>();
        services.AddSingleton<GameEngine>();
        
        return services;
    }
}

