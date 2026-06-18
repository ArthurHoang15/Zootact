namespace Zootact.Core.Domain;

public enum MatchMode
{
    Rated,
    Friendly
}

public static class MatchTypeMetadata
{
    private const string FriendlyPrefix = "Friendly:";
    private const string UntimedName = "Untimed";

    public static string EncodeTimeControl(TimeControlPreset preset, MatchMode matchMode) =>
        matchMode == MatchMode.Friendly
            ? $"{FriendlyPrefix}{preset}"
            : preset.ToString();

    public static string EncodeTimeControl(TimeControl timeControl, MatchMode matchMode) =>
        EncodeTimeControl(timeControl.Preset, matchMode);

    public static string EncodeFriendlyUntimed() => $"{FriendlyPrefix}{UntimedName}";

    public static MatchMode Parse(string storedTimeControl) =>
        storedTimeControl.StartsWith(FriendlyPrefix, StringComparison.OrdinalIgnoreCase)
            ? MatchMode.Friendly
            : MatchMode.Rated;

    public static string GetDisplayTimeControl(string storedTimeControl) =>
        Parse(storedTimeControl) == MatchMode.Friendly
            ? storedTimeControl[FriendlyPrefix.Length..]
            : storedTimeControl;
}
