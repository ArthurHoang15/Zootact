using Microsoft.AspNetCore.Http;
using Zootact.API.Services;

namespace Zootact.Tests.Services;

public sealed class RequestLoggingAndAuthTrackingTests
{
    [Fact]
    public void SanitizeRequestPath_RedactsAccessTokenButKeepsOtherQueryValues()
    {
        var request = new DefaultHttpContext().Request;
        request.Path = "/game-hub";
        request.QueryString = new QueryString("?id=abc123&access_token=secret-token&foo=bar");

        var sanitized = RequestLoggingSanitizer.SanitizeRequestPath(request);

        Assert.Equal("/game-hub?id=abc123&access_token=%5Bredacted%5D&foo=bar", sanitized);
    }

    [Fact]
    public void ShouldUpdateLastLogin_ReturnsTrueForBootstrapRouteWhenOutsideThrottleWindow()
    {
        var shouldUpdate = LastLoginTracker.ShouldUpdate(
            "/api/auth/me",
            DateTimeOffset.UtcNow.AddMinutes(-6),
            DateTimeOffset.UtcNow);

        Assert.True(shouldUpdate);
    }

    [Fact]
    public void ShouldUpdateLastLogin_ReturnsFalseForNonBootstrapRoute()
    {
        var shouldUpdate = LastLoginTracker.ShouldUpdate(
            "/api/lobbies/active",
            DateTimeOffset.UtcNow.AddHours(-1),
            DateTimeOffset.UtcNow);

        Assert.False(shouldUpdate);
    }

    [Fact]
    public void ShouldUpdateLastLogin_ReturnsFalseInsideThrottleWindow()
    {
        var now = DateTimeOffset.UtcNow;

        var shouldUpdate = LastLoginTracker.ShouldUpdate(
            "/api/auth/me",
            now.AddMinutes(-2),
            now);

        Assert.False(shouldUpdate);
    }
}
