namespace Zootact.Core.Domain;

/// <summary>
/// Represents a private invite-only lobby for a head-to-head match.
/// </summary>
public sealed class PrivateLobby
{
    public required Guid LobbyId { get; init; }
    public required Guid HostUserId { get; init; }
    public Guid? GuestUserId { get; set; }
    public required TimeControlPreset Preset { get; init; }
    public bool HostReady { get; set; }
    public bool GuestReady { get; set; }
    public bool CountdownActive { get; set; }
    public DateTimeOffset? CountdownEndAt { get; set; }
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public bool IsParticipant(Guid userId) => userId == HostUserId || userId == GuestUserId;

    public bool CanStart => GuestUserId.HasValue && GuestReady && !CountdownActive;

    public void Touch() => UpdatedAt = DateTimeOffset.UtcNow;

    public static PrivateLobby Create(Guid hostUserId, TimeControlPreset preset)
    {
        return new PrivateLobby
        {
            LobbyId = Guid.NewGuid(),
            HostUserId = hostUserId,
            Preset = preset,
            HostReady = false,
            GuestReady = false,
            CountdownActive = false,
            CountdownEndAt = null,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
    }
}
