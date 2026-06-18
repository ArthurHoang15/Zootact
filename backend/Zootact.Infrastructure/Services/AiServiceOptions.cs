namespace Zootact.Infrastructure.Services;

public sealed class AiServiceOptions
{
    public bool Enabled { get; set; } = true;
    public string BaseUrl { get; set; } = "http://localhost:8001";
}
