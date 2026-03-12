using System;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

/// <summary>
/// Manages game sessions - start, timer, and end with scoring
/// Integrates with backend API for on-chain session tracking
/// </summary>
public class SessionManager : MonoBehaviour
{
    public static SessionManager Instance { get; private set; }
    public GameOverUI _gameOverUI = null;

    [Header("UI References - Game Scene Only")]
    //[SerializeField] private TMP_Text sessionStatusText; // Optional: show "Session #1 Active"
    //[SerializeField] private GameObject scorePopup; // Score popup panel
    //[SerializeField] private TMP_Text finalScoreText; // Final score display

    [Header("Session Data")]
    private int currentSessionId = 0;
    private bool isSessionActive = false;
    private bool isEndingSession = false;  // Blocks new events (set after draining pending)
    private bool isEndingInProgress = false;  // Re-entrancy guard (set at start)
    [SerializeField] private int pendingEventsTimeoutMs = 15000;
    [SerializeField] private float heartbeatIntervalSeconds = 20f;
    private int heartbeatLoopVersion = 0;
    private const string PendingEndSessionKey = "pending_end_session_id";
    private const string PendingEndReasonKey = "pending_end_reason";
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        
        Instance = this;
        DontDestroyOnLoad(gameObject); // Persist across scenes
    }

    private void Start()
    {
        // Initially hide score popup
        //if (scorePopup != null) scorePopup.SetActive(false);
    }
    public void AssignGameOver(GameOverUI obj)
    {
        _gameOverUI = obj;
    }
    private void Update()
    {
        // DEBUG: Press ESC to manually end session (testing only)
        // if (Input.GetKeyDown(KeyCode.Escape) && isSessionActive)
        // {
        //     Debug.Log("🔑 ESC pressed - manually ending session...");
        //     _ = EndGameSession();
        // }
    }
    
    private void ClearPendingEndFlag()
    {
        PlayerPrefs.DeleteKey(PendingEndSessionKey);
        PlayerPrefs.DeleteKey(PendingEndReasonKey);
        PlayerPrefs.Save();
    }

    public async Task RecoverPendingEndIfAny()
    {
        int pendingSessionId = PlayerPrefs.GetInt(PendingEndSessionKey, -1);
        if (pendingSessionId <= 0) return;

        try
        {
            // Best: call your backend "end session" endpoint for pendingSessionId
            await GameAPIClient.Instance.EndSession(pendingSessionId, 0);
        }
        catch
        {
            // keep flag, retry next launch
            return;
        }

        ClearPendingEndFlag();
    }
 private async Task TryEndSessionFromLifecycle(string reason)
    {
        if (!isSessionActive || isEndingInProgress) return;

        // Save intent first in case process dies mid-request
        PlayerPrefs.SetInt(PendingEndSessionKey, currentSessionId);
        PlayerPrefs.SetString(PendingEndReasonKey, reason);
        PlayerPrefs.Save();

        try
        {
            await EndGameSession(); // your existing method
            ClearPendingEndFlag();
        }
        catch
        {
            // keep pending flag for recovery on next launch
        }
    }

#if !UNITY_EDITOR
    private async void OnApplicationFocus(bool hasFocus)
    {
        if (!hasFocus) await TryEndSessionFromLifecycle("APP_BACKGROUND");
    }
#endif
    private void OnDestroy()
    {
        // End session when app/game closes
        if (isSessionActive)
        {
            Debug.Log("🚪 App closing - ending active session...");
            _ = EndGameSession(); // Fire and forget
        }
    }

    private async void OnApplicationQuit()
    {
        // Backup: end session when quitting
        if (isSessionActive)
        {        await TryEndSessionFromLifecycle("APP_QUIT");

            Debug.Log("🚪 Quitting - ending active session...");
            _ = EndGameSession(); // Fire and forget
        }
    }

    private void StartHeartbeatLoop()
    {
        heartbeatLoopVersion++;
        int loopVersion = heartbeatLoopVersion;
        _ = RunHeartbeatLoopAsync(loopVersion);
    }

    private void StopHeartbeatLoop()
    {
        heartbeatLoopVersion++;
    }

    private async Task RunHeartbeatLoopAsync(int loopVersion)
    {
        float interval = Mathf.Max(5f, heartbeatIntervalSeconds);

        while (isSessionActive && loopVersion == heartbeatLoopVersion)
        {
            if (!isEndingSession && !isEndingInProgress && currentSessionId > 0)
            {
                try
                {
                    await GameAPIClient.Instance.HeartbeatSession(currentSessionId);
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"⚠️ Session heartbeat failed for {currentSessionId}: {ex.Message}");
                }
            }

            await Task.Delay(TimeSpan.FromSeconds(interval));
        }
    }

    // ============ Start Session ============
    // Called by ChefManager when PlayButton is clicked

    public async Task StartGameSession()
    {
        try
        {
            Debug.Log("🎮 Starting blockchain session...");
            UpdateSessionStatus("Starting session...", Color.yellow);
            
            // Call backend API
            var response = await GameAPIClient.Instance.StartSession();
            
            if (response.success)
            {
                currentSessionId = response.sessionId;
                isSessionActive = true;
                isEndingSession = false;
                StartHeartbeatLoop();

                // Reset local move count (off-chain) for this session
                if (GameplayEventTracker.Instance != null)
                {
                    GameplayEventTracker.Instance.ResetLocalMoveCount();
                }
                
                Debug.Log($"✅ Blockchain session started!");
                Debug.Log($"   Session ID: {currentSessionId}");
                Debug.Log($"   Energy Remaining: {response.energyRemaining}");
                Debug.Log($"   Tx Hash: {response.txHash}");
                
                UpdateSessionStatus($"Session #{currentSessionId} Active", Color.green);
                
                // Load gameplay scene - KitchenGameManager will handle timer
                Debug.Log("🎮 Loading gameplay scene...");
                GameSceneLaunchContext.SetNormalMode();
                SceneManager.LoadScene(2);
            }
            else
            {
                UpdateSessionStatus("Failed to start session", Color.red);
                throw new Exception("Failed to start session");
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"❌ Start session error: {e.Message}");
            
            // Check if it's an energy error
            if (e.Message.Contains("Insufficient energy"))
            {
                UpdateSessionStatus("❌ Not enough energy! Refill to play.", Color.red);
            }
            else
            {
                UpdateSessionStatus($"❌ Error: {e.Message}", Color.red);
            }
            
            throw; // Re-throw so ChefManager can handle
        }
    }

    // ============ End Session ============
    // Called by KitchenGameManager when game ends (timer hits 0 or manual end)

    public async Task EndGameSession()
    {
        if (!isSessionActive)
        {
            Debug.LogWarning("No active session to end");
            return;
        }

        if (isEndingInProgress)
        {
            Debug.LogWarning("Session end already in progress");
            return;
        }
        isEndingInProgress = true;

        // CRITICAL: Do NOT set isEndingSession yet - allow events that fire during the wait
        // (e.g. cooking completing in same frame as timer) to still be recorded.
        try
        {
            // Drain any pending gameplay events before ending the blockchain session
            if (GameplayEventTracker.Instance != null)
            {
                await GameplayEventTracker.Instance.WaitForPendingEventsAsync(pendingEventsTimeoutMs);
            }

            // Now block new events - we've drained pending and are about to call the API
            isEndingSession = true;

            // Show loading state immediately when game ends
            if (_gameOverUI != null)
            {
                _gameOverUI.ShowLoadingState();
            }

            int maxRetries = 2;
            int retryCount = 0;

            while (retryCount <= maxRetries)
            {
                try
                {
                    Debug.Log($"🏁 Ending blockchain session {currentSessionId}... (Attempt {retryCount + 1}/{maxRetries + 1})");
                    UpdateSessionStatus("⏳ Computing score on-chain... Please wait", Color.yellow);
                    
                    // Call backend API with longer timeout
                    int moveCount = GameplayEventTracker.Instance != null
                        ? GameplayEventTracker.Instance.GetLocalMoveCount()
                        : 0;
                    var response = await GameAPIClient.Instance.EndSession(currentSessionId, moveCount);
                    
                    if (response.success)
                    {
                        isSessionActive = false;
                        isEndingSession = false;
                        isEndingInProgress = false;
                        StopHeartbeatLoop();
                        
                        Debug.Log($"✅ Session ended!");
                        Debug.Log($"   Final Score: {response.finalScore}");
                        Debug.Log($"   Tx Hash: {response.txHash}");
                        
                        // Fetch detailed session stats from blockchain (optional, for accuracy)
                        // For now, use local stats and display score
                        ShowScorePopup(
                            response.finalScore, 
                            response.txHash, 
                            response.sessionStats ?? CreateFallbackStats()
                        );
                        
                        UpdateSessionStatus("Session complete!", Color.green);
                        return; // Success!
                    }
                    else
                    {
                        UpdateSessionStatus("Failed to end session", Color.red);
                        if (_gameOverUI != null)
                        {
                            _gameOverUI.SetScore(0);
                        }
                        return;
                    }
                }
                catch (Exception e)
                {
                    retryCount++;
                    
                    if (e.Message.Contains("timeout") || e.Message.Contains("TIMEOUT"))
                    {
                        if (retryCount <= maxRetries)
                        {
                            Debug.LogWarning($"⏰ Request timed out. Retrying... ({retryCount}/{maxRetries})");
                            UpdateSessionStatus($"⏳ Still processing... Retry {retryCount}/{maxRetries}", Color.yellow);
                            await Task.Delay(2000); // Wait 2s before retry
                            continue;
                        }
                        else
                        {
                            // All retries exhausted - session likely ended on backend anyway
                            Debug.LogWarning("⚠️ Request timed out but session may have ended successfully on backend");
                            UpdateSessionStatus("⚠️ Session ended (blockchain slow - check profile)", Color.yellow);
                            
                            isSessionActive = false; // Mark as inactive locally
                            isEndingSession = false;
                            isEndingInProgress = false;
                            StopHeartbeatLoop();
                            
                            // Show a simpler message (score will be 0, user can check later)
                            ShowScorePopup(0, "", CreateFallbackStats());
                            return;
                        }
                    }
                    else
                    {
                        // Non-timeout error
                        Debug.LogError($"❌ End session error: {e.Message}");
                        UpdateSessionStatus($"❌ Error: {e.Message}", Color.red);
                        if (_gameOverUI != null)
                        {
                            _gameOverUI.SetScore(0);
                        }
                        return;
                    }
                }
            }
        }
        finally
        {
            if (isEndingInProgress)
            {
                // Ensure we don't leave the session stuck in a "ending" state
                isEndingInProgress = false;
                isEndingSession = false;
            }
        }
    }

    private SessionStats CreateFallbackStats()
    {
        int delivered = DeliveryManager.Instance != null
            ? DeliveryManager.Instance.GetSuccessfulRecipesAmount()
            : 0;

        return new SessionStats
        {
            recipesDelivered = delivered
        };
    }

    private async void ShowScorePopup(int score, string txHash, SessionStats stats = null)
    {
        Debug.Log($"🎯 Displaying score: {score}");
        
        if (_gameOverUI != null)
        {
            if (!_gameOverUI.gameObject.activeInHierarchy)
            {
                _gameOverUI.gameObject.SetActive(true);
            }

            // Use the new DisplayScore method with animation
            var statsToUse = stats ?? CreateFallbackStats();
            _gameOverUI.DisplayScore(score, statsToUse);
        }
        else
        {
            Debug.LogWarning("⚠️ GameOverUI is null - cannot display score");
        }
        
        // Open transaction on explorer if we have it (after a short delay to let UI update)
        if (!string.IsNullOrEmpty(txHash))
        {
            // Wait a bit before opening browser so UI can update
            await Task.Delay(1000);
            
            try
            {
                string explorerUrl = await GameAPIClient.Instance.GetExplorerUrl();
                string explorerName = explorerUrl.Contains("basescan") ? "Basescan" : "MonVision";
                string txUrl = $"{explorerUrl}/tx/{txHash}";
                Debug.Log($"🔗 Score transaction on {explorerName}: {txUrl}");
                Application.OpenURL(txUrl);
            }
            catch (Exception e)
            {
                // Fallback to Basescan if network config fetch fails
                Debug.LogWarning($"⚠️ Failed to get explorer URL, using default Basescan: {e.Message}");
            string basescanUrl = $"https://sepolia.basescan.org/tx/{txHash}";
            Debug.Log($"🔗 Score transaction: {basescanUrl}");
            Application.OpenURL(basescanUrl);
            }
        }
    }

    private void UpdateSessionStatus(string message, Color color)
    {
        /*if (sessionStatusText != null)
        {
            sessionStatusText.text = message;
            sessionStatusText.color = color;
        }
        */
        Debug.Log($"[SessionManager] {message}");
    }

    // ============ Public API ============

    public bool IsSessionActive() => isSessionActive;
    public bool CanRecordGameplayEvents() => isSessionActive && !isEndingSession;
    public int GetCurrentSessionId() => currentSessionId;
    
    /// <summary>
    /// Call this from KitchenGameManager when game timer hits 0
    /// </summary>
    public async void OnGameTimeUp()
    {
        Debug.Log("⏰ Game time up! Ending blockchain session...");
        await EndGameSession();
    }
}

