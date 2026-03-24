namespace Zootact.Infrastructure.Data.Entities;

/// <summary>
/// Stores post-game Smart Replay and anti-cheat analysis for a completed match.
/// </summary>
public sealed class MatchAnalysisEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid MatchId { get; set; }

    public string Status { get; set; } = "Pending";

    public string? AnalysisJson { get; set; }

    public string? AntiCheatJson { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? UpdatedAt { get; set; }

    public MatchEntity Match { get; set; } = null!;
}
