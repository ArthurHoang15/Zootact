using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using Zootact.Core.Domain;
using Zootact.Core.DTOs;
using Zootact.Core.Interfaces;

namespace Zootact.API.Controllers;

/// <summary>
/// Matchmaking controller.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class MatchmakingController(
    IMatchmakingService matchmakingService,
    IGameStateRepository gameStateRepository,
    IMatchNotificationService matchNotificationService,
    ILogger<MatchmakingController> logger) : ControllerBase
{
    /// <summary>
    /// Joins the matchmaking queue.
    /// </summary>
    [HttpPost("queue")]
    [ProducesResponseType(typeof(MessageResponse), StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> JoinQueue([FromBody] JoinQueueRequest request)
    {
        var userId = GetUserId();
        if (userId is null)
            return Unauthorized();
        
        try
        {
            if (!Enum.TryParse<TimeControlPreset>(request.TimeControl, out var preset))
            {
                return BadRequest(new ErrorResponse 
                { 
                    Error = "InvalidTimeControl", 
                    Message = "Invalid time control preset." 
                });
            }
            
            var matchId = await matchmakingService.JoinQueueAsync(userId.Value, preset);
            
            if (matchId.HasValue)
            {
                // Match found immediately
                var gameState = await gameStateRepository.GetGameStateAsync(matchId.Value);
                if (gameState is not null)
                {
                    await matchNotificationService.SendMatchStartedAsync(gameState);
                }
                
                return Ok(new { 
                    match_found = true,
                    match_id = matchId.Value.ToString()
                });
            }

            var activeMatchId = await gameStateRepository.GetPlayerActiveMatchAsync(userId.Value);
            if (activeMatchId.HasValue)
            {
                var activeGameState = await gameStateRepository.GetGameStateAsync(activeMatchId.Value);
                if (activeGameState is not null)
                {
                    await matchNotificationService.SendMatchStartedAsync(activeGameState);
                }

                return Ok(new
                {
                    match_found = true,
                    match_id = activeMatchId.Value.ToString()
                });
            }
            
            // Added to queue
            var position = await matchmakingService.GetQueuePositionAsync(userId.Value, preset);
            
            return Accepted(new { 
                match_found = false,
                queue_position = position,
                estimated_wait_seconds = position * 10 // Rough estimate
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error joining queue for user {UserId}", userId);
            return BadRequest(new ErrorResponse 
            { 
                Error = "QueueError", 
                Message = ex.Message 
            });
        }
    }
    
    /// <summary>
    /// Leaves the matchmaking queue.
    /// </summary>
    [HttpDelete("queue")]
    [ProducesResponseType(typeof(MessageResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> LeaveQueue()
    {
        var userId = GetUserId();
        if (userId is null)
            return Unauthorized();
        
        await matchmakingService.LeaveQueueAsync(userId.Value);
        
        return Ok(new MessageResponse 
        { 
            Message = "Left matchmaking queue.",
            Success = true
        });
    }
    
    private Guid? GetUserId()
    {
        var userIdClaim = User.FindFirst("sub")?.Value 
            ?? User.FindFirst("id")?.Value
            ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        
        return Guid.TryParse(userIdClaim, out var userId) ? userId : null;
    }
}

/// <summary>
/// Request to join matchmaking queue.
/// </summary>
public record JoinQueueRequest
{
    [System.Text.Json.Serialization.JsonPropertyName("time_control")]
    public required string TimeControl { get; init; }
}
