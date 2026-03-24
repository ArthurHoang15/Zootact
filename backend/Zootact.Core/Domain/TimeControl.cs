namespace Zootact.Core.Domain;

/// <summary>
/// Time control presets for matches.
/// </summary>
public enum TimeControlPreset
{
    Blitz,      // 3 minutes
    Rapid,      // 10 minutes
    Classical   // 30 minutes
}

/// <summary>
/// Represents the time control settings and remaining time for a match.
/// </summary>
/// <param name="Preset">The time control preset.</param>
/// <param name="InitialTimeMs">Initial time in milliseconds.</param>
/// <param name="IncrementMs">Time increment per move in milliseconds.</param>
/// <param name="BlueTimeRemainingMs">Blue player's remaining time.</param>
/// <param name="RedTimeRemainingMs">Red player's remaining time.</param>
/// <param name="LastMoveTimestamp">Timestamp of the last move for calculating elapsed time.</param>
public record TimeControl(
    TimeControlPreset Preset,
    int InitialTimeMs,
    int IncrementMs,
    long BlueTimeRemainingMs,
    long RedTimeRemainingMs,
    DateTimeOffset LastMoveTimestamp)
{
    /// <summary>
    /// Creates a Blitz time control (3 min + 0 sec).
    /// </summary>
    public static TimeControl Blitz() => new(
        TimeControlPreset.Blitz,
        InitialTimeMs: 3 * 60 * 1000,
        IncrementMs: 0,
        BlueTimeRemainingMs: 3 * 60 * 1000,
        RedTimeRemainingMs: 3 * 60 * 1000,
        DateTimeOffset.UtcNow);
    
    /// <summary>
    /// Creates a Rapid time control (10 min + 0 sec).
    /// </summary>
    public static TimeControl Rapid() => new(
        TimeControlPreset.Rapid,
        InitialTimeMs: 10 * 60 * 1000,
        IncrementMs: 0,
        BlueTimeRemainingMs: 10 * 60 * 1000,
        RedTimeRemainingMs: 10 * 60 * 1000,
        DateTimeOffset.UtcNow);
    
    /// <summary>
    /// Creates a Classical time control (30 min + 0 sec).
    /// </summary>
    public static TimeControl Classical() => new(
        TimeControlPreset.Classical,
        InitialTimeMs: 30 * 60 * 1000,
        IncrementMs: 0,
        BlueTimeRemainingMs: 30 * 60 * 1000,
        RedTimeRemainingMs: 30 * 60 * 1000,
        DateTimeOffset.UtcNow);
    
    /// <summary>
    /// Creates a time control from a preset.
    /// </summary>
    public static TimeControl FromPreset(TimeControlPreset preset) => preset switch
    {
        TimeControlPreset.Blitz => Blitz(),
        TimeControlPreset.Rapid => Rapid(),
        TimeControlPreset.Classical => Classical(),
        _ => Blitz()
    };
    
    /// <summary>
    /// Deducts time for a player after their move.
    /// </summary>
    /// <param name="player">The player whose time to deduct.</param>
    /// <param name="elapsedMs">Elapsed time in milliseconds.</param>
    /// <returns>Updated time control with increment applied.</returns>
    public TimeControl DeductTime(Player player, long elapsedMs)
    {
        var blueTime = BlueTimeRemainingMs;
        var redTime = RedTimeRemainingMs;
        
        if (player == Player.Blue)
        {
            blueTime = Math.Max(0, blueTime - elapsedMs + IncrementMs);
        }
        else
        {
            redTime = Math.Max(0, redTime - elapsedMs + IncrementMs);
        }
        
        return this with
        {
            BlueTimeRemainingMs = blueTime,
            RedTimeRemainingMs = redTime,
            LastMoveTimestamp = DateTimeOffset.UtcNow
        };
    }
    
    /// <summary>
    /// Checks if a player has timed out.
    /// </summary>
    public bool IsTimeout(Player player) =>
        player == Player.Blue ? BlueTimeRemainingMs <= 0 : RedTimeRemainingMs <= 0;
    
    /// <summary>
    /// Gets remaining time for a player in milliseconds.
    /// </summary>
    public long GetRemainingTime(Player player) =>
        player == Player.Blue ? BlueTimeRemainingMs : RedTimeRemainingMs;

    /// <summary>
    /// Computes the effective remaining times at a specific moment without mutating state.
    /// This is used for reconnect/resync flows where no move has been made yet.
    /// </summary>
    public (long BlueTimeRemainingMs, long RedTimeRemainingMs) GetEffectiveRemainingTimes(
        Player currentTurn,
        DateTimeOffset now)
    {
        var elapsedMs = Math.Max(0, (long)(now - LastMoveTimestamp).TotalMilliseconds);
        var blueTime = BlueTimeRemainingMs;
        var redTime = RedTimeRemainingMs;

        if (currentTurn == Player.Blue)
        {
            blueTime = Math.Max(0, blueTime - elapsedMs);
        }
        else
        {
            redTime = Math.Max(0, redTime - elapsedMs);
        }

        return (blueTime, redTime);
    }

    /// <summary>
    /// Advances the active player's clock to a specific moment without applying increment.
    /// This is used by background timeout processing and reconnect synchronization.
    /// </summary>
    public TimeControl AdvanceTo(Player currentTurn, DateTimeOffset now)
    {
        var (blueTime, redTime) = GetEffectiveRemainingTimes(currentTurn, now);

        return this with
        {
            BlueTimeRemainingMs = blueTime,
            RedTimeRemainingMs = redTime,
            LastMoveTimestamp = now
        };
    }

    /// <summary>
    /// Shifts the last move timestamp forward to pause clock consumption for a period.
    /// </summary>
    public TimeControl PauseFor(TimeSpan duration)
    {
        if (duration <= TimeSpan.Zero)
        {
            return this;
        }

        return this with
        {
            LastMoveTimestamp = LastMoveTimestamp.Add(duration)
        };
    }
}
