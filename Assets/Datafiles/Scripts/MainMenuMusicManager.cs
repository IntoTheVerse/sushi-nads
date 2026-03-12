using UnityEngine;

public class MainMenuMusicManager : MonoBehaviour
{
    public static MainMenuMusicManager Instance { get; private set; }

    [SerializeField] private AudioSource musicSource;
    [SerializeField] private AudioClip idleMusic;
    [SerializeField] private AudioClip leaderboardMusic;

    private const string MUSIC_VOL_KEY = "MUSIC_VOLUME";
    private float musicVolume = 1f;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        if (!musicSource) musicSource = gameObject.AddComponent<AudioSource>();
        musicSource.loop = true;
        musicSource.playOnAwake = false;
        musicSource.spatialBlend = 0f;

        musicVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(MUSIC_VOL_KEY, 1f));
        musicSource.volume = musicVolume;

        PlayIdleMusic();
    }

    public float GetVolume() => musicVolume;

    public void SetVolume(float v)
    {
        musicVolume = Mathf.Clamp01(v);
        if (musicSource != null) musicSource.volume = musicVolume;
        PlayerPrefs.SetFloat(MUSIC_VOL_KEY, musicVolume);
    }

    public void PlayIdleMusic()
    {
        if (idleMusic != null && musicSource != null)
        {
            if (musicSource.clip == idleMusic && musicSource.isPlaying) return;
            musicSource.clip = idleMusic;
            musicSource.Play();
        }
    }

    public void PlayLeaderboardMusic()
    {
        if (leaderboardMusic != null && musicSource != null)
        {
            if (musicSource.clip == leaderboardMusic && musicSource.isPlaying) return;
            musicSource.clip = leaderboardMusic;
            musicSource.Play();
        }
    }
}
