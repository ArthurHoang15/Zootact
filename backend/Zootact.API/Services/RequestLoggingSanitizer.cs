using Microsoft.AspNetCore.Http;

namespace Zootact.API.Services;

public static class RequestLoggingSanitizer
{
    public static string SanitizeRequestPath(HttpRequest request)
    {
        var path = string.Concat(request.PathBase.Value, request.Path.Value);
        var rawQuery = request.QueryString.Value;

        if (string.IsNullOrWhiteSpace(rawQuery))
        {
            return string.IsNullOrWhiteSpace(path) ? "/" : path;
        }

        var sanitizedParts = rawQuery.TrimStart('?')
            .Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Select(SanitizeQueryPart);

        return $"{(string.IsNullOrWhiteSpace(path) ? "/" : path)}?{string.Join("&", sanitizedParts)}";
    }

    private static string SanitizeQueryPart(string queryPart)
    {
        var separatorIndex = queryPart.IndexOf('=');
        if (separatorIndex < 0)
        {
            return IsSensitiveKey(queryPart) ? $"{queryPart}=%5Bredacted%5D" : queryPart;
        }

        var key = queryPart[..separatorIndex];
        if (!IsSensitiveKey(key))
        {
            return queryPart;
        }

        return $"{key}={Uri.EscapeDataString("[redacted]")}";
    }

    private static bool IsSensitiveKey(string key) =>
        string.Equals(key, "access_token", StringComparison.OrdinalIgnoreCase);
}
