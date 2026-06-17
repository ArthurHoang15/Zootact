using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Zootact.API.Controllers;
using Zootact.Core.DTOs;
using Zootact.Infrastructure.Data;
using Zootact.Infrastructure.Data.Entities;

namespace Zootact.Tests.Controllers;

public sealed class AuthControllerTests
{
    [Fact]
    public async Task UpdateProfile_RejectsUsernameLongerThanFiftyCharacters()
    {
        await using var dbContext = CreateDbContext();
        var user = await SeedUserAsync(dbContext, "player");
        var controller = CreateController(dbContext, user);

        var result = await controller.UpdateProfile(new UpdateProfileRequest
        {
            Username = new string('a', 51)
        });

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        var error = Assert.IsType<ErrorResponse>(badRequest.Value);
        Assert.Equal("InvalidUsername", error.Error);
    }

    [Fact]
    public async Task UpdateProfile_NormalizesValidUsernameToLowercase()
    {
        await using var dbContext = CreateDbContext();
        var user = await SeedUserAsync(dbContext, "player");
        var controller = CreateController(dbContext, user);

        var result = await controller.UpdateProfile(new UpdateProfileRequest
        {
            Username = "NewName123"
        });

        Assert.IsType<OkObjectResult>(result);
        Assert.Equal("newname123", user.Username);
    }

    private static ZootactDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<ZootactDbContext>()
            .UseInMemoryDatabase($"auth-controller-{Guid.NewGuid()}")
            .Options;

        return new ZootactDbContext(options);
    }

    private static async Task<UserEntity> SeedUserAsync(ZootactDbContext dbContext, string username)
    {
        var user = new UserEntity
        {
            Id = Guid.NewGuid(),
            FirebaseUid = $"{username}-firebase",
            Username = username,
            Email = $"{username}@example.com",
            ForestPoints = 1200
        };

        dbContext.Users.Add(user);
        dbContext.UserStats.Add(new UserStatsEntity
        {
            UserId = user.Id
        });
        await dbContext.SaveChangesAsync();
        return user;
    }

    private static AuthController CreateController(ZootactDbContext dbContext, UserEntity user)
    {
        return new AuthController(dbContext, NullLogger<AuthController>.Instance)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        }.WithUserItem(user);
    }
}

file static class AuthControllerTestExtensions
{
    public static AuthController WithUserItem(this AuthController controller, UserEntity user)
    {
        controller.HttpContext.Items["User"] = user;
        return controller;
    }
}
