using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class LeaderboardRowUI : MonoBehaviour
{
    [SerializeField] private TMP_Text rankText;
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text scoreText;
    [SerializeField] private TMP_Text metadataText;
    [SerializeField] private TMP_Text walletAddress;
    [SerializeField] private GameObject highlight;
    [SerializeField] private Image backgroundImage;
    
    private void Awake()
    {
        // Auto-find background Image if not assigned
        if (backgroundImage == null)
        {
            backgroundImage = GetComponent<Image>();
            if (backgroundImage == null)
            {
                // Try to find in children (common pattern: row has child Image component)
                backgroundImage = GetComponentInChildren<Image>();
            }
        }
    }

    public void Bind(LeaderboardEntry entry, string localPlayerAddress, bool emphasizeRow)
    {
        if (entry == null)
        {
            gameObject.SetActive(false);
            return;
        }

        if (rankText != null)
        {
            rankText.text = entry.rank > 0 ? $"#{entry.rank}" : "—";
        }

        if (nameText != null)
        {
            nameText.text = string.IsNullOrWhiteSpace(entry.playerName) ? "Unknown Chef" : entry.playerName;
        }

        if (scoreText != null)
        {
            scoreText.text = entry.score.ToString("N0");
        }

        if (metadataText != null)
        {
            metadataText.text = FormatTimestamp(entry.lastUpdated);
        }
        // ✅ truncated wallet in its own text
        if (walletAddress != null)
        {
            walletAddress.text = GetShortWallet(entry.player);
        }
        if (highlight != null)
        {
            bool isLocalPlayer = !string.IsNullOrEmpty(localPlayerAddress) &&
                                 !string.IsNullOrEmpty(entry.player) &&
                                 entry.player.Equals(localPlayerAddress, System.StringComparison.OrdinalIgnoreCase);
            highlight.SetActive(emphasizeRow && isLocalPlayer);
        }
    }
    
    public void SetRankSprite(Sprite sprite)
    {
        // Ensure we have a reference to the Image component
        if (backgroundImage == null)
        {
            backgroundImage = GetComponent<Image>();
            if (backgroundImage == null)
            {
                backgroundImage = GetComponentInChildren<Image>();
            }
        }
        
        if (backgroundImage != null && sprite != null)
        {
            backgroundImage.sprite = sprite;
        }
        else
        {
            if (backgroundImage == null)
            {
                Debug.LogWarning($"LeaderboardRowUI: No Image component found on {gameObject.name} to set sprite!");
            }
            if (sprite == null)
            {
                Debug.LogWarning($"LeaderboardRowUI: Sprite is null for {gameObject.name}");
            }
        }
    }
    
    private string GetShortWallet(string addr)
    {
        if (string.IsNullOrWhiteSpace(addr))
            return string.Empty;

        addr = addr.Trim();
        if (addr.Length <= 10)
            return addr;

        return $"{addr.Substring(0, 6)}...{addr.Substring(addr.Length - 4)}";
    }

    private string FormatTimestamp(long timestampMs)
    {
        if (timestampMs <= 0)
        {
            return "Just now";
        }

        var eventTime = DateTimeOffset.FromUnixTimeMilliseconds(timestampMs).ToLocalTime();
        var delta = DateTimeOffset.Now - eventTime;

        if (delta.TotalMinutes < 1)
        {
            return "Just now";
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

        return eventTime.ToString("MMM d");
    }
}


