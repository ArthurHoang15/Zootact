using Microsoft.AspNetCore.SignalR;
using Zootact.API.Hubs;
using Zootact.Core.Domain;
using Zootact.Core.DTOs;
using Zootact.Core.Interfaces;
using Zootact.Infrastructure.Data;

namespace Zootact.API.Services;

public sealed class SignalRMatchNotificationService(
    IHubContext<GameHub> hubContext,
    IGameStateRepository gameStateRepository,
    ZootactDbContext dbContext) : IMatchNotificationService
{
    public async Task SendMatchStartedAsync(GameState gameState)
    {
        var blueUser = await dbContext.Users.FindAsync(gameState.BluePlayerId);
        var redUser = await dbContext.Users.FindAsync(gameState.RedPlayerId);
        var blueConnection = await gameStateRepository.GetPlayerConnectionAsync(gameState.BluePlayerId);
        var redConnection = await gameStateRepository.GetPlayerConnectionAsync(gameState.RedPlayerId);

        if (blueConnection is not null)
        {
            await hubContext.Clients.Client(blueConnection).SendAsync("OnMatchStart", new MatchStartDto
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
                    IsUntimed = gameState.TimeControl.IsUntimed,
                    ClockMode = gameState.TimeControl.IsUntimed ? "countup" : "countdown",
                    InitialTimeMs = gameState.TimeControl.InitialTimeMs,
                    IncrementMs = gameState.TimeControl.IncrementMs
                },
                InitialBoard = ConvertBoardToDto(gameState.Board)
            });
        }

        if (redConnection is not null)
        {
            await hubContext.Clients.Client(redConnection).SendAsync("OnMatchStart", new MatchStartDto
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
                    IsUntimed = gameState.TimeControl.IsUntimed,
                    ClockMode = gameState.TimeControl.IsUntimed ? "countup" : "countdown",
                    InitialTimeMs = gameState.TimeControl.InitialTimeMs,
                    IncrementMs = gameState.TimeControl.IncrementMs
                },
                InitialBoard = ConvertBoardToDto(gameState.Board)
            });
        }
    }

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
