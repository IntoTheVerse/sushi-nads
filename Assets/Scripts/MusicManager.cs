using UnityEngine;
using System.Collections.Generic;

public class MusicManager : MonoBehaviour
{
    public static MusicManager Instance { get; private set; }

    [Header("Main Playlist")]
    [SerializeField] private AudioSource musicSource;     // non-spatial
    [SerializeField] private List<AudioClip> playlist = new();
    [SerializeField] private bool playOnStart = true;

    [Header("Special Tracks")]
    [SerializeField] private AudioClip gameScoreMusic;
    [SerializeField] private AudioClip last12SecondsMusic;
    private AudioSource specialMusicSource;

    private const string MUSIC_VOL_KEY = "MUSIC_VOLUME";
    private const string MUSIC_IDX_KEY = "MUSIC_TRACK_INDEX";

    private float musicVolume = 1f;   // 0..1
    private int currentIndex = 0;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        if (!musicSource) musicSource = gameObject.AddComponent<AudioSource>();
        musicSource.loop = true;
        musicSource.playOnAwake = false;
        musicSource.spatialBlend = 0f;

        // Initialize secondary audio source for special music tracks
        specialMusicSource = gameObject.AddComponent<AudioSource>();
        specialMusicSource.loop = false;
        specialMusicSource.playOnAwake = false;
        specialMusicSource.spatialBlend = 0f;

        musicVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(MUSIC_VOL_KEY, 1f));
        currentIndex = Mathf.Clamp(PlayerPrefs.GetInt(MUSIC_IDX_KEY, 0), 0, Mathf.Max(0, playlist.Count - 1));
        
        musicSource.volume = musicVolume;
        specialMusicSource.volume = musicVolume;

        if (playOnStart) PlayCurrent();
    }

    public float GetVolume() => musicVolume;

    public void SetVolume(float v)
    {
        musicVolume = Mathf.Clamp01(v);
        
        if (musicSource != null) musicSource.volume = musicVolume;
        if (specialMusicSource != null) specialMusicSource.volume = musicVolume;
        
        PlayerPrefs.SetFloat(MUSIC_VOL_KEY, musicVolume);
    }

    public void PlayGameScoreMusic()
    {
        if (gameScoreMusic != null)
        {
            if (musicSource != null) musicSource.Stop();
            if (specialMusicSource != null)
            {
                specialMusicSource.clip = gameScoreMusic;
                specialMusicSource.loop = true;
                specialMusicSource.Play();
            }
        }
    }

    public void PauseGameScoreMusic()
    {
        if (specialMusicSource != null && specialMusicSource.clip == gameScoreMusic && specialMusicSource.isPlaying)
        {
            specialMusicSource.Pause();
        }
    }

    public void ResumeGameScoreMusic()
    {
        if (gameScoreMusic != null)
        {
            if (musicSource != null && musicSource.isPlaying) musicSource.Stop();
            if (specialMusicSource != null)
            {
                if (specialMusicSource.clip != gameScoreMusic)
                {
                    specialMusicSource.clip = gameScoreMusic;
                    specialMusicSource.loop = true;
                    specialMusicSource.Play();
                }
                else
                {
                    if (!specialMusicSource.isPlaying) specialMusicSource.UnPause();
                    if (!specialMusicSource.isPlaying) specialMusicSource.Play(); // Fallback
                }
            }
        }
    }

    public void PlayLast12SecondsMusic()
    {
        if (last12SecondsMusic != null)
        {
            if (musicSource != null) musicSource.Stop();
            if (specialMusicSource != null)
            {
                specialMusicSource.clip = last12SecondsMusic;
                specialMusicSource.loop = false;
                specialMusicSource.Play();
            }
        }
    }

    public void NextTrack()
    {
        if (playlist == null || playlist.Count == 0) return;
        currentIndex = (currentIndex + 1) % playlist.Count;
        PlayerPrefs.SetInt(MUSIC_IDX_KEY, currentIndex);
        PlayCurrent();
    }

    public void PrevTrack()
    {
        if (playlist == null || playlist.Count == 0) return;
        currentIndex = (currentIndex - 1 + playlist.Count) % playlist.Count;
        PlayerPrefs.SetInt(MUSIC_IDX_KEY, currentIndex);
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
        if (playlist == null || playlist.Count == 0) return;
        var clip = playlist[currentIndex]; if (!clip) return;
        musicSource.clip = clip;
        musicSource.Play();
    }
}