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

    // Tracks in-flight API calls so SessionManager can wait before ending a session
    private readonly object pendingLock = new object();
    private int pendingRequestCount = 0;

    // Local move count (off-chain) - sent to backend at session end for score computation
    // No persistence needed: session-scoped, reset when session starts
    private int localMoveCount = 0;
    private TaskCompletionSource<bool> pendingDrainedSignal = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

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
        await TrackAndSendEventAsync(
            "recipe delivery",
            onStart: () => verboseLogging ? "🍱 Recording recipe delivery" : null,
            onSuccess: () => verboseLogging ? "✅ Recipe delivery recorded on-chain" : null,
            sendAsync: sessionId => GameAPIClient.Instance.RecordRecipeDelivered(sessionId)
        );
    }

    /// <summary>
    /// Record a recipe discard (plate thrown in trash)
    /// </summary>
    public async void RecordRecipeDiscard()
    {
        Debug.Log("[GameplayEventTracker] 🗑️ RecordRecipeDiscard() called");
        await TrackAndSendEventAsync(
            "recipe discard",
            onStart: () => verboseLogging ? "🗑️ Recording recipe discard" : null,
            onSuccess: () => verboseLogging ? "✅ Recipe discard recorded on-chain" : null,
            sendAsync: sessionId => GameAPIClient.Instance.RecordRecipeDiscarded(sessionId)
        );
    }

    /// <summary>
    /// Record a perfectly cooked ingredient (fried state)
    /// </summary>
    public async void RecordWellCooked()
    {
        await TrackAndSendEventAsync(
            "well-cooked ingredient",
            onStart: () => verboseLogging ? "🔥 Recording well-cooked ingredient" : null,
            onSuccess: () => verboseLogging ? "✅ Well-cooked recorded on-chain" : null,
            sendAsync: sessionId => GameAPIClient.Instance.RecordWellCooked(sessionId)
        );
    }

    /// <summary>
    /// Record an overcooked ingredient (burned state)
    /// </summary>
    public async void RecordOverCooked()
    {
        await TrackAndSendEventAsync(
            "overcooked ingredient",
            onStart: () => verboseLogging ? "💀 Recording overcooked ingredient" : null,
            onSuccess: () => verboseLogging ? "✅ Overcooked recorded on-chain" : null,
            sendAsync: sessionId => GameAPIClient.Instance.RecordOverCooked(sessionId)
        );
    }

    /// <summary>
    /// Record a player move locally (off-chain).
    /// Move count is kept in memory and sent to backend at session end for score computation.
    /// This avoids 70+ API calls per session and ensures scoring events (recipe-delivered, well-cooked)
    /// are reliably recorded on-chain.
    /// </summary>
    public void RecordMove()
    {
        if (!enableBlockchainRecording) return;
        if (!CanRecordGameplayEvents()) return;
        localMoveCount++;
    }

    /// <summary>
    /// Reset local move count. Call when a new session starts.
    /// </summary>
    public void ResetLocalMoveCount()
    {
        localMoveCount = 0;
        if (verboseLogging) Debug.Log("[GameplayEventTracker] 📊 Local move count reset to 0");
    }

    /// <summary>
    /// Get the current local move count for the active session.
    /// Pass this to EndSession so the backend can use it for score computation.
    /// </summary>
    public int GetLocalMoveCount() => localMoveCount;

    private bool CanRecordGameplayEvents()
    {
        return SessionManager.Instance != null && SessionManager.Instance.CanRecordGameplayEvents();
    }

    // ============ Helper Methods ============

    private int GetActiveSessionId()
    {
        if (SessionManager.Instance == null)
        {
            Debug.LogWarning("⚠️ SessionManager not found - cannot record event");
            return 0;
        }

        if (!SessionManager.Instance.CanRecordGameplayEvents())
        {
            Debug.LogWarning("⚠️ No active session - cannot record event (CanRecordGameplayEvents returned false)");
            return 0;
        }

        int sessionId = SessionManager.Instance.GetCurrentSessionId();
        if (sessionId == 0)
        {
            Debug.LogWarning("⚠️ Session ID is 0 - cannot record event");
        }
        return sessionId;
    }

    /// <summary>
    /// Wait until all pending gameplay events have been sent or the timeout elapses.
    /// SessionManager calls this before ending the session to avoid dropping events.
    /// </summary>
    public async Task WaitForPendingEventsAsync(int timeoutMilliseconds = 5000)
    {
        Task waitTask = null;
        int pending;

        lock (pendingLock)
        {
            pending = pendingRequestCount;
            if (pendingRequestCount > 0)
            {
                waitTask = pendingDrainedSignal.Task;
            }
        }

        if (waitTask == null)
        {
            if (verboseLogging) Debug.Log("🛜 No pending gameplay events to flush.");
            return;
        }

        if (verboseLogging) Debug.Log($"🛜 Waiting for {pending} gameplay events to flush before ending session...");

        Task completed = await Task.WhenAny(waitTask, Task.Delay(timeoutMilliseconds));
        if (completed != waitTask)
        {
            Debug.LogWarning($"⚠️ Timeout waiting for gameplay events to finish sending. {pending} still pending after {timeoutMilliseconds} ms. Proceeding anyway.");
        }
        else
        {
            await waitTask; // ensure exceptions propagate if any
            if (verboseLogging) Debug.Log("✅ Gameplay events flushed.");
        }
    }

    private async Task TrackAndSendEventAsync(string eventName, Func<string> onStart, Func<string> onSuccess, Func<int, Task> sendAsync, bool warnOnFailure = true)
    {
        Debug.Log($"[GameplayEventTracker] TrackAndSendEventAsync called for: {eventName}");
        
        if (!enableBlockchainRecording)
        {
            Debug.LogWarning($"[GameplayEventTracker] ⚠️ Blockchain recording is DISABLED - skipping {eventName}");
            return;
        }

        if (GameAPIClient.Instance == null)
        {
            Debug.LogWarning($"⚠️ GameAPIClient missing - cannot record {eventName}");
            return;
        }

        int sessionId = GetActiveSessionId();
        if (sessionId == 0)
        {
            Debug.LogWarning($"[GameplayEventTracker] ⚠️ Session ID is 0 - skipping {eventName}");
            return;
        }
        
        Debug.Log($"[GameplayEventTracker] ✅ Proceeding with {eventName} for session {sessionId}");

        RegisterPendingRequest();

        try
        {
            string startMessage = onStart?.Invoke();
            if (!string.IsNullOrEmpty(startMessage))
            {
                Debug.Log($"{startMessage} (Session {sessionId})...");
            }

            await sendAsync(sessionId);

            string successMessage = onSuccess?.Invoke();
            if (!string.IsNullOrEmpty(successMessage))
            {
                Debug.Log(successMessage);
            }
        }
        catch (GameAPIClient.SessionInactiveException)
        {
            if (verboseLogging)
            {
                Debug.Log($"⚠️ Skipped {eventName} because the blockchain session already ended.");
            }
        }
        catch (Exception e)
        {
            if (warnOnFailure)
            {
                Debug.LogWarning($"⚠️ Failed to record {eventName}: {e.Message}");
            }
        }
        finally
        {
            CompletePendingRequest();
        }
    }

    private void RegisterPendingRequest()
    {
        lock (pendingLock)
        {
            pendingRequestCount++;
            if (pendingRequestCount == 1)
            {
                // Reset signal so new waiters block until we drain back to zero
                pendingDrainedSignal = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            }
        }
    }

    private void CompletePendingRequest()
    {
        TaskCompletionSource<bool> toSignal = null;

        lock (pendingLock)
        {
            pendingRequestCount = Mathf.Max(0, pendingRequestCount - 1);
            if (pendingRequestCount == 0)
            {
                toSignal = pendingDrainedSignal;
            }
        }

        toSignal?.TrySetResult(true);
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


