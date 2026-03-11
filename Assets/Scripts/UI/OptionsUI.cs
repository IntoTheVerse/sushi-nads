using System;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class OptionsUI : MonoBehaviour
{
    public static OptionsUI Instance { get; private set; }

    [Header("SFX")]
    [SerializeField] private Slider sfxSlider;
    [SerializeField] private TextMeshProUGUI sfxValueText;

    [Header("Music")]
    [SerializeField] private Slider musicSlider;
    [SerializeField] private TextMeshProUGUI musicValueText;
    [SerializeField] private Button prevTrackButton;
    [SerializeField] private Button nextTrackButton;
    [SerializeField] private TextMeshProUGUI trackNameText;

    // [Header("Misc")]
    //[SerializeField] private Button closeButton;

    private void Awake()
    {
        Instance = this;

        EnsureReferences();

        Debug.Log($"[OptionsUI] Awake - sfxSlider: {(sfxSlider != null ? "FOUND" : "NULL")}, musicSlider: {(musicSlider != null ? "FOUND" : "NULL")}");
        Debug.Log($"[OptionsUI] Awake - sfxValueText: {(sfxValueText != null ? "FOUND" : "NULL")}, musicValueText: {(musicValueText != null ? "FOUND" : "NULL")}");

        if (!ValidateBindings())
        {
            enabled = false;
            return;
        }

        if (sfxSlider != null)
        {
            sfxSlider.onValueChanged.AddListener(OnSfxChanged);
            Debug.Log("[OptionsUI] SFX slider listener registered");
        }
        if (musicSlider != null)
        {
            musicSlider.onValueChanged.AddListener(OnMusicChanged);
            Debug.Log("[OptionsUI] Music slider listener registered");
        }
        if (prevTrackButton != null) prevTrackButton.onClick.AddListener(OnPrevTrack);
        if (nextTrackButton != null) nextTrackButton.onClick.AddListener(OnNextTrack);
        //if (closeButton != null) closeButton.onClick.AddListener(Hide);
    }

    private void Start()
    {
        if (KitchenGameManager.Instance != null)
        {
            KitchenGameManager.Instance.OnGameUnpaused += KitchenGameManager_OnGameUnpaused;
        }

        if (SoundManager.Instance != null && sfxSlider != null)
        {
            sfxSlider.SetValueWithoutNotify(SoundManager.Instance.GetVolume());     // 0..1
            if (sfxValueText)
                sfxValueText.text = Mathf.RoundToInt(SoundManager.Instance.GetVolume() * 10f).ToString();
        }

        if (MusicManager.Instance != null && musicSlider != null)
        {
            musicSlider.SetValueWithoutNotify(MusicManager.Instance.GetVolume());   // 0..1
            if (musicValueText)
                musicValueText.text = Mathf.RoundToInt(MusicManager.Instance.GetVolume() * 10f).ToString();
        }

        UpdateVisual();
        Hide();
    }

    private void OnDestroy()
    {
        if (KitchenGameManager.Instance != null)
            KitchenGameManager.Instance.OnGameUnpaused -= KitchenGameManager_OnGameUnpaused;

        if (sfxSlider != null) sfxSlider.onValueChanged.RemoveListener(OnSfxChanged);
        if (musicSlider != null) musicSlider.onValueChanged.RemoveListener(OnMusicChanged);
        if (prevTrackButton != null) prevTrackButton.onClick.RemoveListener(OnPrevTrack);
        if (nextTrackButton != null) nextTrackButton.onClick.RemoveListener(OnNextTrack);
        //if (closeButton != null) closeButton.onClick.RemoveListener(Hide);
    }

    private void EnsureReferences()
    {
        CacheSliders();
        CacheButtons();
        CacheTexts();
    }

    private void CacheSliders()
    {
        var sliders = GetComponentsInChildren<Slider>(true);

        if (sliders == null || sliders.Length == 0) return;

        if (sfxSlider == null)
        {
            sfxSlider = sliders.FirstOrDefault(s => s.name.IndexOf("sfx", StringComparison.OrdinalIgnoreCase) >= 0)
                        ?? sliders.FirstOrDefault();
        }

        if (musicSlider == null)
        {
            musicSlider = sliders.FirstOrDefault(s => s.name.IndexOf("music", StringComparison.OrdinalIgnoreCase) >= 0)
                          ?? (sliders.Length > 1 ? sliders[1] : sliders.FirstOrDefault());
        }

        Debug.Log($"[OptionsUI] CacheSliders - Found {sliders.Length} sliders total");
        Debug.Log($"[OptionsUI] SFX Slider: {(sfxSlider != null ? sfxSlider.name : "NULL")}, interactable: {(sfxSlider != null ? sfxSlider.interactable : "N/A")}");
        Debug.Log($"[OptionsUI] Music Slider: {(musicSlider != null ? musicSlider.name : "NULL")}, interactable: {(musicSlider != null ? musicSlider.interactable : "N/A")}");
    }

    private void CacheButtons()
    {
        var buttons = GetComponentsInChildren<Button>(true);
        if (buttons == null || buttons.Length == 0) return;

        if (prevTrackButton == null)
        {
            prevTrackButton = buttons.FirstOrDefault(b => b.name.IndexOf("prev", StringComparison.OrdinalIgnoreCase) >= 0);
        }

        if (nextTrackButton == null)
        {
            nextTrackButton = buttons.FirstOrDefault(b => b.name.IndexOf("next", StringComparison.OrdinalIgnoreCase) >= 0);
        }

        // if (closeButton == null)
        // {
        //     closeButton = buttons.FirstOrDefault(b => b.name.IndexOf("close", StringComparison.OrdinalIgnoreCase) >= 0
        //                                             || b.name.IndexOf("exit", StringComparison.OrdinalIgnoreCase) >= 0)
        //                   ?? buttons.LastOrDefault();
        // }
    }

    private void CacheTexts()
    {
        var texts = GetComponentsInChildren<TextMeshProUGUI>(true);
        if (texts == null || texts.Length == 0) return;

        if (sfxValueText == null)
        {
            sfxValueText = texts.FirstOrDefault(t => t.name.IndexOf("sfx", StringComparison.OrdinalIgnoreCase) >= 0);
        }

        if (musicValueText == null)
        {
            musicValueText = texts.FirstOrDefault(t => t.name.IndexOf("music", StringComparison.OrdinalIgnoreCase) >= 0);
        }

        if (trackNameText == null)
        {
            trackNameText = texts.FirstOrDefault(t => t.name.IndexOf("track", StringComparison.OrdinalIgnoreCase) >= 0);
        }
    }

    private bool ValidateBindings()
    {
        bool valid = true;

        if (sfxSlider == null)
        {
            Debug.LogWarning("OptionsUI missing SFX slider reference.", this);
            valid = false;
        }

        if (musicSlider == null)
        {
            Debug.LogWarning("OptionsUI missing Music slider reference.", this);
            valid = false;
        }

        if (prevTrackButton == null)
        {
            Debug.LogWarning("OptionsUI missing prev track button reference.", this);
            valid = false;
        }

        if (nextTrackButton == null)
        {
            Debug.LogWarning("OptionsUI missing next track button reference.", this);
            valid = false;
        }

        // if (closeButton == null)
        // {
        //     Debug.LogWarning("OptionsUI missing close button reference.", this);
        //     valid = false;
        // }

        // Log which text fields are missing (these are optional)
        if (sfxValueText == null)
        {
            Debug.LogWarning("OptionsUI missing SFX value text reference (optional).", this);
        }

        if (musicValueText == null)
        {
            Debug.LogWarning("OptionsUI missing Music value text reference (optional).", this);
        }

        return valid;
    }

    private void KitchenGameManager_OnGameUnpaused(object s, EventArgs e) => Hide();

    private void OnSfxChanged(float v)
    {
        Debug.Log($"[OptionsUI] OnSfxChanged called with value: {v}");
        if (SoundManager.Instance != null)
        {
            SoundManager.Instance.SetVolume(v);
        }
        if (sfxValueText) 
        {
            string newText = Mathf.RoundToInt(v * 10f).ToString();
            sfxValueText.text = newText;
            Debug.Log($"[OptionsUI] Updated SFX text to: {newText}");
        }
        else
        {
            Debug.LogWarning("[OptionsUI] sfxValueText is null!");
        }
    }

    private void OnMusicChanged(float v)
    {
        Debug.Log($"[OptionsUI] OnMusicChanged called with value: {v}");
        if (MusicManager.Instance != null)
        {
            MusicManager.Instance.SetVolume(v);
        }
        if (musicValueText) 
        {
            string newText = Mathf.RoundToInt(v * 10f).ToString();
            musicValueText.text = newText;
            Debug.Log($"[OptionsUI] Updated Music text to: {newText}");
        }
        else
        {
            Debug.LogWarning("[OptionsUI] musicValueText is null!");
        }
    }

    private void OnPrevTrack()
    {
        if (MusicManager.Instance != null)
        {
            MusicManager.Instance.PrevTrack();
        }
        UpdateTrackLabel();
    }

    private void OnNextTrack()
    {
        if (MusicManager.Instance != null)
        {
            MusicManager.Instance.NextTrack();
        }
        UpdateTrackLabel();
    }

    private void UpdateVisual()
    {
        if (sfxValueText && sfxSlider)
            sfxValueText.text = Mathf.RoundToInt(sfxSlider.value * 10f).ToString();
        if (musicValueText && musicSlider)
            musicValueText.text = Mathf.RoundToInt(musicSlider.value * 10f).ToString();
        UpdateTrackLabel();
    }

    private void UpdateTrackLabel()
    {
        if (!trackNameText || MusicManager.Instance == null) return;
        trackNameText.text = MusicManager.Instance.GetCurrentTrackDisplayName();
    }

    public void Show() => gameObject.SetActive(true);
    public void Hide() => gameObject.SetActive(false);
}
