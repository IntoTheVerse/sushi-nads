using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class MainMenuMusicSettingUI : MonoBehaviour
{
    [SerializeField] private Slider volumeSlider;
    [SerializeField] private Button prevTrackButton;
    [SerializeField] private Button nextTrackButton;
    [SerializeField] private TextMeshProUGUI trackNameText;
    [SerializeField] private Button resumeButton;

    private void Start()
    {
        // Attach Listeners
        if (resumeButton != null)
        {
            resumeButton.onClick.AddListener(Hide);
        }

        if (volumeSlider != null)
        {
            volumeSlider.onValueChanged.AddListener(OnVolumeChanged);
        }

        if (prevTrackButton != null)
        {
            prevTrackButton.onClick.AddListener(OnPrevTrackClicked);
        }

        if (nextTrackButton != null)
        {
            nextTrackButton.onClick.AddListener(OnNextTrackClicked);
        }

        // Initialize UI values
        if (MainMenuMusicManager.Instance != null)
        {
            if (volumeSlider != null)
            {
                volumeSlider.value = MainMenuMusicManager.Instance.GetVolume();
            }
            UpdateTrackNameDisplay();
        }
    }

    private void OnVolumeChanged(float value)
    {
        if (MainMenuMusicManager.Instance != null)
        {
            MainMenuMusicManager.Instance.SetVolume(value);
        }
    }

    private void OnPrevTrackClicked()
    {
        if (MainMenuMusicManager.Instance != null)
        {
            MainMenuMusicManager.Instance.PrevTrack();
            UpdateTrackNameDisplay();
        }
    }

    private void OnNextTrackClicked()
    {
        if (MainMenuMusicManager.Instance != null)
        {
            MainMenuMusicManager.Instance.NextTrack();
            UpdateTrackNameDisplay();
        }
    }

    private void UpdateTrackNameDisplay()
    {
        if (trackNameText != null && MainMenuMusicManager.Instance != null)
        {
            trackNameText.text = MainMenuMusicManager.Instance.GetCurrentTrackDisplayName();
        }
    }

    private void OnEnable()
    {
        // Refresh UI when panel is opened
        if (MainMenuMusicManager.Instance != null)
        {
            if (volumeSlider != null)
            {
                volumeSlider.value = MainMenuMusicManager.Instance.GetVolume();
            }
            UpdateTrackNameDisplay();
        }
    }

    private void Hide()
    {
        gameObject.SetActive(false);
    }

    private void OnDestroy()
    {
        if (resumeButton != null) resumeButton.onClick.RemoveListener(Hide);
        if (volumeSlider != null) volumeSlider.onValueChanged.RemoveListener(OnVolumeChanged);
        if (prevTrackButton != null) prevTrackButton.onClick.RemoveListener(OnPrevTrackClicked);
        if (nextTrackButton != null) nextTrackButton.onClick.RemoveListener(OnNextTrackClicked);
    }
}
