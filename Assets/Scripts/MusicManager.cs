using TMPro;
using UnityEngine;

public class MusicManager : MonoBehaviour
{
    private const string PLAYER_PREFS_MUSIC_VOLUME = "MusicVolume";
    public static MusicManager Instance { get; private set; }
    public TMP_Text _toggleText;
    [SerializeField] private bool onMeansMuted = true; // set true if "switch ON" == mute
    private const string PLAYER_PREFS_MUTE = "GlobalMute";
    private bool isMuted;
    private AudioSource audioSource;
    private float volume = 0.3f;

    private void Awake() {
        Instance = this;
        audioSource = GetComponent<AudioSource>();
        volume = PlayerPrefs.GetFloat(PLAYER_PREFS_MUSIC_VOLUME, 0.3f);
        audioSource.volume = volume;

        isMuted = PlayerPrefs.GetInt(PLAYER_PREFS_MUTE, 0) == 1;
        ApplyMute(isMuted);
        UpdateToggleText();
    }

    private void Start() {
        audioSource.volume = volume;
    }
    // Add these small helpers anywhere in the class
    private void ApplyMute(bool mute)
    {
        AudioListener.volume = mute ? 0f : 1f;   // mutes the whole game
        if (audioSource) audioSource.mute = mute; // belt & suspenders for music
    }

    private void UpdateToggleText()
    {
        if (_toggleText) _toggleText.text = isMuted ? "OFF" : "ON";
    }

    public void SoundToggle(bool val)
    {
        isMuted = onMeansMuted ? val : !val;
        ApplyMute(isMuted);
        PlayerPrefs.SetInt(PLAYER_PREFS_MUTE, isMuted ? 1 : 0);
        PlayerPrefs.Save();
        UpdateToggleText();

    }

    public void ChangeVolume() {
        volume += 0.1f;
        if (volume > 1f) {
            volume = 0f;
        }
        audioSource.volume = volume;
        PlayerPrefs.SetFloat(PLAYER_PREFS_MUSIC_VOLUME, volume);
        PlayerPrefs.Save();
    }

    public float GetVolume() {
        return volume;
    }
}
