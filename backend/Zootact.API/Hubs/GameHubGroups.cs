namespace Zootact.API.Hubs;

public static class GameHubGroups
{
    public static string Match(string matchId) => matchId;
    public static string Match(Guid matchId) => matchId.ToString();
    public static string Lobby(string lobbyId) => $"lobby:{lobbyId}";
    public static string Lobby(Guid lobbyId) => $"lobby:{lobbyId}";
}
