using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using Zootact.Core.Domain;
using Zootact.Core.DTOs;
using Zootact.Core.Interfaces;
using Zootact.Infrastructure.Data;
using Zootact.Infrastructure.Data.Entities;

namespace Zootact.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public sealed class LobbiesController(
    IPrivateLobbyService privateLobbyService,
    ZootactDbContext dbContext) : ControllerBase
{
    [HttpPost]
    [ProducesResponseType(typeof(LobbyActionResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateLobby([FromBody] CreateLobbyRequest request)
    {
        var userId = GetUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        if (!Enum.TryParse<TimeControlPreset>(request.TimeControl, out var preset))
        {
            return BadRequest(new ErrorResponse
            {
                Error = "InvalidTimeControl",
                Message = "Invalid time control preset."
            });
        }

        try
        {
            var lobby = await privateLobbyService.CreateLobbyAsync(userId.Value, preset);
            return CreatedAtAction(nameof(GetLobby), new { id = lobby.LobbyId }, new LobbyActionResponse
            {
                Message = "Private lobby created.",
                Lobby = await BuildLobbyDtoAsync(lobby, userId.Value)
            });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new ErrorResponse { Error = "CreateLobbyFailed", Message = ex.Message });
        }
    }

    [HttpGet("active")]
    [ProducesResponseType(typeof(PrivateLobbyDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> GetActiveLobby()
    {
        var userId = GetUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        var lobby = await privateLobbyService.GetActiveLobbyAsync(userId.Value);
        if (lobby is null)
        {
            return NoContent();
        }

        return Ok(await BuildLobbyDtoAsync(lobby, userId.Value));
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(PrivateLobbyDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetLobby(Guid id)
    {
        var userId = GetUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        var lobby = await privateLobbyService.GetLobbyAsync(id);
        if (lobby is null)
        {
            return NotFound();
        }

        return Ok(await BuildLobbyDtoAsync(lobby, userId.Value));
    }

    [HttpPost("{id:guid}/join")]
    [ProducesResponseType(typeof(LobbyActionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> JoinLobby(Guid id)
    {
        var userId = GetUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        try
        {
            var lobby = await privateLobbyService.JoinLobbyAsync(id, userId.Value);
            return Ok(new LobbyActionResponse
            {
                Message = "Joined private lobby.",
                Lobby = await BuildLobbyDtoAsync(lobby, userId.Value)
            });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new ErrorResponse { Error = "JoinLobbyFailed", Message = ex.Message });
        }
    }

    [HttpPost("{id:guid}/leave")]
    [ProducesResponseType(typeof(LobbyActionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> LeaveLobby(Guid id)
    {
        var userId = GetUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        try
        {
            var lobby = await privateLobbyService.LeaveLobbyAsync(id, userId.Value);
            return Ok(new LobbyActionResponse
            {
                Message = lobby is null ? "Lobby closed." : "Left private lobby.",
                Lobby = lobby is null ? null : await BuildLobbyDtoAsync(lobby, userId.Value)
            });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new ErrorResponse { Error = "LeaveLobbyFailed", Message = ex.Message });
        }
    }

    [HttpPost("{id:guid}/ready")]
    [ProducesResponseType(typeof(LobbyActionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> SetReady(Guid id, [FromBody] LobbyReadyRequest request)
    {
        var userId = GetUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        try
        {
            var lobby = await privateLobbyService.SetGuestReadyAsync(id, userId.Value, request.Ready);
            return Ok(new LobbyActionResponse
            {
                Message = request.Ready ? "Ready state enabled." : "Ready state disabled.",
                Lobby = await BuildLobbyDtoAsync(lobby, userId.Value)
            });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new ErrorResponse { Error = "ReadyStateFailed", Message = ex.Message });
        }
    }

    [HttpPost("{id:guid}/start")]
    [ProducesResponseType(typeof(LobbyActionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Start(Guid id)
    {
        var userId = GetUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        try
        {
            var lobby = await privateLobbyService.StartCountdownAsync(id, userId.Value);
            return Ok(new LobbyActionResponse
            {
                Message = "Countdown started.",
                Lobby = await BuildLobbyDtoAsync(lobby, userId.Value)
            });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new ErrorResponse { Error = "StartFailed", Message = ex.Message });
        }
    }

    [HttpPost("{id:guid}/cancel-start")]
    [ProducesResponseType(typeof(LobbyActionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CancelStart(Guid id)
    {
        var userId = GetUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        try
        {
            var lobby = await privateLobbyService.CancelCountdownAsync(id, userId.Value);
            return Ok(new LobbyActionResponse
            {
                Message = "Countdown canceled.",
                Lobby = await BuildLobbyDtoAsync(lobby, userId.Value)
            });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new ErrorResponse { Error = "CancelStartFailed", Message = ex.Message });
        }
    }

    private Guid? GetUserId()
    {
        var userIdClaim = User.FindFirst("sub")?.Value
            ?? User.FindFirst("id")?.Value
            ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        return Guid.TryParse(userIdClaim, out var userId) ? userId : null;
    }

    private async Task<PrivateLobbyDto> BuildLobbyDtoAsync(PrivateLobby lobby, Guid currentUserId)
    {
        var host = await dbContext.Users.FindAsync(lobby.HostUserId)
            ?? throw new InvalidOperationException("Host user not found.");

        var guest = lobby.GuestUserId.HasValue
            ? await dbContext.Users.FindAsync(lobby.GuestUserId.Value)
            : null;

        return new PrivateLobbyDto
        {
            LobbyId = lobby.LobbyId.ToString(),
            Preset = lobby.Preset.ToString(),
            Host = ToLobbyPlayerDto(host, isHost: true, isReady: false),
            Guest = guest is null ? null : ToLobbyPlayerDto(guest, isHost: false, isReady: lobby.GuestReady),
            CurrentUserRole = currentUserId == lobby.HostUserId
                ? "Host"
                : currentUserId == lobby.GuestUserId
                    ? "Guest"
                    : "Viewer",
            CountdownActive = lobby.CountdownActive,
            CountdownEndAt = lobby.CountdownEndAt,
            CountdownSecondsRemaining = lobby.CountdownEndAt.HasValue
                ? Math.Max(0, (int)Math.Ceiling((lobby.CountdownEndAt.Value - DateTimeOffset.UtcNow).TotalSeconds))
                : 0,
            CanStart = lobby.CanStart
        };
    }

    private static LobbyPlayerDto ToLobbyPlayerDto(UserEntity user, bool isHost, bool isReady)
    {
        return new LobbyPlayerDto
        {
            Id = user.Id.ToString(),
            Username = user.Username,
            AvatarUrl = user.AvatarUrl,
            ForestPoints = user.ForestPoints,
            IsHost = isHost,
            IsReady = isReady
        };
    }
}
