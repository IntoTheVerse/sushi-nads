using System;

[Serializable]
public class LeaderboardResponse
{
    public LeaderboardEntry[] rankings;
    public bool cached;
    public int season;
}

[Serializable]
public class LeaderboardEntry
{
    public int rank;
    public string player;      // wallet / address
    public string playerName;
    public int score;
    public int sessionId;
    public long lastUpdated;   // unix ms from API (1763725543000)
}

[Serializable]
public class PlayerRankResponse
{
    public int rank;
    public int totalPlayers;
    public int score;
    public int sessionId;
    public long lastUpdated;   // unix ms
}
