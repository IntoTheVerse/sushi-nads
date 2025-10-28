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

    [Header("UI References - Game Scene Only")]
    [SerializeField] private TMP_Text sessionStatusText; // Optional: show "Session #1 Active"
    [SerializeField] private GameObject scorePopup; // Score popup panel
    [SerializeField] private TMP_Text finalScoreText; // Final score display

    [Header("Session Data")]
    private int currentSessionId = 0;
    private bool isSessionActive = false;

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
        if (scorePopup != null) scorePopup.SetActive(false);
    }

    private void Update()
    {
        // DEBUG: Press ESC to manually end session (testing only)
        if (Input.GetKeyDown(KeyCode.Escape) && isSessionActive)
        {
            Debug.Log("🔑 ESC pressed - manually ending session...");
            _ = EndGameSession();
        }
    }

    private void OnDestroy()
    {
        // End session when app/game closes
        if (isSessionActive)
        {
            Debug.Log("🚪 App closing - ending active session...");
            _ = EndGameSession(); // Fire and forget
        }
    }

    private void OnApplicationQuit()
    {
        // Backup: end session when quitting
        if (isSessionActive)
        {
            Debug.Log("🚪 Quitting - ending active session...");
            _ = EndGameSession(); // Fire and forget
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
                
                Debug.Log($"✅ Blockchain session started!");
                Debug.Log($"   Session ID: {currentSessionId}");
                Debug.Log($"   Energy Remaining: {response.energyRemaining}");
                Debug.Log($"   Tx Hash: {response.txHash}");
                
                UpdateSessionStatus($"Session #{currentSessionId} Active", Color.green);
                
                // Load gameplay scene - KitchenGameManager will handle timer
                Debug.Log("🎮 Loading gameplay scene...");
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

        int maxRetries = 2;
        int retryCount = 0;

        while (retryCount <= maxRetries)
        {
            try
            {
                Debug.Log($"🏁 Ending blockchain session {currentSessionId}... (Attempt {retryCount + 1}/{maxRetries + 1})");
                UpdateSessionStatus("⏳ Computing score on-chain... Please wait", Color.yellow);
                
                // Call backend API with longer timeout
                var response = await GameAPIClient.Instance.EndSession(currentSessionId);
                
                if (response.success)
                {
                    isSessionActive = false;
                    
                    Debug.Log($"✅ Session ended!");
                    Debug.Log($"   Final Score: {response.finalScore}");
                    Debug.Log($"   Tx Hash: {response.txHash}");
                    
                    // Show score popup
                    ShowScorePopup(response.finalScore, response.txHash);
                    
                    UpdateSessionStatus("Session complete!", Color.green);
                    return; // Success!
                }
                else
                {
                    UpdateSessionStatus("Failed to end session", Color.red);
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
                        
                        // Show a simpler message
                        ShowScorePopup(0, ""); // Show popup without score (user can check later)
                        return;
                    }
                }
                else
                {
                    // Non-timeout error
                    Debug.LogError($"❌ End session error: {e.Message}");
                    UpdateSessionStatus($"❌ Error: {e.Message}", Color.red);
                    return;
                }
            }
        }
    }

    private void ShowScorePopup(int score, string txHash)
    {
        if (scorePopup != null)
        {
            scorePopup.SetActive(true);
            
            if (finalScoreText != null)
            {
                if (score > 0 && !string.IsNullOrEmpty(txHash))
                {
                    // Normal case - we got the score
                    finalScoreText.text = $"🎉 Game Over!\n\nFinal Score: {score}\n\n🔗 View on Basescan";
                }
                else
                {
                    // Timeout case - session ended but we didn't get score
                    finalScoreText.text = $"⏰ Game Over!\n\nScore computed on-chain\n(Check your profile for final score)";
                }
            }
            
            // Open transaction on Basescan if we have it
            if (!string.IsNullOrEmpty(txHash))
            {
                string basescanUrl = $"https://sepolia.basescan.org/tx/{txHash}";
                Debug.Log($"🔗 Score transaction: {basescanUrl}");
                Application.OpenURL(basescanUrl);
            }
        }
    }

    private void UpdateSessionStatus(string message, Color color)
    {
        if (sessionStatusText != null)
        {
            sessionStatusText.text = message;
            sessionStatusText.color = color;
        }
        
        Debug.Log($"[SessionManager] {message}");
    }

    // ============ Public API ============

    public bool IsSessionActive() => isSessionActive;
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

