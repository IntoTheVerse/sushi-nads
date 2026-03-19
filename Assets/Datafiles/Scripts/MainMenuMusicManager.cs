using UnityEngine;
using System.Collections.Generic;

public class MainMenuMusicManager : MonoBehaviour
{
    public static MainMenuMusicManager Instance { get; private set; }

    [SerializeField] private AudioSource musicSource;
    [SerializeField] private List<AudioClip> playlist = new();

    private const string MUSIC_VOL_KEY = "MUSIC_VOLUME";
    private const string MAIN_MENU_MUSIC_IDX_KEY = "MAIN_MENU_MUSIC_TRACK_INDEX";
    
    private float musicVolume = 1f;
    private int currentIndex = 0;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        if (!musicSource) musicSource = gameObject.AddComponent<AudioSource>();
        musicSource.loop = true;
        musicSource.playOnAwake = false;
        musicSource.spatialBlend = 0f;

        musicVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(MUSIC_VOL_KEY, 1f));
        currentIndex = Mathf.Clamp(PlayerPrefs.GetInt(MAIN_MENU_MUSIC_IDX_KEY, 0), 0, Mathf.Max(0, playlist.Count - 1));
        
        musicSource.volume = musicVolume;

        PlayCurrent();
    }

    public float GetVolume() => musicVolume;

    public void SetVolume(float v)
    {
        musicVolume = Mathf.Clamp01(v);
        if (musicSource != null) musicSource.volume = musicVolume;
        PlayerPrefs.SetFloat(MUSIC_VOL_KEY, musicVolume);
    }

    public void NextTrack()
    {
        if (playlist == null || playlist.Count == 0) return;
        currentIndex = (currentIndex + 1) % playlist.Count;
        PlayerPrefs.SetInt(MAIN_MENU_MUSIC_IDX_KEY, currentIndex);
        PlayCurrent();
    }

    public void PrevTrack()
    {
        if (playlist == null || playlist.Count == 0) return;
        currentIndex = (currentIndex - 1 + playlist.Count) % playlist.Count;
        PlayerPrefs.SetInt(MAIN_MENU_MUSIC_IDX_KEY, currentIndex);
        PlayCurrent();
    }

    public int GetCurrentTrackIndex() => currentIndex;

    public string GetCurrentTrackDisplayName()
    {
        if (playlist == null || playlist.Count == 0) return "No Music";
        var clip = playlist[currentIndex];
        return clip ? clip.name : $"Music {currentIndex + 1}";
    }

    private void PlayCurrent()
    {
        if (playlist == null || playlist.Count == 0 || musicSource == null) return;
        var clip = playlist[currentIndex]; 
        if (!clip) return;
        
        // Prevent restarting the same song if it's already playing
        if (musicSource.clip == clip && musicSource.isPlaying) return;

        musicSource.clip = clip;
        musicSource.Play();
    }
}
