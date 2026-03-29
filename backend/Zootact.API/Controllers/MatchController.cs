using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Zootact.Core.Domain;
using Zootact.Core.DTOs;
using Zootact.Core.Interfaces;
using Zootact.Infrastructure.Data;
using Zootact.Infrastructure.Data.Entities;

namespace Zootact.API.Controllers;

/// <summary>
/// Controller for match-related operations.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class MatchController(
    IGameStateRepository gameStateRepository,
    IMatchLifecycleService matchLifecycleService,
    ZootactDbContext dbContext,
    ILogger<MatchController> logger) : ControllerBase
{
    /// <summary>
    /// Gets the user's active match, if any.
    /// Used for reconnection after page refresh.
    /// </summary>
    [HttpGet("active")]
    [ProducesResponseType(typeof(ActiveMatchResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> GetActiveMatch()
    {
        var userId = GetUserId();
        if (userId is null)
            return Unauthorized();
        
        var matchId = await gameStateRepository.GetPlayerActiveMatchAsync(userId.Value);
        if (matchId is null)
            return NoContent();
        
        var gameState = await gameStateRepository.GetGameStateAsync(matchId.Value);
        if (gameState is null)
        {
            // Clean up stale active match reference
            await gameStateRepository.ClearPlayerActiveMatchAsync(userId.Value);
            return NoContent();
        }
        
        var playerColor = gameState.GetPlayerColor(userId.Value);
        var bluePlayer = await dbContext.Users.FindAsync(gameState.BluePlayerId);
        var redPlayer = await dbContext.Users.FindAsync(gameState.RedPlayerId);
        
        var response = new ActiveMatchResponse
        {
            MatchId = matchId.Value.ToString(),
            GameState = ConvertToGameStateDto(gameState, playerColor, bluePlayer, redPlayer)
        };
        
        logger.LogInformation("User {UserId} retrieved active match {MatchId}", userId, matchId);
        
        return Ok(response);
    }

    [HttpGet("{matchId:guid}/analysis")]
    [ProducesResponseType(typeof(MatchAnalysisResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetAnalysis(Guid matchId)
    {
        var userId = GetUserId();
        if (userId is null)
            return Unauthorized();

        var analysis = await matchLifecycleService.GetMatchAnalysisAsync(matchId, userId.Value);
        if (analysis is null)
            return NotFound();

        return Ok(analysis);
    }
    
    /// <summary>
    /// Gets the current user ID from claims.
    /// </summary>
    private Guid? GetUserId()
    {
        var userIdClaim = User.FindFirst("sub")?.Value 
            ?? User.FindFirst("id")?.Value
            ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        
        return Guid.TryParse(userIdClaim, out var userId) ? userId : null;
    }
    
    /// <summary>
    /// Converts domain GameState to DTO.
    /// </summary>
    private static GameStateDto ConvertToGameStateDto(GameState state, Player? playerColor, UserEntity? bluePlayer, UserEntity? redPlayer)
    {
        var (blueTimeRemainingMs, redTimeRemainingMs) =
            state.TimeControl.GetEffectiveRemainingTimes(state.CurrentTurn, DateTimeOffset.UtcNow);

        return new GameStateDto
        {
            MatchId = state.MatchId.ToString(),
            BluePlayer = new OpponentDto 
            { 
                Id = state.BluePlayerId.ToString(), 
                Username = bluePlayer?.Username ?? "Blue Player",
                AvatarUrl = bluePlayer?.AvatarUrl,
                ForestPoints = bluePlayer?.ForestPoints ?? 1200 
            },
            RedPlayer = new OpponentDto 
            { 
                Id = state.RedPlayerId.ToString(), 
                Username = redPlayer?.Username ?? "Red Player",
                AvatarUrl = redPlayer?.AvatarUrl,
                ForestPoints = redPlayer?.ForestPoints ?? 1200 
            },
            YourColor = playerColor?.ToString() ?? "Spectator",
            CurrentTurn = state.CurrentTurn.ToString(),
            Board = ConvertBoardToDto(state.Board),
            TimeControl = new TimeControlDto
            {
                Preset = state.TimeControl.Preset.ToString(),
                IsUntimed = state.TimeControl.IsUntimed,
                ClockMode = state.TimeControl.IsUntimed ? "countup" : "countdown",
                InitialTimeMs = state.TimeControl.InitialTimeMs,
                IncrementMs = state.TimeControl.IncrementMs
            },
            BlueTimeRemainingMs = blueTimeRemainingMs,
            RedTimeRemainingMs = redTimeRemainingMs,
            MoveCount = state.MoveCount,
            Status = state.Status.ToString(),
            Result = state.Result.ToString(),
            ResultReason = state.ResultReason,
            MoveHistory = [..state.MoveHistory]
        };
    }
    
    /// <summary>
    /// Converts domain Board to DTO.
    /// </summary>
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
