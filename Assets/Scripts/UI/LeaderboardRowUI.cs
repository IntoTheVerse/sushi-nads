using System;
using TMPro;
using UnityEngine;

public class LeaderboardRowUI : MonoBehaviour
{
    [SerializeField] private TMP_Text rankText;
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text scoreText;
    [SerializeField] private TMP_Text metadataText;
    [SerializeField] private GameObject highlight;

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

        if (highlight != null)
        {
            bool isLocalPlayer = !string.IsNullOrEmpty(localPlayerAddress) &&
                                 !string.IsNullOrEmpty(entry.player) &&
                                 entry.player.Equals(localPlayerAddress, System.StringComparison.OrdinalIgnoreCase);
            highlight.SetActive(emphasizeRow && isLocalPlayer);
        }
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


