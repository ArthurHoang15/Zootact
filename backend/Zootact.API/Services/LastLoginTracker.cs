namespace Zootact.API.Services;

public static class LastLoginTracker
{
    private static readonly TimeSpan UpdateThrottle = TimeSpan.FromMinutes(5);

    public static bool ShouldUpdate(string? requestPath, DateTimeOffset? lastLoginAt, DateTimeOffset now)
    {
        if (!string.Equals(requestPath, "/api/auth/me", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!lastLoginAt.HasValue)
        {
            return true;
        }

        return now - lastLoginAt.Value >= UpdateThrottle;
    }
}
