using FirebaseAdmin.Auth;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Npgsql;
using Zootact.Infrastructure.Data;
using Zootact.Infrastructure.Data.Entities;

namespace Zootact.API.Middleware;

/// <summary>
/// Middleware to verify Firebase ID tokens and auto-sync users to PostgreSQL.
/// </summary>
public class FirebaseAuthMiddleware
{
    private const int MaxFirebaseUidLength = 128;
    private const int MaxUsernameLength = 50;
    private const int MaxEmailLength = 512;
    private const int MaxAvatarUrlLength = 2048;

    private readonly RequestDelegate _next;
    private readonly ILogger<FirebaseAuthMiddleware> _logger;

    public FirebaseAuthMiddleware(RequestDelegate next, ILogger<FirebaseAuthMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, ZootactDbContext dbContext)
    {
        // Skip authentication for public endpoints
        var path = context.Request.Path.Value?.ToLower() ?? "";
        if (IsPublicEndpoint(path))
        {
            await _next(context);
            return;
        }

        try
        {
            // Extract Firebase ID token from Authorization header
            var authHeader = context.Request.Headers.Authorization.FirstOrDefault();
            var queryToken = context.Request.Query["access_token"].FirstOrDefault();
            var token = !string.IsNullOrWhiteSpace(authHeader) && authHeader.StartsWith("Bearer ")
                ? authHeader["Bearer ".Length..].Trim()
                : queryToken;

            if (string.IsNullOrWhiteSpace(token))
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await context.Response.WriteAsJsonAsync(new { error = "Missing or invalid Authorization header" });
                return;
            }

            // Verify token with Firebase
            FirebaseToken decodedToken;
            try
            {
                decodedToken = await FirebaseAuth.DefaultInstance.VerifyIdTokenAsync(token);
            }
            catch (FirebaseAuthException ex)
            {
                _logger.LogWarning("Firebase token verification failed: {Message}", ex.Message);
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await context.Response.WriteAsJsonAsync(new { error = "Invalid or expired token" });
                return;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error while verifying Firebase token");
                context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
                await WriteErrorResponseAsync(context, "Authentication provider unavailable", ex, StatusCodes.Status503ServiceUnavailable);
                return;
            }

            // Extract user info from token
            var rawFirebaseUid = decodedToken.Uid;
            var rawEmail = decodedToken.Claims.GetValueOrDefault("email")?.ToString();
            var rawPhotoUrl = decodedToken.Claims.GetValueOrDefault("picture")?.ToString();

            var firebaseUid = Truncate(rawFirebaseUid, MaxFirebaseUidLength);
            var email = NormalizeEmail(firebaseUid, rawEmail);
            var displayName = NormalizeOptional(decodedToken.Claims.GetValueOrDefault("name")?.ToString());
            var photoUrl = NormalizeAvatarUrl(rawPhotoUrl);

            LogIfFirebaseProfileWasSanitized(firebaseUid, rawFirebaseUid, rawEmail, email, rawPhotoUrl, photoUrl);

            var user = await SyncUserAsync(
                context,
                dbContext,
                firebaseUid,
                email,
                displayName,
                photoUrl);

            // Check if user is banned
            if (user.IsBanned)
            {
                _logger.LogWarning("Banned user attempted login: {UserId}", user.Id);
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                await context.Response.WriteAsJsonAsync(new { error = "Account has been banned" });
                return;
            }

            // Attach user info to HttpContext for downstream use
            context.Items["User"] = user;
            context.Items["FirebaseUid"] = firebaseUid;
            context.Items["FirebaseToken"] = decodedToken;

            // Add claims to User principal
            var claims = new List<System.Security.Claims.Claim>
            {
                new("uid", firebaseUid),
                new("sub", user.Id.ToString()),
                new("id", user.Id.ToString()),
                new(System.Security.Claims.ClaimTypes.NameIdentifier, user.Id.ToString()),
                new(System.Security.Claims.ClaimTypes.Name, user.Username),
                new(System.Security.Claims.ClaimTypes.Email, user.Email)
            };

            var identity = new System.Security.Claims.ClaimsIdentity(claims, "Firebase");
            context.User = new System.Security.Claims.ClaimsPrincipal(identity);

            await _next(context);
        }
        catch (DbUpdateException ex)
        {
            _logger.LogError(ex, "Database error while syncing Firebase user for path {Path}", context.Request.Path);
            await WriteErrorResponseAsync(context, "Authentication data sync unavailable", ex, StatusCodes.Status503ServiceUnavailable);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in Firebase auth middleware");
            await WriteErrorResponseAsync(context, "Authentication service error", ex, StatusCodes.Status500InternalServerError);
        }
    }

    private async Task SaveUserChangesAsync(HttpContext context, ZootactDbContext dbContext)
    {
        SanitizeTrackedUsers(dbContext.ChangeTracker);

        try
        {
            await dbContext.SaveChangesAsync(context.RequestAborted);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException postgresException && postgresException.SqlState == PostgresErrorCodes.StringDataRightTruncation)
        {
            LogTrackedUserFieldLengths(dbContext.ChangeTracker);
            throw;
        }
    }

    private async Task<UserEntity> SyncUserAsync(
        HttpContext context,
        ZootactDbContext dbContext,
        string firebaseUid,
        string? email,
        string? displayName,
        string? photoUrl)
    {
        const int maxAttempts = 4;

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            dbContext.ChangeTracker.Clear();

            var user = await FindUserAsync(dbContext, firebaseUid, email, context.RequestAborted);
            if (user is not null)
            {
                var relinked = user.FirebaseUid != firebaseUid;
                user.FirebaseUid = firebaseUid;
                user.AvatarUrl = photoUrl;
                user.LastLoginAt = DateTimeOffset.UtcNow;
                await EnsureUserStatsExistsAsync(dbContext, user.Id, context.RequestAborted);

                try
                {
                    await SaveUserChangesAsync(context, dbContext);
                    if (relinked)
                    {
                        _logger.LogInformation(
                            "Re-linked existing user {UserId} to new Firebase UID {FirebaseUid}",
                            user.Id,
                            firebaseUid);
                    }

                    return user;
                }
                catch (DbUpdateException ex) when (attempt < maxAttempts)
                {
                    _logger.LogWarning(
                        ex,
                        "Retrying Firebase user sync for existing user {FirebaseUid} (attempt {Attempt}/{MaxAttempts})",
                        firebaseUid,
                        attempt,
                        maxAttempts);
                    await Task.Delay(100 * attempt, context.RequestAborted);
                    continue;
                }
            }

            var username = await GenerateUniqueUsernameAsync(dbContext, email, displayName);
            var newUser = new UserEntity
            {
                FirebaseUid = firebaseUid,
                Username = username,
                Email = email ?? $"{firebaseUid}@firebase.local",
                AvatarUrl = photoUrl,
                ForestPoints = 1200,
                CreatedAt = DateTimeOffset.UtcNow,
                LastLoginAt = DateTimeOffset.UtcNow
            };

            dbContext.Users.Add(newUser);
            dbContext.UserStats.Add(new UserStatsEntity
            {
                UserId = newUser.Id
            });

            try
            {
                await SaveUserChangesAsync(context, dbContext);
                _logger.LogInformation(
                    "Created new user from Firebase: {FirebaseUid} -> {Username}",
                    firebaseUid,
                    username);
                return newUser;
            }
            catch (DbUpdateException ex) when (attempt < maxAttempts)
            {
                _logger.LogWarning(
                    ex,
                    "Retrying Firebase user bootstrap for {FirebaseUid} (attempt {Attempt}/{MaxAttempts})",
                    firebaseUid,
                    attempt,
                    maxAttempts);
                await Task.Delay(100 * attempt, context.RequestAborted);
            }
        }

        throw new DbUpdateException($"Failed to sync Firebase user {firebaseUid} after multiple attempts.");
    }

    private static async Task<UserEntity?> FindUserAsync(
        ZootactDbContext dbContext,
        string firebaseUid,
        string? email,
        CancellationToken cancellationToken)
    {
        var user = await dbContext.Users.FirstOrDefaultAsync(u => u.FirebaseUid == firebaseUid, cancellationToken);
        if (user is not null || string.IsNullOrWhiteSpace(email))
        {
            return user;
        }

        return await dbContext.Users.FirstOrDefaultAsync(u => u.Email == email, cancellationToken);
    }

    private static async Task EnsureUserStatsExistsAsync(
        ZootactDbContext dbContext,
        Guid userId,
        CancellationToken cancellationToken)
    {
        var statsExists = await dbContext.UserStats.AnyAsync(s => s.UserId == userId, cancellationToken);
        if (!statsExists)
        {
            dbContext.UserStats.Add(new UserStatsEntity
            {
                UserId = userId
            });
        }
    }

    private static async Task WriteErrorResponseAsync(
        HttpContext context,
        string error,
        Exception exception,
        int statusCode)
    {
        context.Response.StatusCode = statusCode;

        var environment = context.RequestServices.GetService<IHostEnvironment>();
        if (environment?.IsDevelopment() == true)
        {
            await context.Response.WriteAsJsonAsync(new
            {
                error,
                exception = exception.GetType().Name,
                message = exception.Message
            });
            return;
        }

        await context.Response.WriteAsJsonAsync(new { error });
    }

    private static bool IsPublicEndpoint(string path)
    {
        // Public endpoints that don't require authentication
        var publicPaths = new[]
        {
            "/health",
            "/swagger"
        };

        return publicPaths.Any(p => path.StartsWith(p));
    }

    private static async Task<string> GenerateUniqueUsernameAsync(
        ZootactDbContext dbContext,
        string? email,
        string? displayName)
    {
        var baseUsername = GenerateUsernameBase(email, displayName);
        var candidate = baseUsername;
        var suffix = 1;

        while (await dbContext.Users.AnyAsync(u => u.Username == candidate))
        {
            var suffixText = suffix.ToString();
            var maxBaseLength = Math.Max(1, 20 - suffixText.Length);
            candidate = $"{baseUsername[..Math.Min(baseUsername.Length, maxBaseLength)]}{suffixText}";
            suffix++;
        }

        return Truncate(candidate, MaxUsernameLength);
    }

    private static string GenerateUsernameBase(string? email, string? displayName)
    {
        if (!string.IsNullOrWhiteSpace(displayName))
        {
            // Use display name, sanitize it, and cap after sanitizing.
            var sanitized = new string(displayName
                .Where(char.IsLetterOrDigit)
                .ToArray())
                .ToLowerInvariant();

            if (!string.IsNullOrWhiteSpace(sanitized))
            {
                return Truncate(sanitized, 20);
            }
        }

        if (!string.IsNullOrEmpty(email))
        {
            // Use email prefix
            var emailPrefix = email.Split('@')[0];
            var sanitized = new string(emailPrefix
                .Where(char.IsLetterOrDigit)
                .ToArray())
                .ToLowerInvariant();

            if (!string.IsNullOrWhiteSpace(sanitized))
            {
                return Truncate(sanitized, 20);
            }
        }

        // Fallback to random username
        return $"user_{Guid.NewGuid().ToString()[..8]}";
    }

    private void LogIfFirebaseProfileWasSanitized(
        string firebaseUid,
        string? rawFirebaseUid,
        string? rawEmail,
        string? normalizedEmail,
        string? rawPhotoUrl,
        string? normalizedPhotoUrl)
    {
        var firebaseUidTrimmed = !string.Equals(rawFirebaseUid, firebaseUid, StringComparison.Ordinal);
        var emailChanged = !string.Equals(rawEmail?.Trim(), normalizedEmail, StringComparison.Ordinal);
        var avatarChanged = !string.Equals(rawPhotoUrl?.Trim(), normalizedPhotoUrl, StringComparison.Ordinal);

        if (!firebaseUidTrimmed && !emailChanged && !avatarChanged)
        {
            return;
        }

        _logger.LogInformation(
            "Sanitized Firebase profile for {FirebaseUid}. Raw lengths uid={RawUidLength}, email={RawEmailLength}, avatar={RawAvatarLength}; stored lengths uid={StoredUidLength}, email={StoredEmailLength}, avatar={StoredAvatarLength}",
            firebaseUid,
            rawFirebaseUid?.Length ?? 0,
            rawEmail?.Length ?? 0,
            rawPhotoUrl?.Length ?? 0,
            firebaseUid.Length,
            normalizedEmail?.Length ?? 0,
            normalizedPhotoUrl?.Length ?? 0);
    }

    private void LogTrackedUserFieldLengths(Microsoft.EntityFrameworkCore.ChangeTracking.ChangeTracker changeTracker)
    {
        foreach (var entry in changeTracker.Entries<UserEntity>()
                     .Where(e => e.State is EntityState.Added or EntityState.Modified))
        {
            var user = entry.Entity;
            _logger.LogError(
                "Tracked user field lengths before save: userId={UserId}, firebaseUid={FirebaseUidLength}, username={UsernameLength}, email={EmailLength}, avatarUrl={AvatarUrlLength}",
                user.Id,
                user.FirebaseUid?.Length ?? 0,
                user.Username?.Length ?? 0,
                user.Email?.Length ?? 0,
                user.AvatarUrl?.Length ?? 0);
        }
    }

    private static void SanitizeTrackedUsers(Microsoft.EntityFrameworkCore.ChangeTracking.ChangeTracker changeTracker)
    {
        foreach (var entry in changeTracker.Entries<UserEntity>()
                     .Where(e => e.State is EntityState.Added or EntityState.Modified))
        {
            var user = entry.Entity;
            user.FirebaseUid = Truncate(user.FirebaseUid, MaxFirebaseUidLength);
            user.Username = TruncateRequired(user.Username, MaxUsernameLength, "user");
            user.Email = NormalizeEmail(user.FirebaseUid, user.Email);
            user.AvatarUrl = NormalizeAvatarUrl(user.AvatarUrl);
        }
    }

    private static string? NormalizeOptional(string? value, int? maxLength = null)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        return maxLength.HasValue ? Truncate(trimmed, maxLength.Value) : trimmed;
    }

    private static string NormalizeEmail(string firebaseUid, string? email)
    {
        var normalized = NormalizeOptional(email);
        if (string.IsNullOrWhiteSpace(normalized) || normalized.Length > MaxEmailLength)
        {
            return $"{firebaseUid}@firebase.local";
        }

        return normalized;
    }

    private static string? NormalizeAvatarUrl(string? avatarUrl)
    {
        var normalized = NormalizeOptional(avatarUrl);
        if (normalized is null)
        {
            return null;
        }

        return normalized.Length > MaxAvatarUrlLength ? null : normalized;
    }

    private static string TruncateRequired(string? value, int maxLength, string fallback)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return fallback;
        }

        return Truncate(value.Trim(), maxLength);
    }

    private static string Truncate(string value, int maxLength) =>
        value.Length <= maxLength ? value : value[..maxLength];
}

/// <summary>
/// Extension method to add Firebase auth middleware to the pipeline.
/// </summary>
public static class FirebaseAuthMiddlewareExtensions
{
    public static IApplicationBuilder UseFirebaseAuth(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<FirebaseAuthMiddleware>();
    }
}
