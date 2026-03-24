using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace Zootact.Infrastructure.Services;

public sealed class AiServiceClient(HttpClient httpClient, ILogger<AiServiceClient> logger)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<string?> AnalyzeGameAsync(object request, CancellationToken cancellationToken = default)
    {
        return await PostAsync("/api/ai/analyze", request, cancellationToken);
    }

    public async Task<string?> AnalyzeMoveTimesAsync(object request, CancellationToken cancellationToken = default)
    {
        return await PostAsync("/api/anti-cheat/analyze", request, cancellationToken);
    }

    private async Task<string?> PostAsync(string path, object request, CancellationToken cancellationToken)
    {
        try
        {
            var response = await httpClient.PostAsJsonAsync(path, request, JsonOptions, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("AI service call to {Path} failed with status {StatusCode}", path, response.StatusCode);
                return null;
            }

            return await response.Content.ReadAsStringAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "AI service call to {Path} failed", path);
            return null;
        }
    }
}
