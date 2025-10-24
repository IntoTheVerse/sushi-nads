using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;
using Newtonsoft.Json;
using Privy;

/// <summary>
/// HTTP client for Sushi Bonks backend API
/// Handles all communication with Node.js backend
/// </summary>
public class GameAPIClient : MonoBehaviour
{
    public static GameAPIClient Instance { get; private set; }

    [Header("API Configuration")]
    [SerializeField] private string baseUrl = "http://localhost:3000";
    // For production: "https://api.sushibonks.com"

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    // ============ Helper Methods ============

    private string GetWalletAddress()
    {
        // Get wallet address from Privy
        var wallet = PrivyManager.Instance?.User?.EmbeddedWallets?.FirstOrDefault();
        return wallet?.Address ?? "";
    }

    private async Task<T> SendRequestAsync<T>(string endpoint, string method, object body = null)
    {
        string url = $"{baseUrl}{endpoint}";
        
        // HACKATHON MODE: Add wallet address to all requests
        string walletAddress = GetWalletAddress();
        
        if (string.IsNullOrEmpty(walletAddress))
        {
            Debug.LogError("❌ No wallet address - user must be logged in!");
            throw new Exception("User not authenticated with Privy");
        }
        
        using (UnityWebRequest request = new UnityWebRequest(url, method))
        {
            // Setup request body
            if (body != null)
            {
                // Add wallet address to body
                var bodyDict = JsonConvert.DeserializeObject<Dictionary<string, object>>(
                    JsonConvert.SerializeObject(body)
                );
                bodyDict["walletAddress"] = walletAddress;
                
                string jsonBody = JsonConvert.SerializeObject(bodyDict);
                byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonBody);
                request.uploadHandler = new UploadHandlerRaw(bodyRaw);
                
                Debug.Log($"📤 Request body: {jsonBody}");
            }
            else
            {
                // GET request - add wallet as query param
                url += $"?walletAddress={walletAddress}";
                request.url = url;
            }
            
            request.downloadHandler = new DownloadHandlerBuffer();
            
            // Add headers
            request.SetRequestHeader("Content-Type", "application/json");
            
            // Send request
            var operation = request.SendWebRequest();
            
            while (!operation.isDone)
            {
                await Task.Yield();
            }
            
            // Handle response
            if (request.result == UnityWebRequest.Result.Success)
            {
                string responseText = request.downloadHandler.text;
                Debug.Log($"✅ API Response ({endpoint}): {responseText}");
                
                return JsonConvert.DeserializeObject<T>(responseText);
            }
            else
            {
                string errorText = request.downloadHandler?.text ?? request.error;
                Debug.LogError($"❌ API Error ({endpoint}): {errorText}");
                throw new Exception($"API request failed: {errorText}");
            }
        }
    }

    // ============ Chef NFT APIs ============

    public async Task<MintChefResponse> MintChef(string playerName)
    {
        Debug.Log($"🎨 Minting chef: {playerName}");
        
        var body = new { playerName };
        return await SendRequestAsync<MintChefResponse>("/api/chef/mint", "POST", body);
    }

    public async Task<PlayerProfileResponse> GetProfile()
    {
        return await SendRequestAsync<PlayerProfileResponse>("/api/chef/profile", "GET");
    }

    public async Task<RefillEnergyResponse> RefillEnergy()
    {
        return await SendRequestAsync<RefillEnergyResponse>("/api/chef/refill-energy", "POST");
    }

    // ============ Session APIs ============

    public async Task<StartSessionResponse> StartSession()
    {
        Debug.Log("🎮 Starting game session...");
        return await SendRequestAsync<StartSessionResponse>("/api/session/start", "POST");
    }

    public async Task<EndSessionResponse> EndSession(int sessionId)
    {
        Debug.Log($"🏁 Ending session {sessionId}...");
        
        var body = new { sessionId };
        return await SendRequestAsync<EndSessionResponse>("/api/session/end", "POST", body);
    }

    public async Task<CurrentSessionResponse> GetCurrentSession()
    {
        return await SendRequestAsync<CurrentSessionResponse>("/api/session/current", "GET");
    }

    // ============ Gameplay APIs ============

    public async Task RecordMove(int sessionId)
    {
        var body = new { sessionId };
        await SendRequestAsync<BasicResponse>("/api/gameplay/move", "POST", body);
    }

    public async Task RecordRecipeDelivered(int sessionId)
    {
        Debug.Log("✅ Recipe delivered!");
        
        var body = new { sessionId };
        await SendRequestAsync<BasicResponse>("/api/gameplay/recipe-delivered", "POST", body);
    }

    public async Task RecordRecipeDiscarded(int sessionId)
    {
        var body = new { sessionId };
        await SendRequestAsync<BasicResponse>("/api/gameplay/recipe-discarded", "POST", body);
    }

    public async Task RecordWellCooked(int sessionId)
    {
        var body = new { sessionId };
        await SendRequestAsync<BasicResponse>("/api/gameplay/well-cooked", "POST", body);
    }

    public async Task RecordOverCooked(int sessionId)
    {
        var body = new { sessionId };
        await SendRequestAsync<BasicResponse>("/api/gameplay/over-cooked", "POST", body);
    }

    // ============ Leaderboard APIs ============

    public async Task<LeaderboardResponse> GetTopPlayers(int limit = 100)
    {
        return await SendRequestAsync<LeaderboardResponse>($"/api/leaderboard/top?limit={limit}", "GET");
    }

    public async Task<PlayerRankResponse> GetPlayerRank()
    {
        return await SendRequestAsync<PlayerRankResponse>("/api/leaderboard/player-rank", "GET");
    }
}

// ============ Response Models ============

[Serializable]
public class MintChefResponse
{
    public bool success;
    public string txHash;
    public int tokenId;
    public string name;
    public bool gasSponsored;
}

[Serializable]
public class PlayerProfileResponse
{
    public PlayerInfo player;
    public GlobalStats globalStats;
    public int currentSessionId;
}

[Serializable]
public class PlayerInfo
{
    public string address;
    public string name;
    public int tokenId;
    public int energy;
    public int maxEnergy;
}

[Serializable]
public class GlobalStats
{
    public int totalSessionsPlayed;
    public int totalMoves;
    public int totalRecipesSpawned;
    public int totalRecipesDelivered;
    public int totalRecipesDiscarded;
    public int totalWellCooked;
    public int totalOverCooked;
}

[Serializable]
public class RefillEnergyResponse
{
    public bool success;
    public int newEnergy;
    public string txHash;
}

[Serializable]
public class StartSessionResponse
{
    public bool success;
    public int sessionId;
    public string txHash;
    public long startTime;
    public int energyRemaining;
    public bool gasSponsored;
}

[Serializable]
public class EndSessionResponse
{
    public bool success;
    public int sessionId;
    public int finalScore;
    public string txHash;
    public bool gasSponsored;
}

[Serializable]
public class CurrentSessionResponse
{
    public bool hasActiveSession;
    public int sessionId;
    public long startTime;
    public int moveCount;
    public int recipesDelivered;
}

[Serializable]
public class BasicResponse
{
    public bool success;
    public string txHash;
    public bool gasSponsored;
}

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
    public string player;
    public string playerName;
    public int score;
}

[Serializable]
public class PlayerRankResponse
{
    public int rank;
    public int score;
    public int totalPlayers;
    public int season;
}

