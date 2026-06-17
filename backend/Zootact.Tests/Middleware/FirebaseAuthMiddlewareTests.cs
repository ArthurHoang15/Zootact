using Microsoft.EntityFrameworkCore;
using Zootact.API.Middleware;
using Zootact.Infrastructure.Data;
using Zootact.Infrastructure.Data.Entities;

namespace Zootact.Tests.Middleware;

public sealed class FirebaseAuthMiddlewareTests
{
    [Fact]
    public async Task FindUserForFirebaseAsync_DoesNotRelinkUnverifiedEmail()
    {
        var options = new DbContextOptionsBuilder<ZootactDbContext>()
            .UseInMemoryDatabase($"firebase-auth-{Guid.NewGuid()}")
            .Options;

        await using var dbContext = new ZootactDbContext(options);
        var existingUser = CreateUser("existing-firebase", "player@example.com");
        dbContext.Users.Add(existingUser);
        await dbContext.SaveChangesAsync();

        var resolved = await FirebaseAuthMiddleware.FindUserForFirebaseAsync(
            dbContext,
            "attacker-firebase",
            "player@example.com",
            emailVerified: false,
            CancellationToken.None);

        Assert.Null(resolved);
    }

    [Fact]
    public async Task FindUserForFirebaseAsync_AllowsVerifiedEmailRelink()
    {
        var options = new DbContextOptionsBuilder<ZootactDbContext>()
            .UseInMemoryDatabase($"firebase-auth-{Guid.NewGuid()}")
            .Options;

        await using var dbContext = new ZootactDbContext(options);
        var existingUser = CreateUser("existing-firebase", "player@example.com");
        dbContext.Users.Add(existingUser);
        await dbContext.SaveChangesAsync();

        var resolved = await FirebaseAuthMiddleware.FindUserForFirebaseAsync(
            dbContext,
            "new-firebase",
            "player@example.com",
            emailVerified: true,
            CancellationToken.None);

        Assert.Same(existingUser, resolved);
    }

    private static UserEntity CreateUser(string firebaseUid, string email)
    {
        return new UserEntity
        {
            Id = Guid.NewGuid(),
            FirebaseUid = firebaseUid,
            Username = email.Split('@')[0],
            Email = email,
            ForestPoints = 1200
        };
    }
}
