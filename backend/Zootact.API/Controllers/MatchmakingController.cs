using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using System.Security.Claims;
using Zootact.API.Hubs;
using Zootact.Core.Domain;
using Zootact.Core.DTOs;
using Zootact.Core.Interfaces;
using Zootact.Infrastructure.Data;

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
    IHubContext<GameHub> hubContext,
    ZootactDbContext dbContext,
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
                    // Notify both players via SignalR
                    await NotifyMatchStart(gameState);
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
                    await NotifyMatchStart(activeGameState);
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
    
    private async Task NotifyMatchStart(GameState gameState)
    {
        var blueUser = await dbContext.Users.FindAsync(gameState.BluePlayerId);
        var redUser = await dbContext.Users.FindAsync(gameState.RedPlayerId);

        // Get connection IDs for both players
        var blueConnId = await gameStateRepository.GetPlayerConnectionAsync(gameState.BluePlayerId);
        var redConnId = await gameStateRepository.GetPlayerConnectionAsync(gameState.RedPlayerId);
        
        // Send match start event to both players
        var blueMatchStart = new MatchStartDto
        {
            MatchId = gameState.MatchId.ToString(),
            Opponent = new OpponentDto 
            { 
                Id = gameState.RedPlayerId.ToString(), 
                Username = redUser?.Username ?? "Red Player",
                AvatarUrl = redUser?.AvatarUrl,
                ForestPoints = redUser?.ForestPoints ?? 1200 
            },
            YourColor = "Blue",
            TimeControl = new TimeControlDto
            {
                Preset = gameState.TimeControl.Preset.ToString(),
                InitialTimeMs = gameState.TimeControl.InitialTimeMs,
                IncrementMs = gameState.TimeControl.IncrementMs
            },
            InitialBoard = ConvertBoardToDto(gameState.Board)
        };
        
        var redMatchStart = new MatchStartDto
        {
            MatchId = gameState.MatchId.ToString(),
            Opponent = new OpponentDto 
            { 
                Id = gameState.BluePlayerId.ToString(), 
                Username = blueUser?.Username ?? "Blue Player",
                AvatarUrl = blueUser?.AvatarUrl,
                ForestPoints = blueUser?.ForestPoints ?? 1200 
            },
            YourColor = "Red",
            TimeControl = new TimeControlDto
            {
                Preset = gameState.TimeControl.Preset.ToString(),
                InitialTimeMs = gameState.TimeControl.InitialTimeMs,
                IncrementMs = gameState.TimeControl.IncrementMs
            },
            InitialBoard = ConvertBoardToDto(gameState.Board)
        };
        
        if (blueConnId is not null)
            await hubContext.Clients.Client(blueConnId).SendAsync("OnMatchStart", blueMatchStart);
        
        if (redConnId is not null)
            await hubContext.Clients.Client(redConnId).SendAsync("OnMatchStart", redMatchStart);
        
        logger.LogInformation("Match start notification sent for {MatchId}", gameState.MatchId);
    }
    
    private static BoardDto ConvertBoardToDto(Board board)
    {
        var cells = new PieceDto?[BoardConstants.Rows][];
        
        for (var row = 0; row < BoardConstants.Rows; row++)
        {
            cells[row] = new PieceDto?[BoardConstants.Cols];
            for (var col = 0; col < BoardConstants.Cols; col++)
            {
                var piece = board[row, col];
                if (piece is not null)
                {
                    cells[row][col] = new PieceDto
                    {
                        Type = piece.Type.ToString(),
                        Owner = piece.Owner.ToString(),
                        Rank = piece.Rank
                    };
                }
            }
        }
        
        return new BoardDto { Cells = cells };
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
