using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Manages profile tab switching and ensures data is populated when the panel opens.
/// Add this component to the ProfilePanel (or BGtwo/3GUIS parent) root.
/// Tab buttons and panels are auto-found; no need to wire On Click() in Inspector.
/// </summary>
public class ProfilePanelUI : MonoBehaviour
{
    [Header("Tab Buttons (optional - auto-found if empty)")]
    [SerializeField] private Button infoBtn;
    [SerializeField] private Button globalStatisticsBtn;
    [SerializeField] private Button personalRecordsBtn;
    [SerializeField] private Button recentSessionsBtn;

    [Header("Tab Panels (optional - auto-found if empty)")]
    [SerializeField] private GameObject infoPanel;
    [SerializeField] private GameObject globalPanel;
    [SerializeField] private GameObject personalPanel;
    [SerializeField] private GameObject recentPanel;

    private void Awake()
    {
        ResolveTabs();
        WireTabClicks();
    }

    private void OnEnable()
    {
        EnsureTabButtonsEnabled();
        RefreshProfileStats();
    }

    private void RefreshProfileStats()
    {
        var profile = ChefManager.LastProfile;
        if (profile == null && GameAPIClient.Instance != null)
        {
            _ = FetchAndApplyProfileAsync();
            return;
        }
        if (profile != null && ChefManager.Instance != null)
        {
            ChefManager.Instance.RefreshProfileUI(profile, transform);
        }
    }

    private async System.Threading.Tasks.Task FetchAndApplyProfileAsync()
    {
        try
        {
            var profile = await GameAPIClient.Instance.GetProfile();
            if (profile != null && ChefManager.Instance != null)
            {
                ChefManager.Instance.RefreshProfileUI(profile, transform);
            }
        }
        catch (System.Exception ex)
        {
            UnityEngine.Debug.LogWarning($"[ProfilePanelUI] Failed to fetch profile: {ex.Message}");
        }
    }

    private void EnsureTabButtonsEnabled()
    {
        ResolveButtons();

        if (globalStatisticsBtn != null)
        {
            globalStatisticsBtn.interactable = true;
        }
        if (personalRecordsBtn != null)
        {
            personalRecordsBtn.interactable = true;
        }
        if (recentSessionsBtn != null)
        {
            recentSessionsBtn.interactable = true;
        }
    }

    private void ResolveTabs()
    {
        var buttons = GetComponentsInChildren<Button>(true);
        foreach (var btn in buttons)
        {
            if (btn == null) continue;
            string name = (btn.gameObject?.name ?? "").Trim();
            string childText = GetButtonText(btn);

            if (Matches(name, childText, "Info") && infoBtn == null) infoBtn = btn;
            else if (Matches(name, childText, "Global Statistics") && globalStatisticsBtn == null) globalStatisticsBtn = btn;
            else if ((Matches(name, childText, "Personal Record") || Matches(name, childText, "Personal Records")) && personalRecordsBtn == null) personalRecordsBtn = btn;
            else if (Matches(name, childText, "Recent Sessions") && recentSessionsBtn == null) recentSessionsBtn = btn;
        }

        var all = GetComponentsInChildren<Transform>(true);
        foreach (var t in all)
        {
            if (t == null) continue;
            var n = (t.name ?? "").ToLowerInvariant();
            if (infoPanel == null && (n == "infopanel" || n.Contains("info panel"))) infoPanel = t.gameObject;
            else if (globalPanel == null && (n == "globalpanel" || n.Contains("global panel"))) globalPanel = t.gameObject;
            else if (personalPanel == null && (n == "personalpanel" || n.Contains("personal panel"))) personalPanel = t.gameObject;
            else if (recentPanel == null && (n == "recentpanel" || n.Contains("recent panel"))) recentPanel = t.gameObject;
        }
    }

    private void WireTabClicks()
    {
        void ShowTab(int index)
        {
            if (infoPanel != null) infoPanel.SetActive(index == 0);
            if (globalPanel != null) globalPanel.SetActive(index == 1);
            if (personalPanel != null) personalPanel.SetActive(index == 2);
            if (recentPanel != null) recentPanel.SetActive(index == 3);
            if (infoBtn != null) infoBtn.interactable = index != 0;
            if (globalStatisticsBtn != null) globalStatisticsBtn.interactable = index != 1;
            if (personalRecordsBtn != null) personalRecordsBtn.interactable = index != 2;
            if (recentSessionsBtn != null) recentSessionsBtn.interactable = index != 3;
        }

        if (infoBtn != null) infoBtn.onClick.AddListener(() => ShowTab(0));
        if (globalStatisticsBtn != null) globalStatisticsBtn.onClick.AddListener(() => ShowTab(1));
        if (personalRecordsBtn != null) personalRecordsBtn.onClick.AddListener(() => ShowTab(2));
        if (recentSessionsBtn != null) recentSessionsBtn.onClick.AddListener(() => ShowTab(3));

        ShowTab(0);
    }

    private void ResolveButtons()
    {
        ResolveTabs();
    }

    private static bool Matches(string name, string childText, string keyword)
    {
        return (name?.IndexOf(keyword, System.StringComparison.OrdinalIgnoreCase) ?? -1) >= 0 ||
               (childText?.IndexOf(keyword, System.StringComparison.OrdinalIgnoreCase) ?? -1) >= 0;
    }

    private static string GetButtonText(Button btn)
    {
        if (btn == null) return null;
        var tmp = btn.GetComponentInChildren<TMP_Text>(true);
        return tmp?.text;
    }
}
