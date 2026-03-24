using FirebaseAdmin.Auth;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Zootact.Infrastructure.Data;
using Zootact.Infrastructure.Data.Entities;

namespace Zootact.API.Middleware;

/// <summary>
/// Middleware to verify Firebase ID tokens and auto-sync users to PostgreSQL.
/// </summary>
public class FirebaseAuthMiddleware
{
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
            var firebaseUid = decodedToken.Uid;
            var email = decodedToken.Claims.GetValueOrDefault("email")?.ToString();
            var emailVerified = decodedToken.Claims.GetValueOrDefault("email_verified") as bool? ?? false;
            var displayName = decodedToken.Claims.GetValueOrDefault("name")?.ToString();
            var photoUrl = decodedToken.Claims.GetValueOrDefault("picture")?.ToString();

            // Auto-sync user to PostgreSQL (upsert)
            var user = await dbContext.Users.FirstOrDefaultAsync(u => u.FirebaseUid == firebaseUid);
            if (user == null && !string.IsNullOrWhiteSpace(email))
            {
                // Firebase accounts can be recreated and get a new UID while keeping the same email.
                user = await dbContext.Users.FirstOrDefaultAsync(u => u.Email == email);
                if (user != null)
                {
                    user.FirebaseUid = firebaseUid;
                    user.AvatarUrl = photoUrl;
                    user.LastLoginAt = DateTimeOffset.UtcNow;
                    await SaveUserChangesAsync(context, dbContext);

                    _logger.LogInformation(
                        "Re-linked existing user {UserId} to new Firebase UID {FirebaseUid}",
                        user.Id,
                        firebaseUid);
                }
            }
            
            if (user == null)
            {
                // First login - create new user
                var username = await GenerateUniqueUsernameAsync(dbContext, email, displayName);
                
                user = new UserEntity
                {
                    FirebaseUid = firebaseUid,
                    Username = username,
                    Email = email ?? $"{firebaseUid}@firebase.local",
                    AvatarUrl = photoUrl,
                    ForestPoints = 1200,
                    CreatedAt = DateTimeOffset.UtcNow,
                    LastLoginAt = DateTimeOffset.UtcNow
                };

                dbContext.Users.Add(user);

                // Create initial stats
                var stats = new UserStatsEntity
                {
                    UserId = user.Id
                };
                dbContext.UserStats.Add(stats);

                try
                {
                    await SaveUserChangesAsync(context, dbContext);
                }
                catch (DbUpdateException) when (!string.IsNullOrWhiteSpace(email))
                {
                    // Multiple auth/bootstrap requests can race. If another request already created or re-linked
                    // the user, load that row and continue instead of failing the whole auth flow.
                    dbContext.ChangeTracker.Clear();
                    user = await dbContext.Users.FirstOrDefaultAsync(u =>
                        u.FirebaseUid == firebaseUid || u.Email == email);

                    if (user == null)
                    {
                        throw;
                    }

                    if (user.FirebaseUid != firebaseUid)
                    {
                        user.FirebaseUid = firebaseUid;
                    }

                    user.AvatarUrl = photoUrl;
                    user.LastLoginAt = DateTimeOffset.UtcNow;

                    var statsExists = await dbContext.UserStats.AnyAsync(s => s.UserId == user.Id);
                    if (!statsExists)
                    {
                        dbContext.UserStats.Add(new UserStatsEntity
                        {
                            UserId = user.Id
                        });
                    }

                    await SaveUserChangesAsync(context, dbContext);
                }
                
                _logger.LogInformation("Created new user from Firebase: {FirebaseUid} -> {Username}", 
                    firebaseUid, username);
            }
            else
            {
                // Update last login
                user.LastLoginAt = DateTimeOffset.UtcNow;
                await SaveUserChangesAsync(context, dbContext);
            }

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

    private static async Task SaveUserChangesAsync(HttpContext context, ZootactDbContext dbContext)
    {
        await dbContext.SaveChangesAsync(context.RequestAborted);
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

        return candidate;
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
                return sanitized[..Math.Min(20, sanitized.Length)];
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
                return sanitized[..Math.Min(20, sanitized.Length)];
            }
        }

        // Fallback to random username
        return $"user_{Guid.NewGuid().ToString()[..8]}";
    }
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
