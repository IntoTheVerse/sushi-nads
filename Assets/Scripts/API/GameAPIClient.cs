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

    private enum ApiEnvironment
    {
        Local,
        Hosted,
        Custom
    }

    [Header("API Configuration")]
    [SerializeField] private ApiEnvironment apiEnvironment = ApiEnvironment.Local;
    [SerializeField] private string localBaseUrl = "http://localhost:8081";
    [SerializeField] private string hostedBaseUrl = "https://api-sushinads.cryptara.world";
    [SerializeField] private string customBaseUrl = string.Empty;
    [SerializeField] private string baseUrl = "https://api-sushinads.cryptara.world";

    private string cachedWalletAddress = string.Empty;

    [Header("Rate Limiting")]
    [SerializeField] private int requestsPerMinuteBudget = 90;
    [SerializeField] private int maxRequestAttempts = 4;

    private readonly Queue<DateTime> requestTimestamps = new Queue<DateTime>();
    private readonly object rateLimitLock = new object();

    // Cached network configuration
    private NetworkConfigResponse cachedNetworkConfig;
    private bool networkConfigFetched = false;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        ApplySelectedApiEnvironment();
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void ApplySelectedApiEnvironment()
    {
        switch (apiEnvironment)
        {
            case ApiEnvironment.Local:
                baseUrl = localBaseUrl;
                break;
            case ApiEnvironment.Hosted:
                baseUrl = hostedBaseUrl;
                break;
            case ApiEnvironment.Custom:
                if (!string.IsNullOrWhiteSpace(customBaseUrl))
                {
                    baseUrl = customBaseUrl;
                }
                else
                {
                    baseUrl = hostedBaseUrl;
                    Debug.LogWarning("⚠️ API environment is Custom but customBaseUrl is empty. Falling back to hostedBaseUrl.");
                }
                break;
            default:
                baseUrl = hostedBaseUrl;
                break;
        }

        baseUrl = baseUrl.TrimEnd('/');
        Debug.Log($"🌐 API environment: {apiEnvironment} | Base URL: {baseUrl}");
    }
    public async Task<LeaderboardResponse> GetTopLeaderboardAsync(int limit = 25)
    {
        string url = $"{baseUrl}/api/leaderboard/top?limit={limit}";
        using (UnityWebRequest request = UnityWebRequest.Get(url))
        {
            request.timeout = 15;
            request.SetRequestHeader("Content-Type", "application/json");

            var operation = request.SendWebRequest();
            while (!operation.isDone)
            {
                await Task.Yield();
            }

            if (request.result == UnityWebRequest.Result.Success)
            {
                string responseText = request.downloadHandler.text;
                Debug.Log($"✅ Public leaderboard response: {responseText}");
                return JsonConvert.DeserializeObject<LeaderboardResponse>(responseText);
            }

            string errorText = request.downloadHandler?.text ?? request.error;
            Debug.LogError($"❌ Public leaderboard error: {errorText}");
            throw new Exception($"Public leaderboard request failed: {errorText}");
        }
    }
    // ============ Helper Methods ============

    private string GetWalletAddress()
    {
        try
        {
            var wallet = PrivyManager.Instance?.User?.EmbeddedWallets?.FirstOrDefault();
            if (wallet != null && !string.IsNullOrEmpty(wallet.Address))
            {
                cachedWalletAddress = wallet.Address;
                return cachedWalletAddress;
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning($"⚠️ Unable to fetch wallet address yet: {e.Message}");
        }

        return string.IsNullOrEmpty(cachedWalletAddress) ? string.Empty : cachedWalletAddress;
    }

    public string GetConnectedWalletAddress()
    {
        return GetWalletAddress();
    }

    public Task<string> WaitForWalletAddressAsync(float timeoutSeconds = 8f)
    {
        return EnsureWalletReadyAsync(timeoutSeconds);
    }

    private async Task<string> EnsureWalletReadyAsync(float timeoutSeconds = 8f, int pollIntervalMs = 200)
    {
        var timeout = TimeSpan.FromSeconds(timeoutSeconds);
        var startTime = DateTime.UtcNow;

        string walletAddress = GetWalletAddress();
        while (string.IsNullOrEmpty(walletAddress) && DateTime.UtcNow - startTime < timeout)
        {
            await Task.Delay(pollIntervalMs);
            walletAddress = GetWalletAddress();
        }

        return walletAddress;
    }

    public class SessionInactiveException : Exception
    {
        public SessionInactiveException(string message) : base(message) { }
    }

    private async Task<T> SendRequestAsync<T>(string endpoint, string method, object body = null, int timeoutSeconds = 30)
    {
        // HACKATHON MODE: Add wallet address to all requests (wait if Privy still initializing)
        string walletAddress = await EnsureWalletReadyAsync();

        if (string.IsNullOrEmpty(walletAddress))
        {
            Debug.LogError("❌ No wallet address - user must be logged in!");
            throw new Exception("User not authenticated with Privy");
        }

        int attempt = 0;

        while (attempt < Mathf.Max(1, maxRequestAttempts))
        {
            attempt++;
            await EnforceRateLimitAsync();

            string url = $"{baseUrl}{endpoint}";
            using (UnityWebRequest request = new UnityWebRequest(url, method))
            {
                request.timeout = timeoutSeconds;

                if (body != null)
                {
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
                    string separator = url.Contains("?") ? "&" : "?";
                    url += $"{separator}walletAddress={walletAddress}";
                    request.url = url;
                }

                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/json");

                var operation = request.SendWebRequest();
                while (!operation.isDone)
                {
                    await Task.Yield();
                }

                if (request.result == UnityWebRequest.Result.Success)
                {
                    string responseText = request.downloadHandler.text;
                    Debug.Log($"✅ API Response ({endpoint}): {responseText}");
                    return JsonConvert.DeserializeObject<T>(responseText);
                }

                string errorText = request.downloadHandler?.text ?? request.error;
                if (string.IsNullOrEmpty(errorText))
                {
                    errorText = $"{request.result} (code: {(int)request.responseCode})";
                }
                int statusCode = (int)request.responseCode;

                if (statusCode == 429 && attempt < maxRequestAttempts)
                {
                    int retrySeconds = ExtractRetryDelaySeconds(request, errorText);
                    Debug.LogWarning($"⏳ Rate limited ({endpoint}). Retrying in {retrySeconds}s (attempt {attempt}/{maxRequestAttempts})");
                    await Task.Delay(TimeSpan.FromSeconds(Mathf.Max(1, retrySeconds)));
                    continue;
                }

                if (IsUnsupportedProfileStatsResponse(endpoint, statusCode, errorText))
                {
                    // Expected on networks/contracts that do not expose profile stats yet.
                    Debug.Log($"ℹ️ Profile stats unavailable on current network ({endpoint}): {errorText}");
                    throw new Exception(errorText);
                }

                if (IsHtml502BadGatewayResponse(statusCode, errorText))
                {
                    Debug.LogError($"❌ API Error ({endpoint}): 502 Bad Gateway - the hosted backend is down or misconfigured.");
                    Debug.LogError($"   baseUrl: {baseUrl}");
                    Debug.LogError($"   Check on the server: pm2 list, pm2 logs sushi-bonks-api. Ensure Nginx proxy_pass port matches backend PORT.");
                    throw new Exception($"502 Bad Gateway. Hosted backend at {baseUrl} is not responding. Server admin must check backend and Nginx.");
                }

                if (attempt < maxRequestAttempts && IsTransientRequestError(request.result, statusCode))
                {
                    int backoffSeconds = Mathf.Clamp(attempt * 2, 1, 10);
                    Debug.LogWarning($"⚠️ API error ({endpoint}): {errorText}. Retrying in {backoffSeconds}s (attempt {attempt}/{maxRequestAttempts})");
                    await Task.Delay(TimeSpan.FromSeconds(backoffSeconds));
                    continue;
                }

                if (IsSessionInactiveResponse(endpoint, statusCode, errorText))
                {
                    Debug.Log($"⚠️ Session not active for {endpoint}. Skipping request.");
                    throw new SessionInactiveException(errorText ?? "Session not active");
                }

                if (IsHtml404Response(errorText))
                {
                    Debug.LogError($"❌ API Error ({endpoint}): Got HTML 404 instead of JSON. You may be hitting the wrong server!");
                    Debug.LogError($"   Current baseUrl: {baseUrl}");
                    Debug.LogError($"   For local dev: Set Api Environment = Local. If using pm2: Local Base Url = http://localhost:8081");
                    Debug.LogError($"   Ensure sushi-bonks backend is running (e.g. pm2 start sushi-bonks-api)");
                    throw new Exception($"Wrong server (HTML 404). Check GameAPIClient baseUrl matches your backend port.");
                }

                if (request.result == UnityWebRequest.Result.ConnectionError && string.IsNullOrEmpty(request.downloadHandler?.text))
                {
                    Debug.LogError($"❌ API Error ({endpoint}): Connection failed - {errorText}");
                    Debug.LogError($"   baseUrl: {baseUrl}");
                    Debug.LogError($"   If on Android device/emulator: localhost = the device. Use 10.0.2.2:8081 (emulator) or your PC IP (e.g. 192.168.1.x:8081) in Local Base Url.");
                    Debug.LogError($"   Ensure backend is running: pm2 start ecosystem.config.cjs");
                    throw new Exception($"Connection failed to {baseUrl}. Check backend is running and baseUrl is correct for your platform.");
                }

                Debug.LogError($"❌ API Error ({endpoint}): {errorText}");
                throw new Exception($"API request failed: {errorText}");
            }
        }

        throw new Exception($"API request exceeded retry budget for {endpoint}");
    }

    private async Task EnforceRateLimitAsync()
    {
        if (requestsPerMinuteBudget <= 0)
        {
            return;
        }

        while (true)
        {
            double waitSeconds;
            lock (rateLimitLock)
            {
                var now = DateTime.UtcNow;
                while (requestTimestamps.Count > 0 && (now - requestTimestamps.Peek()).TotalSeconds >= 60d)
                {
                    requestTimestamps.Dequeue();
                }

                if (requestTimestamps.Count < requestsPerMinuteBudget)
                {
                    requestTimestamps.Enqueue(now);
                    return;
                }

                var oldest = requestTimestamps.Peek();
                waitSeconds = Math.Max(0.05, 60d - (now - oldest).TotalSeconds);
            }

            await Task.Delay(TimeSpan.FromSeconds(waitSeconds));
        }
    }

    private int ExtractRetryDelaySeconds(UnityWebRequest request, string errorText)
    {
        const int defaultDelay = 5;

        try
        {
            string retryAfterHeader = request.GetResponseHeader("Retry-After");
            if (!string.IsNullOrEmpty(retryAfterHeader) && int.TryParse(retryAfterHeader, out int headerDelay))
            {
                return Mathf.Max(1, headerDelay);
            }

            if (!string.IsNullOrEmpty(errorText))
            {
                var parsed = JsonConvert.DeserializeObject<Dictionary<string, object>>(errorText);
                if (parsed != null && parsed.TryGetValue("retryAfter", out object retryField))
                {
                    if (retryField is long longVal)
                    {
                        return Mathf.Max(1, (int)longVal);
                    }
                    if (retryField is int intVal)
                    {
                        return Mathf.Max(1, intVal);
                    }
                    if (int.TryParse(retryField.ToString(), out int parsedDelay))
                    {
                        return Mathf.Max(1, parsedDelay);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"⚠️ Failed to parse retry delay: {ex.Message}");
        }

        return defaultDelay;
    }

    private bool IsTransientRequestError(UnityWebRequest.Result result, int statusCode)
    {
        if (result == UnityWebRequest.Result.ConnectionError ||
            result == UnityWebRequest.Result.DataProcessingError)
        {
            return true;
        }

        // 429 is already handled with Retry-After above.
        if (statusCode == 408 || statusCode == 425 || statusCode >= 500)
        {
            return true;
        }

        return false;
    }

    private bool IsUnsupportedProfileStatsResponse(string endpoint, int statusCode, string errorText)
    {
        if (string.IsNullOrEmpty(endpoint) || string.IsNullOrEmpty(errorText))
        {
            return false;
        }

        if (!endpoint.Equals("/api/chef/profile", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (statusCode != 404 && statusCode != 400)
        {
            return false;
        }

        return errorText.IndexOf("No on-chain stats available", StringComparison.OrdinalIgnoreCase) >= 0 ||
               errorText.IndexOf("does not support profile/statistics queries yet", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private bool IsHtml502BadGatewayResponse(int statusCode, string errorText)
    {
        if (statusCode != 502 && statusCode != 503) return false;
        if (string.IsNullOrEmpty(errorText)) return true; // 502/503 with no body
        var trimmed = errorText.TrimStart();
        return trimmed.StartsWith("<!DOCTYPE", StringComparison.OrdinalIgnoreCase) ||
               trimmed.StartsWith("<html", StringComparison.OrdinalIgnoreCase);
    }

    private bool IsHtml404Response(string errorText)
    {
        if (string.IsNullOrEmpty(errorText)) return false;
        var trimmed = errorText.TrimStart();
        return (trimmed.StartsWith("<!DOCTYPE", StringComparison.OrdinalIgnoreCase) ||
                trimmed.StartsWith("<html", StringComparison.OrdinalIgnoreCase)) &&
               (errorText.IndexOf("404", StringComparison.OrdinalIgnoreCase) >= 0 ||
                errorText.IndexOf("page could not be found", StringComparison.OrdinalIgnoreCase) >= 0);
    }

    private bool IsSessionInactiveResponse(string endpoint, int statusCode, string errorText)
    {
        if (string.IsNullOrEmpty(endpoint) || string.IsNullOrEmpty(errorText))
        {
            return false;
        }

        if (!endpoint.Contains("/gameplay/", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (statusCode < 400)
        {
            return false;
        }

        return errorText.IndexOf("session not active", StringComparison.OrdinalIgnoreCase) >= 0;
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

    public async Task<RefillEnergyResponse> RefillEnergyWithUSDC()
    {
        return await SendRequestAsync<RefillEnergyResponse>("/api/chef/refill-energy-usdc", "POST");
    }

    // ============ Session APIs ============

    public async Task<StartSessionResponse> StartSession()
    {
        Debug.Log("🎮 Starting game session...");
        return await SendRequestAsync<StartSessionResponse>("/api/session/start", "POST");
    }

    public async Task<EndSessionResponse> EndSession(int sessionId, int moveCount = 0)
    {
        Debug.Log($"🏁 Ending session {sessionId} (local move count: {moveCount})...");
        
        var body = new { sessionId, moveCount };
        // Longer timeout for session end (score computation can be slow)
        return await SendRequestAsync<EndSessionResponse>("/api/session/end", "POST", body, timeoutSeconds: 60);
    }

    public async Task<CurrentSessionResponse> GetCurrentSession()
    {
        return await SendRequestAsync<CurrentSessionResponse>("/api/session/current", "GET");
    }

    public async Task<HeartbeatResponse> HeartbeatSession(int sessionId)
    {
        var body = new { sessionId };
        return await SendRequestAsync<HeartbeatResponse>("/api/session/heartbeat", "POST", body, timeoutSeconds: 15);
    }

    public async Task<ForceEndSessionResponse> ForceEndSession(int sessionId, int moveCount = 0)
    {
        var body = new { sessionId, moveCount };
        return await SendRequestAsync<ForceEndSessionResponse>("/api/session/force-end", "POST", body, timeoutSeconds: 30);
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

    // ============ Config APIs ============

    /// <summary>
    /// Get network configuration from backend (includes explorer URL)
    /// Caches the result to avoid repeated requests
    /// </summary>
    public async Task<NetworkConfigResponse> GetNetworkConfig()
    {
        if (networkConfigFetched && cachedNetworkConfig != null)
        {
            return cachedNetworkConfig;
        }

        try
        {
            // This endpoint doesn't require authentication, so we don't need wallet address
            string url = $"{baseUrl}/api/config/network";
            using (UnityWebRequest request = UnityWebRequest.Get(url))
            {
                request.timeout = 10;
                request.SetRequestHeader("Content-Type", "application/json");
                
                var operation = request.SendWebRequest();
                while (!operation.isDone)
                {
                    await Task.Yield();
                }
                
                if (request.result == UnityWebRequest.Result.Success)
                {
                    string responseText = request.downloadHandler.text;
                    cachedNetworkConfig = JsonConvert.DeserializeObject<NetworkConfigResponse>(responseText);
                    networkConfigFetched = true;
                    Debug.Log($"✅ Network config loaded: {cachedNetworkConfig.network.name} ({cachedNetworkConfig.network.explorerUrl})");
                    return cachedNetworkConfig;
                }
                else
                {
                    Debug.LogWarning($"⚠️ Failed to fetch network config, using default Basescan URL");
                    // Return default Base Sepolia config as fallback
                    return new NetworkConfigResponse
                    {
                        success = false,
                        network = new NetworkInfo
                        {
                            explorerUrl = "https://sepolia.basescan.org"
                        }
                    };
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning($"⚠️ Error fetching network config: {e.Message}, using default Basescan URL");
            // Return default Base Sepolia config as fallback
            return new NetworkConfigResponse
            {
                success = false,
                network = new NetworkInfo
                {
                    explorerUrl = "https://sepolia.basescan.org"
                }
            };
        }
    }

    /// <summary>
    /// Get the explorer URL for the current network
    /// </summary>
    public async Task<string> GetExplorerUrl()
    {
        var config = await GetNetworkConfig();
        return config?.network?.explorerUrl ?? "https://sepolia.basescan.org";
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
    public PersonalRecords personalRecords;
    public RecentSession[] recentSessions;
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
public class PersonalRecords
{
    public int highestScore;
    public int bestCombo;
    public int fastestTime; // seconds
    public int currentRank;
}

[Serializable]
public class RecentSession
{
    public int sessionId;
    public int score;
    public long timestamp; // unix ms
}

[Serializable]
public class RefillEnergyResponse
{
    public bool success;
    public int newEnergy;
    public string txHash;
    public string message;
    public string usdcPaid; // Amount of USDC paid (e.g., "0.1")
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
    public SessionStats sessionStats;
    public bool gasSponsored;
}

[Serializable]
public class SessionStats
{
    public int moves;
    public int recipesDelivered;
    public int recipesDiscarded;
    public int wellCooked;
    public int overCooked;
    public int recipesSpawned;
    public int duration;
    public int efficiency;
    public int combo;
    public int currentStreak;
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
public class HeartbeatResponse
{
    public bool success;
    public int sessionId;
    public long heartbeatAt;
}

[Serializable]
public class ForceEndSessionResponse
{
    public bool success;
    public int sessionId;
    public int finalScore;
    public string txHash;
    public string message;
}

[Serializable]
public class BasicResponse
{
    public bool success;
    public string txHash;
    public bool gasSponsored;
}

/*[Serializable]
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
    public int sessionId;
    public long lastUpdated;
}

[Serializable]
public class PlayerRankResponse
{
    public int rank;
    public int score;
    public int totalPlayers;
    public int season;
    public int sessionId;
    public long lastUpdated;
}*/

[Serializable]
public class NetworkConfigResponse
{
    public bool success;
    public NetworkInfo network;
}

[Serializable]
public class NetworkInfo
{
    public string key;
    public string name;
    public int chainId;
    public string explorerUrl;
    public ContractAddresses contracts;
}

[Serializable]
public class ContractAddresses
{
    public string chefNFT;
    public string gameCore;
    public string leaderboard;
}

