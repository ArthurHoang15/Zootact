using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Zootact.Core.Domain;
using Zootact.Core.DTOs;
using Zootact.Core.GameLogic;
using Zootact.Core.Interfaces;

namespace Zootact.API.Hubs;

/// <summary>
/// SignalR hub for real-time game communication.
/// Handles game events: join, move, draw offers, resign, chat.
/// </summary>
[Authorize]
public sealed class GameHub(
    IGameStateRepository gameStateRepository,
    IMatchLifecycleService matchLifecycleService,
    IMatchNotificationService matchNotificationService,
    GameEngine gameEngine,
    ILogger<GameHub> logger) : Hub
{
    /// <summary>
    /// Joins a match room to receive real-time updates.
    /// </summary>
    /// <param name="matchId">Match ID to join.</param>
    public async Task JoinMatch(string matchId)
    {
        var userId = GetUserId();
        if (userId is null)
        {
            await Clients.Caller.SendAsync("OnError", new { error = "Unauthorized", message = "User not authenticated." });
            return;
        }
        
        if (!Guid.TryParse(matchId, out var matchGuid))
        {
            await Clients.Caller.SendAsync("OnError", new { error = "InvalidMatchId", message = "Invalid match ID format." });
            return;
        }
        
        var gameState = await gameStateRepository.GetGameStateAsync(matchGuid);
        if (gameState is null)
        {
            await Clients.Caller.SendAsync("OnError", new { error = "MatchNotFound", message = "Match not found." });
            return;
        }
        
        if (!gameState.IsParticipant(userId.Value))
        {
            await Clients.Caller.SendAsync("OnError", new { error = "NotParticipant", message = "You are not a participant in this match." });
            return;
        }
        
        // Add to SignalR group for the match
        await Groups.AddToGroupAsync(Context.ConnectionId, matchId);
        
        // Store connection mapping
        await gameStateRepository.SetPlayerConnectionAsync(userId.Value, Context.ConnectionId);
        
        var disconnectInfo = await gameStateRepository.GetPlayerDisconnectInfoAsync(matchGuid, userId.Value);
        if (disconnectInfo is not null && gameState.Result == GameResult.InProgress)
        {
            gameState.TimeControl = gameState.TimeControl.PauseFor(DateTimeOffset.UtcNow - disconnectInfo.DisconnectedAt);
            await gameStateRepository.SaveGameStateAsync(gameState);
        }

        // Clear any disconnect timer
        await gameStateRepository.ClearPlayerDisconnectedAsync(matchGuid, userId.Value);

        var (blueTimeRemainingMs, redTimeRemainingMs) =
            gameState.TimeControl.GetEffectiveRemainingTimes(gameState.CurrentTurn, DateTimeOffset.UtcNow);

        var timeSync = new TimeSyncDto
        {
            BlueTimeRemainingMs = blueTimeRemainingMs,
            RedTimeRemainingMs = redTimeRemainingMs,
            ServerTimestamp = DateTimeOffset.UtcNow.ToString("O")
        };

        await Clients.Group(matchId).SendAsync("OnTimeSync", timeSync);
        
        // Notify opponent of reconnection if they were waiting
        await Clients.OthersInGroup(matchId).SendAsync("OnOpponentReconnected");
        
        logger.LogInformation("User {UserId} joined match {MatchId}", userId, matchId);
    }
    
    /// <summary>
    /// Submits a move in the current game.
    /// </summary>
    /// <param name="request">Move details.</param>
    /// <returns>Move result.</returns>
    public async Task<MoveResultDto> MakeMove(MakeMoveRequest request)
    {
        var userId = GetUserId();
        if (userId is null)
        {
            return new MoveResultDto { Success = false, ErrorCode = "Unauthorized", ErrorMessage = "User not authenticated." };
        }
        
        var activeMatchId = await gameStateRepository.GetPlayerActiveMatchAsync(userId.Value);
        if (activeMatchId is null)
        {
            return new MoveResultDto { Success = false, ErrorCode = "NoActiveMatch", ErrorMessage = "No active match found." };
        }
        
        var gameState = await gameStateRepository.GetGameStateAsync(activeMatchId.Value);
        if (gameState is null)
        {
            return new MoveResultDto { Success = false, ErrorCode = "MatchNotFound", ErrorMessage = "Match state not found." };
        }
        
        // Create positions
        var from = new Position(request.FromRow, request.FromCol);
        var to = new Position(request.ToRow, request.ToCol);
        
        // Execute the move through the game engine
        var result = gameEngine.MakeMove(gameState, userId.Value, from, to);
        
        if (!result.IsSuccess)
        {
            if (result.IsTimeout)
            {
                await gameStateRepository.SaveGameStateAsync(gameState);
                var finalizedTimeout = await matchLifecycleService.FinalizeMatchAsync(activeMatchId.Value);
                if (finalizedTimeout is not null)
                {
                    await matchNotificationService.SendGameEndedAsync(finalizedTimeout);
                }
            }

            logger.LogDebug("Move rejected for user {UserId}: {ErrorCode} - {ErrorMessage}", 
                userId, result.ErrorCode, result.ErrorMessage);
            return new MoveResultDto 
            { 
                Success = false, 
                ErrorCode = result.ErrorCode, 
                ErrorMessage = result.ErrorMessage 
            };
        }
        
        // Save updated game state to Redis
        await gameStateRepository.SaveGameStateAsync(gameState);
        await matchLifecycleService.RecordMoveAsync(
            activeMatchId.Value,
            userId.Value,
            result.Move!,
            gameState.MoveCount,
            result.TimeSpentMs,
            result.PositionHash);
        
        var matchId = activeMatchId.Value.ToString();
        var playerColor = gameState.GetPlayerColor(userId.Value);
        
        // Broadcast move to both players
        var moveMade = new MoveMadeDto
        {
            PlayerColor = playerColor.ToString()!,
            From = new PositionDto { Row = from.Row, Col = from.Col },
            To = new PositionDto { Row = to.Row, Col = to.Col },
            CapturedPiece = result.Move?.CapturedPiece?.ToString(),
            BoardAfter = ConvertBoardToDto(result.BoardAfter!),
            BlueTimeRemainingMs = gameState.TimeControl.BlueTimeRemainingMs,
            RedTimeRemainingMs = gameState.TimeControl.RedTimeRemainingMs,
            MoveNumber = gameState.MoveCount
        };
        
        await Clients.Group(matchId).SendAsync("OnMoveMade", moveMade);
        
        // Check if game ended
        if (result.GameCheck?.IsGameOver == true)
        {
            await HandleGameEnd(activeMatchId.Value);
        }
        
        logger.LogInformation("Move made in match {MatchId}: {From} -> {To}", matchId, from, to);
        
        return new MoveResultDto { Success = true };
    }
    
    /// <summary>
    /// Offers a draw to the opponent.
    /// </summary>
    public async Task OfferDraw()
    {
        var userId = GetUserId();
        if (userId is null) return;
        
        var activeMatchId = await gameStateRepository.GetPlayerActiveMatchAsync(userId.Value);
        if (activeMatchId is null) return;
        
        var matchId = activeMatchId.Value.ToString();
        var username = Context.User?.Identity?.Name ?? "Unknown";
        
        // Notify opponent of draw offer
        await Clients.OthersInGroup(matchId).SendAsync("OnDrawOffered", username);
        
        logger.LogInformation("User {UserId} offered draw in match {MatchId}", userId, matchId);
    }
    
    /// <summary>
    /// Accepts a draw offer.
    /// </summary>
    public async Task AcceptDraw()
    {
        var userId = GetUserId();
        if (userId is null) return;
        
        var activeMatchId = await gameStateRepository.GetPlayerActiveMatchAsync(userId.Value);
        if (activeMatchId is null) return;
        
        var gameState = await gameStateRepository.GetGameStateAsync(activeMatchId.Value);
        if (gameState is null) return;
        
        // Set game as draw
        gameEngine.AcceptDraw(gameState);
        await gameStateRepository.SaveGameStateAsync(gameState);
        
        await HandleGameEnd(activeMatchId.Value);
        
        logger.LogInformation("Draw accepted in match {MatchId}", activeMatchId.Value);
    }
    
    /// <summary>
    /// Declines a draw offer.
    /// </summary>
    public async Task DeclineDraw()
    {
        var userId = GetUserId();
        if (userId is null) return;
        
        var activeMatchId = await gameStateRepository.GetPlayerActiveMatchAsync(userId.Value);
        if (activeMatchId is null) return;
        
        var matchId = activeMatchId.Value.ToString();
        
        // Notify opponent
        await Clients.OthersInGroup(matchId).SendAsync("OnDrawDeclined");
    }
    
    /// <summary>
    /// Resigns from the current game.
    /// </summary>
    public async Task Resign()
    {
        var userId = GetUserId();
        if (userId is null) return;
        
        var activeMatchId = await gameStateRepository.GetPlayerActiveMatchAsync(userId.Value);
        if (activeMatchId is null) return;
        
        var gameState = await gameStateRepository.GetGameStateAsync(activeMatchId.Value);
        if (gameState is null) return;
        
        // Process resignation
        gameEngine.Resign(gameState, userId.Value);
        await gameStateRepository.SaveGameStateAsync(gameState);
        
        await HandleGameEnd(activeMatchId.Value);
        
        logger.LogInformation("User {UserId} resigned in match {MatchId}", userId, activeMatchId.Value);
    }
    
    /// <summary>
    /// Sends a chat message to the match.
    /// </summary>
    /// <param name="message">Message content.</param>
    public async Task SendChat(string message)
    {
        var userId = GetUserId();
        if (userId is null) return;
        
        var activeMatchId = await gameStateRepository.GetPlayerActiveMatchAsync(userId.Value);
        if (activeMatchId is null) return;
        
        var trimmedMessage = message.Trim();
        if (string.IsNullOrWhiteSpace(trimmedMessage) || trimmedMessage.Length > 200)
            return;
        
        var matchId = activeMatchId.Value.ToString();
        var username = Context.User?.Identity?.Name ?? "Unknown";
        
        var chatMessage = new ChatMessageDto
        {
            SenderId = userId.Value.ToString(),
            SenderUsername = username,
            Message = trimmedMessage,
            Timestamp = DateTimeOffset.UtcNow.ToString("O")
        };
        
        await Clients.Group(matchId).SendAsync("OnChatReceived", chatMessage);
    }

    public async Task ReportWindowFocus(bool isFocused)
    {
        if (isFocused)
            return;

        var userId = GetUserId();
        if (userId is null)
            return;

        var activeMatchId = await gameStateRepository.GetPlayerActiveMatchAsync(userId.Value);
        if (activeMatchId is null)
            return;

        var gameState = await gameStateRepository.GetGameStateAsync(activeMatchId.Value);
        if (gameState is null || gameState.Result != GameResult.InProgress)
            return;

        if (userId.Value == gameState.BluePlayerId)
            gameState.BlueBlurCount++;
        else if (userId.Value == gameState.RedPlayerId)
            gameState.RedBlurCount++;

        await gameStateRepository.SaveGameStateAsync(gameState);
    }
    
    /// <summary>
    /// Handles client disconnection.
    /// </summary>
    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var userId = GetUserId();
        if (userId is not null)
        {
            var activeMatchId = await gameStateRepository.GetPlayerActiveMatchAsync(userId.Value);
            if (activeMatchId is not null)
            {
                // Set disconnect timer (60 seconds to reconnect)
                await gameStateRepository.SetPlayerDisconnectedAsync(
                    activeMatchId.Value, 
                    userId.Value, 
                    TimeSpan.FromSeconds(60));
                
                var matchId = activeMatchId.Value.ToString();
                
                // Notify opponent
                await Clients.OthersInGroup(matchId).SendAsync("OnOpponentDisconnected", 60);
                
                logger.LogInformation("User {UserId} disconnected from match {MatchId}", userId, matchId);
            }
            
            await gameStateRepository.ClearPlayerConnectionAsync(userId.Value);
        }
        
        await base.OnDisconnectedAsync(exception);
    }
    
    /// <summary>
    /// Gets the authenticated user's ID from claims.
    /// </summary>
    private Guid? GetUserId()
    {
        var userIdClaim = Context.User?.FindFirst("sub")?.Value 
            ?? Context.User?.FindFirst("id")?.Value
            ?? Context.User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        
        return Guid.TryParse(userIdClaim, out var userId) ? userId : null;
    }
    
    /// <summary>
    /// Handles game end logic.
    /// </summary>
    private async Task HandleGameEnd(Guid matchId)
    {
        var finalizedMatch = await matchLifecycleService.FinalizeMatchAsync(matchId);
        if (finalizedMatch is null)
            return;

        await matchNotificationService.SendGameEndedAsync(finalizedMatch);
    }
    
    /// <summary>
    /// Converts domain Board to BoardDto.
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
