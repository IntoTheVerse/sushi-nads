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
    [SerializeField] private Button refreshButton;
    [SerializeField] private int fetchLimit = 25;

    private readonly List<LeaderboardRowUI> spawnedRows = new();
    private bool isLoading;

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
        RefreshLeaderboard();
    }

    private void OnDisable()
    {
        if (refreshButton != null)
        {
            refreshButton.onClick.RemoveListener(RefreshLeaderboard);
        }

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
                    isLoading = false;
                    return;
                }
            }

            SetLoadingState(true, "Loading leaderboard...");

            var leaderboardResponse = await GameAPIClient.Instance.GetTopPlayers(fetchLimit);
            var playerRankResponse = await GameAPIClient.Instance.GetPlayerRank();

            RenderHeader(leaderboardResponse);
            RenderRows(leaderboardResponse, playerRankResponse, walletAddress);
            RenderPlayerCard(playerRankResponse);
        }
        catch (Exception ex)
        {
            Debug.LogError($"LeaderboardPanelUI: failed to refresh leaderboard - {ex.Message}");
            SetEmptyState($"Unable to load leaderboard.\n{ex.Message}");
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

        foreach (var entry in response.rankings)
        {
            if (entry == null)
            {
                continue;
            }
            entry.playerName = GetDisplayName(entry.playerName, entry.player);
            AddRow(entry, walletAddress, true);
        }

        EnsureLocalPlayerRow(response, playerRank, walletAddress);
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
    }

    private void SetLoadingState(bool isVisible, string message = null)
    {
        if (loadingState != null)
        {
            loadingState.SetActive(isVisible);
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

    private void AddRow(LeaderboardEntry entry, string walletAddress, bool emphasizeLocalPlayer)
    {
        if (rowPrefab == null || rowsParent == null || entry == null)
        {
            return;
        }

        var rowInstance = Instantiate(rowPrefab, rowsParent);
        rowInstance.gameObject.SetActive(true);
        rowInstance.Bind(entry, walletAddress, emphasizeLocalPlayer);
        spawnedRows.Add(rowInstance);
    }

    private void EnsureLocalPlayerRow(LeaderboardResponse response, PlayerRankResponse playerRank, string walletAddress)
    {
        if (playerRank == null || playerRank.rank <= 0 || string.IsNullOrEmpty(walletAddress))
        {
            return;
        }

        bool alreadyListed = response?.rankings?.Any(entry =>
            entry != null &&
            !string.IsNullOrEmpty(entry.player) &&
            entry.player.Equals(walletAddress, StringComparison.OrdinalIgnoreCase)) ?? false;

        if (alreadyListed)
        {
            return;
        }

        var fallbackEntry = new LeaderboardEntry
        {
            rank = playerRank.rank,
            player = walletAddress,
            playerName = GetDisplayName(null, walletAddress),
            score = playerRank.score,
            sessionId = playerRank.sessionId,
            lastUpdated = playerRank.lastUpdated
        };

        AddRow(fallbackEntry, walletAddress, true);
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


