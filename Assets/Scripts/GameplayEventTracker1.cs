using System;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// Tracks gameplay events and records them to blockchain
/// Works with SessionManager to get current session ID
/// Fire-and-forget pattern: doesn't block gameplay if blockchain is slow
/// </summary>
public class GameplayEventTracker : MonoBehaviour
{
    public static GameplayEventTracker Instance { get; private set; }

    [Header("Debug Settings")]
    [SerializeField] private bool enableBlockchainRecording = true;
    [SerializeField] private bool verboseLogging = true;

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

    // ============ Public API ============

    /// <summary>
    /// Record a successful recipe delivery
    /// </summary>
    public async void RecordRecipeDelivery()
    {
        if (!enableBlockchainRecording) return;
        
        int sessionId = GetActiveSessionId();
        if (sessionId == 0) return;

        try
        {
            if (verboseLogging) Debug.Log($"🍱 Recording recipe delivery (Session {sessionId})...");
            
            await GameAPIClient.Instance.RecordRecipeDelivered(sessionId);
            
            if (verboseLogging) Debug.Log($"✅ Recipe delivery recorded on-chain");
        }
        catch (Exception e)
        {
            // Don't block gameplay - just log error
            Debug.LogWarning($"⚠️ Failed to record delivery (gameplay continues): {e.Message}");
        }
    }

    /// <summary>
    /// Record a recipe discard (plate thrown in trash)
    /// </summary>
    public async void RecordRecipeDiscard()
    {
        if (!enableBlockchainRecording) return;
        
        int sessionId = GetActiveSessionId();
        if (sessionId == 0) return;

        try
        {
            if (verboseLogging) Debug.Log($"🗑️ Recording recipe discard (Session {sessionId})...");
            
            await GameAPIClient.Instance.RecordRecipeDiscarded(sessionId);
            
            if (verboseLogging) Debug.Log($"✅ Recipe discard recorded on-chain");
        }
        catch (Exception e)
        {
            Debug.LogWarning($"⚠️ Failed to record discard (gameplay continues): {e.Message}");
        }
    }

    /// <summary>
    /// Record a perfectly cooked ingredient (fried state)
    /// </summary>
    public async void RecordWellCooked()
    {
        if (!enableBlockchainRecording) return;
        
        int sessionId = GetActiveSessionId();
        if (sessionId == 0) return;

        try
        {
            if (verboseLogging) Debug.Log($"🔥 Recording well-cooked ingredient (Session {sessionId})...");
            
            await GameAPIClient.Instance.RecordWellCooked(sessionId);
            
            if (verboseLogging) Debug.Log($"✅ Well-cooked recorded on-chain");
        }
        catch (Exception e)
        {
            Debug.LogWarning($"⚠️ Failed to record well-cooked (gameplay continues): {e.Message}");
        }
    }

    /// <summary>
    /// Record an overcooked ingredient (burned state)
    /// </summary>
    public async void RecordOverCooked()
    {
        if (!enableBlockchainRecording) return;
        
        int sessionId = GetActiveSessionId();
        if (sessionId == 0) return;

        try
        {
            if (verboseLogging) Debug.Log($"💀 Recording overcooked ingredient (Session {sessionId})...");
            
            await GameAPIClient.Instance.RecordOverCooked(sessionId);
            
            if (verboseLogging) Debug.Log($"✅ Overcooked recorded on-chain");
        }
        catch (Exception e)
        {
            Debug.LogWarning($"⚠️ Failed to record overcooked (gameplay continues): {e.Message}");
        }
    }

    /// <summary>
    /// Record a player move (optional - minor score impact)
    /// Moves are batched on backend to save gas (10 moves per batch)
    /// </summary>
    public async void RecordMove()
    {
        if (!enableBlockchainRecording) return;
        
        int sessionId = GetActiveSessionId();
        if (sessionId == 0) return;

        try
        {
            // Moves are batched on backend to save gas (no logging to avoid spam)
            await GameAPIClient.Instance.RecordMove(sessionId);
        }
        catch (Exception e)
        {
            // Silently fail for moves (too frequent to spam logs)
            // Only log if verbose logging is enabled
            if (verboseLogging) Debug.LogWarning($"⚠️ Failed to record move: {e.Message}");
        }
    }

    // ============ Helper Methods ============

    private int GetActiveSessionId()
    {
        if (SessionManager.Instance == null)
        {
            Debug.LogWarning("⚠️ SessionManager not found - cannot record event");
            return 0;
        }

        if (!SessionManager.Instance.IsSessionActive())
        {
            Debug.LogWarning("⚠️ No active session - cannot record event");
            return 0;
        }

        return SessionManager.Instance.GetCurrentSessionId();
    }

    // ============ Settings ============

    public void SetBlockchainRecording(bool enabled)
    {
        enableBlockchainRecording = enabled;
        Debug.Log($"📡 Blockchain recording: {(enabled ? "ENABLED" : "DISABLED")}");
    }

    public void SetVerboseLogging(bool enabled)
    {
        verboseLogging = enabled;
    }
}

