using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Zootact.API.Controllers;
using Zootact.Core.Domain;
using Zootact.Core.DTOs;
using Zootact.Core.Interfaces;
using Zootact.Infrastructure.Data;
using Zootact.Infrastructure.Data.Entities;

namespace Zootact.Tests.Controllers;

public sealed class LobbiesControllerTests
{
    [Fact]
    public async Task GetLobby_ReturnsForbiddenForNonParticipant()
    {
        var options = new DbContextOptionsBuilder<ZootactDbContext>()
            .UseInMemoryDatabase($"lobbies-controller-{Guid.NewGuid()}")
            .Options;

        await using var dbContext = new ZootactDbContext(options);
        var hostId = Guid.NewGuid();
        var guestId = Guid.NewGuid();
        var viewerId = Guid.NewGuid();
        var lobbyId = Guid.NewGuid();

        dbContext.Users.AddRange(
            CreateUser(hostId, "host"),
            CreateUser(guestId, "guest"),
            CreateUser(viewerId, "viewer"));
        await dbContext.SaveChangesAsync();

        var controller = new LobbiesController(new StubPrivateLobbyService(new PrivateLobby
        {
            LobbyId = lobbyId,
            HostUserId = hostId,
            GuestUserId = guestId,
            GuestReady = true,
            CountdownActive = false,
            CountdownEndAt = null,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        }), dbContext)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(
                    [
                        new Claim("sub", viewerId.ToString()),
                    ], "TestAuth"))
                }
            }
        };

        var result = await controller.GetLobby(lobbyId);

        Assert.IsType<ForbidResult>(result);
    }

    private static UserEntity CreateUser(Guid id, string username)
    {
        return new UserEntity
        {
            Id = id,
            FirebaseUid = $"{username}-firebase",
            Username = username,
            Email = $"{username}@example.com",
            ForestPoints = 1200
        };
    }

    private sealed class StubPrivateLobbyService(PrivateLobby? lobby) : IPrivateLobbyService
    {
        public Task<PrivateLobby> CreateLobbyAsync(Guid userId) => throw new NotImplementedException();
        public Task<PrivateLobby?> GetActiveLobbyAsync(Guid userId) => Task.FromResult<PrivateLobby?>(null);
        public Task<PrivateLobby?> GetLobbyAsync(Guid lobbyId) => Task.FromResult(lobby);
        public Task<PrivateLobby> JoinLobbyAsync(Guid lobbyId, Guid userId) => throw new NotImplementedException();
        public Task<PrivateLobby?> LeaveLobbyAsync(Guid lobbyId, Guid userId) => throw new NotImplementedException();
        public Task<PrivateLobby> SetGuestReadyAsync(Guid lobbyId, Guid userId, bool ready) => throw new NotImplementedException();
        public Task<PrivateLobby> StartCountdownAsync(Guid lobbyId, Guid userId) => throw new NotImplementedException();
        public Task<PrivateLobby> CancelCountdownAsync(Guid lobbyId, Guid userId) => throw new NotImplementedException();
        public Task<Guid?> TryStartMatchAsync(Guid lobbyId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    }
}
