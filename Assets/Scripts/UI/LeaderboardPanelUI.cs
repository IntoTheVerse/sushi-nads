using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class LeaderboardPanelUI : MonoBehaviour
{
    [Header("Bindings")]
    [SerializeField] private Transform rowsParent;
    [SerializeField] private LeaderboardRowUI rowPrefab;
    [SerializeField] private TMP_Text headerText;
    [SerializeField] private TMP_Text lastUpdatedText;
    [SerializeField] private TMP_Text playerRankText;
    [SerializeField] private TMP_Text playerScoreText;
    [SerializeField] private TMP_Text emptyStateText;
    [SerializeField] private GameObject loadingState;
    [SerializeField] private GameObject leaderboardContentPanel;

    [SerializeField] private Button refreshButton;
    [SerializeField] private int fetchLimit = 25;
    
    [Header("Static Player Row (Outside Scroll View)")]
    [SerializeField] private LeaderboardRowUI staticPlayerRow; // Static row at top, always visible

    private readonly List<LeaderboardRowUI> spawnedRows = new();
    private bool isLoading;
    private VerticalLayoutGroup dynamicLayoutGroup;
    private ContentSizeFitter dynamicContentFitter;
    private Coroutine loadingRotationCoroutine;
    
    // Medal sprites for top 3 and player highlight
    [Header("Rank Sprites")]
    [SerializeField] private Sprite goldBoxSprite;      // For rank 1
    [SerializeField] private Sprite silverBoxSprite;    // For rank 2
    [SerializeField] private Sprite bronzeBoxSprite;    // For rank 3
    [SerializeField] private Sprite greenBoxSprite;     // For player highlight

    private void Awake()
    {
        AutoAssignReferences();

        if (refreshButton != null)
        {
            refreshButton.onClick.AddListener(RefreshLeaderboard);
        }
    }

    private void OnEnable()
    {
        // Hide content panel initially until data loads
        SetContentPanelVisible(false);
        RefreshLeaderboard();
    }

    private void OnDisable()
    {
        if (refreshButton != null)
        {
            refreshButton.onClick.RemoveListener(RefreshLeaderboard);
        }

        StopLoadingRotation(); // Stop rotation when panel is disabled
        ClearRows();
    }

    public void RefreshLeaderboard()
    {
        if (!isActiveAndEnabled || isLoading)
        {
            return;
        }

        _ = RefreshAsync();
    }

    private async System.Threading.Tasks.Task RefreshAsync()
    {
        if (GameAPIClient.Instance == null)
        {
            Debug.LogWarning("LeaderboardPanelUI: GameAPIClient is missing in the scene.");
            return;
        }

        isLoading = true;
        SetLoadingState(true, "Connecting wallet...");
        SetContentPanelVisible(false); // Hide content while loading
        ClearRows();

        try
        {
            var walletAddress = GameAPIClient.Instance.GetConnectedWalletAddress();
            if (string.IsNullOrEmpty(walletAddress))
            {
                walletAddress = await GameAPIClient.Instance.WaitForWalletAddressAsync(8f);
                if (string.IsNullOrEmpty(walletAddress))
                {
                    SetEmptyState("Connect wallet to view the leaderboard.");
                    SetLoadingState(false);
                    SetContentPanelVisible(false); // Keep hidden if wallet not connected
                    isLoading = false;
                    return;
                }
            }

            SetLoadingState(true, "Loading leaderboard...");

            var leaderboardResponse = await GameAPIClient.Instance.GetTopPlayers(fetchLimit);
            var playerRankResponse = await GameAPIClient.Instance.GetPlayerRank();

            RenderHeader(leaderboardResponse);
            RenderStaticPlayerRow(leaderboardResponse, playerRankResponse, walletAddress);
            RenderRows(leaderboardResponse, playerRankResponse, walletAddress);
            RenderPlayerCard(playerRankResponse);
            
            // Show content panel after successful render
            SetContentPanelVisible(true);
        }
        catch (Exception ex)
        {
            Debug.LogError($"LeaderboardPanelUI: failed to refresh leaderboard - {ex.Message}");
            SetEmptyState($"Unable to load leaderboard.\n{ex.Message}");
            // Keep content panel hidden on error
        }
        finally
        {
            SetLoadingState(false);
            isLoading = false;
        }
    }

    private void RenderHeader(LeaderboardResponse response)
    {
        if (response == null)
        {
            SetEmptyState("No leaderboard data yet.");
            return;
        }

        if (headerText != null)
        {
            headerText.text = $"Season {response.season}";
        }

        if (lastUpdatedText != null)
        {
            var latestTimestamp = 0L;
            if (response.rankings != null)
            {
                foreach (var entry in response.rankings)
                {
                    if (entry != null && entry.lastUpdated > latestTimestamp)
                    {
                        latestTimestamp = entry.lastUpdated;
                    }
                }
            }

            if (latestTimestamp > 0)
            {
                DateTimeOffset dto = DateTimeOffset.FromUnixTimeMilliseconds(latestTimestamp).ToLocalTime();
                lastUpdatedText.text = $"Updated {dto:MMM d • h:mm tt}";
            }
            else
            {
                lastUpdatedText.text = "Updated just now";
            }
        }
    }

    private void RenderStaticPlayerRow(LeaderboardResponse response, PlayerRankResponse playerRank, string walletAddress)
    {
        // Update the static player row at top (outside scroll view)
        if (staticPlayerRow == null || playerRank == null || playerRank.rank <= 0)
        {
            if (staticPlayerRow != null)
            {
                staticPlayerRow.gameObject.SetActive(false);
            }
            return;
        }

        // Try to find player's name from leaderboard response
        string playerName = null;
        if (response?.rankings != null && !string.IsNullOrEmpty(walletAddress))
        {
            var playerEntryInLeaderboard = response.rankings.FirstOrDefault(entry =>
                entry != null &&
                !string.IsNullOrEmpty(entry.player) &&
                entry.player.Equals(walletAddress, System.StringComparison.OrdinalIgnoreCase));
            
            if (playerEntryInLeaderboard != null)
            {
                playerName = playerEntryInLeaderboard.playerName;
            }
        }

        // Create entry for static player row
        var playerEntry = new LeaderboardEntry
        {
            rank = playerRank.rank,
            player = walletAddress,
            playerName = GetDisplayName(playerName, walletAddress),
            score = playerRank.score,
            sessionId = playerRank.sessionId,
            lastUpdated = playerRank.lastUpdated
        };

        // Bind the static row
        staticPlayerRow.gameObject.SetActive(true);
        staticPlayerRow.Bind(playerEntry, walletAddress, true);
        
        // Always use green box sprite for static player row
        if (greenBoxSprite != null)
        {
            staticPlayerRow.SetRankSprite(greenBoxSprite);
        }
    }

    private void RenderRows(LeaderboardResponse response, PlayerRankResponse playerRank, string walletAddress)
    {
        if (rowPrefab == null || rowsParent == null)
        {
            Debug.LogWarning("LeaderboardPanelUI: Row prefab or parent is not assigned.");
            return;
        }

        if (response?.rankings == null || response.rankings.Length == 0)
        {
            SetEmptyState("The leaderboard is quiet for now.\nBe the first to post a score!");
            return;
        }

        HideEmptyState();
        
        // Find if player is in top 5
        bool isPlayerInTop5 = false;
        LeaderboardEntry playerEntry = null;
        int playerRankValue = playerRank?.rank ?? 0;
        
        if (playerRankValue > 0 && !string.IsNullOrEmpty(walletAddress))
        {
            foreach (var entry in response.rankings)
            {
                if (entry != null && entry.rank == playerRankValue && 
                    !string.IsNullOrEmpty(entry.player) &&
                    entry.player.Equals(walletAddress, System.StringComparison.OrdinalIgnoreCase))
                {
                    if (entry.rank <= 5)
                    {
                        isPlayerInTop5 = true;
                        playerEntry = entry;
                    }
                    break;
                }
            }
        }
        
        // Get top 5 entries
        var top5Entries = new List<LeaderboardEntry>();
        for (int i = 0; i < Mathf.Min(5, response.rankings.Length); i++)
        {
            if (response.rankings[i] != null)
            {
                top5Entries.Add(response.rankings[i]);
            }
        }
        
        // Render top 5 with special colors for top 3
        foreach (var entry in top5Entries)
        {
            if (entry == null) continue;
            
            entry.playerName = GetDisplayName(entry.playerName, entry.player);
            bool isPlayer = !string.IsNullOrEmpty(entry.player) &&
                           !string.IsNullOrEmpty(walletAddress) &&
                           entry.player.Equals(walletAddress, System.StringComparison.OrdinalIgnoreCase);
            var row = AddRow(entry, walletAddress, true);
            
            // Apply medal sprites for top 3 (player in scroll view gets medal color, not green)
            // Green box is only for the static player row at top
            if (row != null)
            {
                if (entry.rank == 1 && goldBoxSprite != null)
                {
                    row.SetRankSprite(goldBoxSprite);
                }
                else if (entry.rank == 2 && silverBoxSprite != null)
                {
                    row.SetRankSprite(silverBoxSprite);
                }
                else if (entry.rank == 3 && bronzeBoxSprite != null)
                {
                    row.SetRankSprite(bronzeBoxSprite);
                }
                // Note: Player in scroll view shows their actual rank color, not green
                // Green box is reserved for the static player row at top
            }
        }
        
        // If player is NOT in top 5, show 4 normal rankers, then player at bottom
        if (!isPlayerInTop5 && playerRankValue > 5)
        {
            // Get 4 normal rankers (prefer ranks 6-9, but skip player)
            var normalRankers = new List<LeaderboardEntry>();
            int normalRankerCount = 0;
            
            foreach (var entry in response.rankings)
            {
                if (entry == null) continue;
                
                // Skip top 5 entries (already displayed)
                if (entry.rank <= 5) continue;
                
                // Skip player entry (will be shown at bottom)
                bool isPlayer = !string.IsNullOrEmpty(entry.player) &&
                               !string.IsNullOrEmpty(walletAddress) &&
                               entry.player.Equals(walletAddress, System.StringComparison.OrdinalIgnoreCase);
                if (isPlayer) continue;
                
                // Add normal ranker
                normalRankers.Add(entry);
                normalRankerCount++;
                
                if (normalRankerCount >= 4) break;
            }
            
            // Render 4 normal rankers
            foreach (var entry in normalRankers)
            {
                entry.playerName = GetDisplayName(entry.playerName, entry.player);
                AddRow(entry, walletAddress, false);
            }
            
            // Render player at bottom
            EnsureLocalPlayerRow(response, playerRank, walletAddress);
        }
        
        RebuildLayout();
    }

    private void RenderPlayerCard(PlayerRankResponse response)
    {
        if (response == null)
        {
            if (playerRankText != null) playerRankText.text = "Rank —";
            if (playerScoreText != null) playerScoreText.text = "Score —";
            return;
        }

        if (playerRankText != null)
        {
            if (response.rank > 0)
            {
                playerRankText.text = response.totalPlayers > 0
                    ? $"#{response.rank} / {Mathf.Max(1, response.totalPlayers)}"
                    : $"#{response.rank}";
            }
            else
            {
                playerRankText.text = "Unranked";
            }
        }

        if (playerScoreText != null)
        {
            if (response.score > 0)
            {
                string sessionLabel = response.sessionId > 0 ? $"Session #{response.sessionId}" : null;
                string lastRunLabel = response.lastUpdated > 0 ? $"Last run {FormatRelativeTime(response.lastUpdated)}" : null;

                if (!string.IsNullOrEmpty(sessionLabel) && !string.IsNullOrEmpty(lastRunLabel))
                {
                    playerScoreText.text = $"{response.score:N0} pts • {sessionLabel}\n{lastRunLabel}";
                }
                else if (!string.IsNullOrEmpty(sessionLabel))
                {
                    playerScoreText.text = $"{response.score:N0} pts • {sessionLabel}";
                }
                else if (!string.IsNullOrEmpty(lastRunLabel))
                {
                    playerScoreText.text = $"{response.score:N0} pts\n{lastRunLabel}";
                }
                else
                {
                    playerScoreText.text = $"{response.score:N0} pts";
                }
            }
            else
            {
                playerScoreText.text = "Play a run to set a score";
            }
        }
    }

    private void ClearRows()
    {
        foreach (var row in spawnedRows)
        {
            if (row != null)
            {
                Destroy(row.gameObject);
            }
        }

        spawnedRows.Clear();
        RebuildLayout();
    }

    private void SetLoadingState(bool isVisible, string message = null)
    {
        if (loadingState != null)
        {
            loadingState.SetActive(isVisible);
            
            // Start/Stop rotation animation
            if (isVisible)
            {
                StartLoadingRotation();
            }
            else
            {
                StopLoadingRotation();
            }
        }

        if (isVisible)
        {
            HideEmptyState();
        }

        if (!string.IsNullOrEmpty(message) && emptyStateText != null)
        {
            emptyStateText.text = message;
        }
    }
    
    private void StartLoadingRotation()
    {
        StopLoadingRotation(); // Stop any existing rotation
        
        if (loadingState != null)
        {
            loadingRotationCoroutine = StartCoroutine(RotateLoadingIcon());
        }
    }
    
    private void StopLoadingRotation()
    {
        if (loadingRotationCoroutine != null)
        {
            StopCoroutine(loadingRotationCoroutine);
            loadingRotationCoroutine = null;
        }
    }
    
    private System.Collections.IEnumerator RotateLoadingIcon()
    {
        float rotationSpeed = 180f; // Degrees per second
        while (loadingState != null && loadingState.activeSelf)
        {
            if (loadingState.transform != null)
            {
                loadingState.transform.Rotate(0f, 0f, -rotationSpeed * Time.deltaTime);
            }
            yield return null;
        }
    }
    
    private void SetContentPanelVisible(bool isVisible)
    {
        if (leaderboardContentPanel != null)
        {
            leaderboardContentPanel.SetActive(isVisible);
        }
    }

    private void SetEmptyState(string message)
    {
        if (emptyStateText != null)
        {
            emptyStateText.gameObject.SetActive(true);
            emptyStateText.text = message;
        }
    }

    private void HideEmptyState()
    {
        if (emptyStateText != null)
        {
            emptyStateText.gameObject.SetActive(false);
        }
    }

    private void AutoAssignReferences()
    {
        if (rowsParent == null)
        {
            var scrollRect = GetComponentInChildren<ScrollRect>(true);
            rowsParent = scrollRect != null ? scrollRect.content : null;
        }

        ConfigureRowPrefab();
        EnsureLayoutContainer();

        if (headerText == null)
        {
            headerText = FindTextByName("LeaderboardTitle");
        }

        if (lastUpdatedText == null)
        {
            lastUpdatedText = FindTextByName("LeaderboardSubtitle") ?? FindTextContaining("Updated");
            if (lastUpdatedText == null && headerText != null)
            {
                lastUpdatedText = headerText.transform.parent?
                    .GetComponentsInChildren<TMP_Text>(true)
                    .FirstOrDefault(t => t != headerText);
            }
        }

        if (playerRankText == null)
        {
            playerRankText = FindTextByName("PlayerRankValue") ?? FindTextContaining("Rank");
        }

        if (playerScoreText == null)
        {
            playerScoreText = FindTextByName("PlayerScoreValue") ?? FindTextContaining("Score");
        }

        if (refreshButton == null)
        {
            var buttons = GetComponentsInChildren<Button>(true);
            foreach (var btn in buttons)
            {
                if (btn.name.Contains("Refresh", StringComparison.OrdinalIgnoreCase))
                {
                    refreshButton = btn;
                    break;
                }
            }
        }
    }

    private void ConfigureRowPrefab()
    {
        if (rowsParent == null)
        {
            return;
        }

        var templates = rowsParent.GetComponentsInChildren<LeaderboardRowUI>(true);
        if (templates == null || templates.Length == 0)
        {
            return;
        }

        rowPrefab = templates[0];
        foreach (var template in templates)
        {
            if (template == null)
            {
                continue;
            }

            if (template != rowPrefab && template.gameObject.activeSelf)
            {
                template.gameObject.SetActive(false);
            }
        }

        if (rowPrefab != null && rowPrefab.gameObject.activeSelf)
        {
            rowPrefab.gameObject.SetActive(false);
        }

        if (rowPrefab != null)
        {
            EnsureRowLayout(rowPrefab.transform as RectTransform);
        }
    }

    private void EnsureLayoutContainer()
    {
        if (rowsParent == null)
        {
            return;
        }

        dynamicLayoutGroup = rowsParent.GetComponent<VerticalLayoutGroup>();
        if (dynamicLayoutGroup == null)
        {
            dynamicLayoutGroup = rowsParent.gameObject.AddComponent<VerticalLayoutGroup>();
        }

        dynamicLayoutGroup.spacing = 12f;
        dynamicLayoutGroup.childAlignment = TextAnchor.UpperCenter;
        dynamicLayoutGroup.childControlWidth = true;
        dynamicLayoutGroup.childControlHeight = true;
        dynamicLayoutGroup.childForceExpandWidth = true;
        dynamicLayoutGroup.childForceExpandHeight = false;

        var layoutElement = rowsParent.GetComponent<LayoutElement>();
        if (layoutElement == null)
        {
            layoutElement = rowsParent.gameObject.AddComponent<LayoutElement>();
            layoutElement.minHeight = 0;
            layoutElement.flexibleHeight = 0;
        }

        dynamicContentFitter = rowsParent.GetComponent<ContentSizeFitter>();
        if (dynamicContentFitter == null)
        {
            dynamicContentFitter = rowsParent.gameObject.AddComponent<ContentSizeFitter>();
        }

        dynamicContentFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        dynamicContentFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
    }

    private void RebuildLayout()
    {
        if (rowsParent == null)
        {
            return;
        }

        var rect = rowsParent as RectTransform ?? rowsParent.GetComponent<RectTransform>();
        if (rect != null)
        {
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(rect);
        }
    }

    private void EnsureRowLayout(RectTransform rowTransform)
    {
        if (rowTransform == null)
        {
            return;
        }

        var layoutElement = rowTransform.GetComponent<LayoutElement>();
        if (layoutElement == null)
        {
            layoutElement = rowTransform.gameObject.AddComponent<LayoutElement>();
        }

        // Use existing size if set, otherwise fall back to default height
        float preferredHeight = rowTransform.sizeDelta.y > 0 ? rowTransform.sizeDelta.y : 120f;
        layoutElement.minHeight = preferredHeight;
        layoutElement.preferredHeight = preferredHeight;
        layoutElement.flexibleHeight = 0;
    }

    private TMP_Text FindTextByName(string targetName)
    {
        if (string.IsNullOrEmpty(targetName))
        {
            return null;
        }

        var texts = GetComponentsInChildren<TMP_Text>(true);
        foreach (var text in texts)
        {
            if (text != null && text.name.Equals(targetName, StringComparison.OrdinalIgnoreCase))
            {
                return text;
            }
        }

        return null;
    }

    private TMP_Text FindTextContaining(string fragment)
    {
        if (string.IsNullOrEmpty(fragment))
        {
            return null;
        }

        var texts = GetComponentsInChildren<TMP_Text>(true);
        return texts.FirstOrDefault(t =>
            t != null && t.name.IndexOf(fragment, StringComparison.OrdinalIgnoreCase) >= 0);
    }

    private LeaderboardRowUI AddRow(LeaderboardEntry entry, string walletAddress, bool emphasizeLocalPlayer)
    {
        if (rowPrefab == null || rowsParent == null || entry == null)
        {
            return null;
        }

        var rowObject = Instantiate(rowPrefab.gameObject, rowsParent);
        rowObject.SetActive(true);
        var rowInstance = rowObject.GetComponent<LeaderboardRowUI>();
        if (rowInstance == null)
        {
            Debug.LogError("LeaderboardPanelUI: Row prefab is missing LeaderboardRowUI component.");
            Destroy(rowObject);
            return null;
        }

        var rowTransform = rowObject.transform as RectTransform;
        if (rowTransform != null)
        {
            rowTransform.localScale = Vector3.one;
            rowTransform.localRotation = Quaternion.identity;
            rowTransform.SetAsLastSibling();
            EnsureRowLayout(rowTransform);
        }

        rowInstance.Bind(entry, walletAddress, emphasizeLocalPlayer);
        spawnedRows.Add(rowInstance);
        foreach (var row in spawnedRows){
            row.gameObject.SetActive(true);
        }
        
        return rowInstance;
    }

    private void EnsureLocalPlayerRow(LeaderboardResponse response, PlayerRankResponse playerRank, string walletAddress)
    {
        if (playerRank == null || playerRank.rank <= 0 || string.IsNullOrEmpty(walletAddress))
        {
            return;
        }

        // Check if player is already in the displayed rankings from response
        bool alreadyListed = response?.rankings?.Any(entry =>
            entry != null &&
            !string.IsNullOrEmpty(entry.player) &&
            entry.player.Equals(walletAddress, StringComparison.OrdinalIgnoreCase)) ?? false;

        if (alreadyListed)
        {
            // Player is already in the rankings we've displayed
            return;
        }

        // Create player entry for bottom display
        var fallbackEntry = new LeaderboardEntry
        {
            rank = playerRank.rank,
            player = walletAddress,
            playerName = GetDisplayName(null, walletAddress),
            score = playerRank.score,
            sessionId = playerRank.sessionId,
            lastUpdated = playerRank.lastUpdated
        };

        // Add player row at bottom with green highlight sprite
        var playerRow = AddRow(fallbackEntry, walletAddress, true);
        if (playerRow != null && greenBoxSprite != null)
        {
            playerRow.SetRankSprite(greenBoxSprite);
        }
    }

    private string GetDisplayName(string playerName, string walletAddress)
    {
        if (!string.IsNullOrWhiteSpace(playerName))
        {
            return playerName;
        }

        if (string.IsNullOrWhiteSpace(walletAddress))
        {
            return "Unknown Chef";
        }

        walletAddress = walletAddress.Trim();
        if (walletAddress.Length <= 10)
        {
            return walletAddress;
        }

        return $"{walletAddress.Substring(0, 6)}...{walletAddress.Substring(walletAddress.Length - 4)}";
    }

    private string FormatRelativeTime(long timestampMs)
    {
        if (timestampMs <= 0)
        {
            return "just now";
        }

        var eventTime = DateTimeOffset.FromUnixTimeMilliseconds(timestampMs);
        var delta = DateTimeOffset.UtcNow - eventTime;

        if (delta.TotalMinutes < 1)
        {
            return "just now";
        }

        if (delta.TotalHours < 1)
        {
            return $"{Mathf.FloorToInt((float)delta.TotalMinutes)}m ago";
        }

        if (delta.TotalDays < 1)
        {
            return $"{Mathf.FloorToInt((float)delta.TotalHours)}h ago";
        }

        if (delta.TotalDays < 7)
        {
            return $"{Mathf.FloorToInt((float)delta.TotalDays)}d ago";
        }

        return eventTime.LocalDateTime.ToString("MMM d");
    }
}


