using System;
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

    [Header("Misc")]
    [SerializeField] private Button closeButton;

    private void Awake()
    {
        Instance = this;
        sfxSlider.onValueChanged.AddListener(OnSfxChanged);
        musicSlider.onValueChanged.AddListener(OnMusicChanged);
        prevTrackButton.onClick.AddListener(OnPrevTrack);
        nextTrackButton.onClick.AddListener(OnNextTrack);
        closeButton.onClick.AddListener(Hide);
    }

    private void Start()
    {
        KitchenGameManager.Instance.OnGameUnpaused += KitchenGameManager_OnGameUnpaused;

        sfxSlider.SetValueWithoutNotify(SoundManager.Instance.GetVolume());     // 0..1
        musicSlider.SetValueWithoutNotify(MusicManager.Instance.GetVolume());   // 0..1

        UpdateVisual();
        Hide();
    }

    private void OnDestroy()
    {
        if (KitchenGameManager.Instance != null)
            KitchenGameManager.Instance.OnGameUnpaused -= KitchenGameManager_OnGameUnpaused;

        sfxSlider.onValueChanged.RemoveListener(OnSfxChanged);
        musicSlider.onValueChanged.RemoveListener(OnMusicChanged);
        prevTrackButton.onClick.RemoveListener(OnPrevTrack);
        nextTrackButton.onClick.RemoveListener(OnNextTrack);
        closeButton.onClick.RemoveListener(Hide);
    }

    private void KitchenGameManager_OnGameUnpaused(object s, EventArgs e) => Hide();

    private void OnSfxChanged(float v)
    {
        SoundManager.Instance.SetVolume(v);
        if (sfxValueText) sfxValueText.text = Mathf.RoundToInt(v * 10f).ToString();
    }

    private void OnMusicChanged(float v)
    {
        MusicManager.Instance.SetVolume(v);
        if (musicValueText) musicValueText.text = Mathf.RoundToInt(v * 10f).ToString();
    }

    private void OnPrevTrack()
    {
        MusicManager.Instance.PrevTrack();
        UpdateTrackLabel();
    }

    private void OnNextTrack()
    {
        MusicManager.Instance.NextTrack();
        UpdateTrackLabel();
    }

    private void UpdateVisual()
    {
        if (sfxValueText) sfxValueText.text = Mathf.RoundToInt(sfxSlider.value * 10f).ToString();
        if (musicValueText) musicValueText.text = Mathf.RoundToInt(musicSlider.value * 10f).ToString();
        UpdateTrackLabel();
    }

    private void UpdateTrackLabel()
    {
        if (!trackNameText) return;
        trackNameText.text = MusicManager.Instance.GetCurrentTrackDisplayName();
    }

    public void Show() => gameObject.SetActive(true);
    public void Hide() => gameObject.SetActive(false);
}
