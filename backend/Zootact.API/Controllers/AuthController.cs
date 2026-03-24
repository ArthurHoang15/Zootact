using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Zootact.Core.DTOs;
using Zootact.Infrastructure.Data;
using Zootact.Infrastructure.Data.Entities;

namespace Zootact.API.Controllers;

/// <summary>
/// Authentication controller for Firebase integration.
/// Simplified - Firebase handles all auth, we just sync users to PostgreSQL.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class AuthController(
    ZootactDbContext dbContext,
    ILogger<AuthController> logger) : ControllerBase
{
    private static UserDto MapUserDto(UserEntity user) => new()
    {
        Id = user.Id.ToString(),
        Username = user.Username,
        Email = user.Email,
        AvatarUrl = user.AvatarUrl,
        ForestPoints = user.ForestPoints,
        AuthProvider = "Firebase"
    };

    private static UserStatsDto MapStatsDto(UserStatsEntity? stats)
    {
        var totalGames = stats?.TotalGames ?? 0;

        return new UserStatsDto
        {
            TotalGames = totalGames,
            Wins = stats?.Wins ?? 0,
            Losses = stats?.Losses ?? 0,
            Draws = stats?.Draws ?? 0,
            WinRate = totalGames == 0 ? 0 : Math.Round(((decimal)(stats?.Wins ?? 0) / totalGames) * 100, 1),
            CurrentStreak = stats?.WinStreakCurrent ?? 0,
            BestStreak = stats?.WinStreakBest ?? 0,
            AvgMoveTimeMs = stats?.AvgMoveTimeMs,
            TotalPlayTimeMs = stats?.TotalPlayTimeMs ?? 0
        };
    }

    /// <summary>
    /// Syncs Firebase user to PostgreSQL database (auto-creates if new user).
    /// This endpoint is called automatically by the frontend after Firebase login.
    /// </summary>
    [HttpPost("sync")]
    public IActionResult SyncUser()
    {
        try
        {
            // User info is already attached by FirebaseAuthMiddleware
            var user = HttpContext.Items["User"] as UserEntity;
            
            if (user == null)
            {
                logger.LogWarning("SyncUser called but no user in HttpContext");
                return Unauthorized(new ErrorResponse 
                { 
                    Error = "NotAuthenticated", 
                    Message = "User not authenticated" 
                });
            }

            // Return user info
            var userDto = MapUserDto(user);

            logger.LogInformation("User synced: {UserId} ({Username})", user.Id, user.Username);

            return Ok(new
            {
                user = userDto,
                message = "User synced successfully"
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error syncing user");
            return StatusCode(500, new ErrorResponse
            {
                Error = "SyncError",
                Message = "Failed to sync user data"
            });
        }
    }

    /// <summary>
    /// Gets current user info.
    /// </summary>
    [HttpGet("me")]
    public IActionResult GetCurrentUser()
    {
        var user = HttpContext.Items["User"] as UserEntity;
        
        if (user == null)
        {
            return Unauthorized(new ErrorResponse 
            { 
                Error = "NotAuthenticated", 
                Message = "User not authenticated" 
            });
        }

        var userDto = MapUserDto(user);

        return Ok(userDto);
    }

    /// <summary>
    /// Gets current user profile with stats and recent matches.
    /// </summary>
    [HttpGet("profile")]
    public async Task<IActionResult> GetMyProfile()
    {
        var user = HttpContext.Items["User"] as UserEntity;

        if (user == null)
        {
            return Unauthorized(new ErrorResponse
            {
                Error = "NotAuthenticated",
                Message = "User not authenticated"
            });
        }

        var dbUserExists = await dbContext.Users.AnyAsync(u => u.Id == user.Id);

        if (!dbUserExists)
        {
            return NotFound(new ErrorResponse
            {
                Error = "UserNotFound",
                Message = "User not found"
            });
        }

        return Ok(await BuildProfileResponseAsync(user.Id));
    }

    /// <summary>
    /// Updates user profile (username only for now).
    /// </summary>
    [HttpPatch("profile")]
    public async Task<IActionResult> UpdateProfile([FromBody] UpdateProfileRequest request)
    {
        var user = HttpContext.Items["User"] as UserEntity;
        
        if (user == null)
        {
            return Unauthorized();
        }

        try
        {
            // Check if username is taken
            if (!string.IsNullOrWhiteSpace(request.Username))
            {
                var normalizedUsername = request.Username.Trim();
                if (normalizedUsername == user.Username)
                {
                    return Ok(await BuildProfileResponseAsync(user.Id));
                }

                var exists = await dbContext.Users.AnyAsync(u => 
                    u.Username == normalizedUsername && u.Id != user.Id);
                
                if (exists)
                {
                    return BadRequest(new ErrorResponse 
                    { 
                        Error = "UsernameTaken", 
                        Message = "Username is already taken" 
                    });
                }

                user.Username = normalizedUsername;
            }

            await dbContext.SaveChangesAsync();

            logger.LogInformation("User profile updated: {UserId}", user.Id);

            return Ok(await BuildProfileResponseAsync(user.Id));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error updating profile for user {UserId}", user.Id);
            return StatusCode(500, new ErrorResponse
            {
                Error = "UpdateError",
                Message = "Failed to update profile"
            });
        }
    }

    private async Task<MyProfileDto> BuildProfileResponseAsync(Guid userId)
    {
        var dbUser = await dbContext.Users
            .Include(u => u.Stats)
            .FirstAsync(u => u.Id == userId);

        var recentMatches = await dbContext.Matches
            .Include(m => m.BluePlayer)
            .Include(m => m.RedPlayer)
            .Where(m =>
                m.Status == "Completed" &&
                (m.BluePlayerId == dbUser.Id || m.RedPlayerId == dbUser.Id))
            .OrderByDescending(m => m.EndedAt ?? m.StartedAt)
            .Take(5)
            .ToListAsync();

        return new MyProfileDto
        {
            User = MapUserDto(dbUser),
            Stats = MapStatsDto(dbUser.Stats),
            RecentMatches = recentMatches.Select(match =>
            {
                var isBlue = match.BluePlayerId == dbUser.Id;
                var opponent = isBlue ? match.RedPlayer : match.BluePlayer;
                var eloBefore = isBlue ? match.BlueEloBefore : match.RedEloBefore;
                var eloAfter = isBlue ? (match.BlueEloAfter ?? match.BlueEloBefore) : (match.RedEloAfter ?? match.RedEloBefore);
                var outcome = match.WinnerId == null
                    ? "Draw"
                    : match.WinnerId == dbUser.Id
                        ? "Win"
                        : "Loss";

                return new RecentProfileMatchDto
                {
                    MatchId = match.Id.ToString(),
                    TimeControl = match.TimeControl,
                    Outcome = outcome,
                    ResultReason = match.ResultReason ?? "Unknown",
                    OpponentUsername = opponent.Username,
                    OpponentAvatarUrl = opponent.AvatarUrl,
                    EndedAt = match.EndedAt,
                    EloChange = eloAfter - eloBefore
                };
            }).ToList()
        };
    }
}

/// <summary>
/// Update profile request.
/// </summary>
public record UpdateProfileRequest
{
    [System.Text.Json.Serialization.JsonPropertyName("username")]
    public string? Username { get; init; }
}
