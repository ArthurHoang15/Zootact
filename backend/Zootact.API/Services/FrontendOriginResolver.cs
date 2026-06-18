namespace Zootact.API.Services;

public static class FrontendOriginResolver
{
    public static string[] Resolve(IConfiguration configuration)
    {
        var explicitOrigins = configuration["Frontend:AllowedOrigins"];
        if (!string.IsNullOrWhiteSpace(explicitOrigins))
        {
            return explicitOrigins
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(origin => !string.IsNullOrWhiteSpace(origin))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        var legacyOrigin = configuration["Frontend:Url"];
        if (!string.IsNullOrWhiteSpace(legacyOrigin))
        {
            return [legacyOrigin.Trim()];
        }

        return [];
    }
}
