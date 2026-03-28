using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class MainMenuMusicManager : MonoBehaviour
{
    public static MainMenuMusicManager Instance { get; private set; }

    [Header("Main Playlist")]
    [SerializeField] private List<AudioClip> playlist = new();
    [SerializeField] private bool shufflePlaylist = false;

    [Header("Crossfade")]
    [SerializeField] [Range(0.5f, 5f)] private float crossfadeDuration = 2f;

    private const string MUSIC_VOL_KEY        = "MUSIC_VOLUME";
    private const string MAIN_MENU_IDX_KEY    = "MAIN_MENU_MUSIC_TRACK_INDEX";

    // Two AudioSources that ping-pong
    private AudioSource[] sources = new AudioSource[2];
    private int activeSrc = 0;

    private float musicVolume = 1f;
    private int currentIndex = 0;
    private bool isCrossfading = false;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        for (int i = 0; i < 2; i++)
        {
            sources[i] = gameObject.AddComponent<AudioSource>();
            sources[i].loop = false;
            sources[i].playOnAwake = false;
            sources[i].spatialBlend = 0f;
        }

        musicVolume  = Mathf.Clamp01(PlayerPrefs.GetFloat(MUSIC_VOL_KEY, 1f));
        currentIndex = Mathf.Clamp(PlayerPrefs.GetInt(MAIN_MENU_IDX_KEY, 0),
                                   0, Mathf.Max(0, playlist.Count - 1));

        sources[0].volume = musicVolume;
        sources[1].volume = 0f;

        PlayCurrent();
    }

    private void Update()
    {
        if (isCrossfading || playlist.Count == 0) return;

        AudioSource active = sources[activeSrc];
        if (active.clip == null || !active.isPlaying) return;

        float remaining = active.clip.length - active.time;
        if (remaining <= crossfadeDuration)
        {
            AdvanceIndex();
            StartCoroutine(CrossfadeToNext(playlist[currentIndex]));
        }
    }

    private IEnumerator CrossfadeToNext(AudioClip nextClip)
    {
        if (nextClip == null) yield break;
        isCrossfading = true;

        int incoming = 1 - activeSrc;
        AudioSource fadeOut = sources[activeSrc];
        AudioSource fadeIn  = sources[incoming];

        fadeIn.clip   = nextClip;
        fadeIn.volume = 0f;
        fadeIn.Play();

        float elapsed  = 0f;
        float startVol = fadeOut.volume;

        while (elapsed < crossfadeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / crossfadeDuration);
            fadeOut.volume = Mathf.Lerp(startVol, 0f,          t);
            fadeIn.volume  = Mathf.Lerp(0f,       musicVolume, t);
            yield return null;
        }

        fadeOut.Stop();
        fadeOut.volume = 0f;
        activeSrc      = incoming;
        isCrossfading  = false;
        PlayerPrefs.SetInt(MAIN_MENU_IDX_KEY, currentIndex);
    }

    private void AdvanceIndex()
    {
        if (shufflePlaylist)
        {
            int next;
            do { next = Random.Range(0, playlist.Count); }
            while (playlist.Count > 1 && next == currentIndex);
            currentIndex = next;
        }
        else
        {
            currentIndex = (currentIndex + 1) % playlist.Count;
        }
    }

    private void PlayCurrent()
    {
        if (playlist == null || playlist.Count == 0) return;
        var clip = playlist[currentIndex];
        if (!clip) return;
        // Don't restart if already playing the same clip
        if (sources[activeSrc].clip == clip && sources[activeSrc].isPlaying) return;
        sources[activeSrc].clip   = clip;
        sources[activeSrc].volume = musicVolume;
        sources[activeSrc].Play();
    }

    // ── Public API ──

    public float GetVolume() => musicVolume;

    public void SetVolume(float v)
    {
        musicVolume = Mathf.Clamp01(v);
        sources[activeSrc].volume = musicVolume;
        PlayerPrefs.SetFloat(MUSIC_VOL_KEY, musicVolume);
    }

    public void NextTrack()
    {
        if (playlist == null || playlist.Count == 0) return;
        AdvanceIndex();
        StopAllCoroutines();
        isCrossfading = false;
        sources[1 - activeSrc].Stop();
        foreach (var s in sources) s.volume = 0f;
        sources[activeSrc].volume = musicVolume;
        PlayCurrent();
        PlayerPrefs.SetInt(MAIN_MENU_IDX_KEY, currentIndex);
    }

    public void PrevTrack()
    {
        if (playlist == null || playlist.Count == 0) return;
        currentIndex = (currentIndex - 1 + playlist.Count) % playlist.Count;
        StopAllCoroutines();
        isCrossfading = false;
        sources[1 - activeSrc].Stop();
        foreach (var s in sources) s.volume = 0f;
        sources[activeSrc].volume = musicVolume;
        PlayCurrent();
        PlayerPrefs.SetInt(MAIN_MENU_IDX_KEY, currentIndex);
    }

    public int    GetCurrentTrackIndex()       => currentIndex;

    public string GetCurrentTrackDisplayName()
    {
        if (playlist == null || playlist.Count == 0) return "No Music";
        var clip = playlist[currentIndex];
        return clip ? clip.name : $"Music {currentIndex + 1}";
    }
}
