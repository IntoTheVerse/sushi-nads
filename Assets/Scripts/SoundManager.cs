using UnityEngine;
using System;

public class SoundManager : MonoBehaviour
{
    public static SoundManager Instance { get; private set; }

    [SerializeField] private AudioClipRefsSO audioClipRefsSO;

    private const string SFX_VOL_KEY = "SFX_VOLUME";
    private float sfxVolume = 1f; // 0..1

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        sfxVolume = PlayerPrefs.GetFloat(SFX_VOL_KEY, 1f);
    }

    private void Start()
    {
        DeliveryManager.Instance.OnRecipeSuccess += (_, __) =>
            PlaySound(audioClipRefsSO.deliverySuccess, DeliveryCounter.Instance.transform.position);
        DeliveryManager.Instance.OnRecipeFailed += (_, __) =>
            PlaySound(audioClipRefsSO.deliveryFail, DeliveryCounter.Instance.transform.position);
        CuttingCounter.OnAnyCut += (s, __) =>
            PlaySound(audioClipRefsSO.chop, ((CuttingCounter)s).transform.position);
        Player.Instance.OnPickedSomething += (s, __) =>
            PlaySound(audioClipRefsSO.objectPickup, ((Player)s).transform.position);
        BaseCounter.OnAnyObjectPlacedHere += (s, __) =>
            PlaySound(audioClipRefsSO.objectDrop, ((BaseCounter)s).transform.position);
        TrashCounter.OnAnyObjectTrashed += (s, __) =>
            PlaySound(audioClipRefsSO.trash, ((TrashCounter)s).transform.position);
    }

    private void PlaySound(AudioClip clip, Vector3 pos, float mult = 1f)
    {
        AudioSource.PlayClipAtPoint(clip, pos, mult * sfxVolume);
    }
    private void PlaySound(AudioClip[] clips, Vector3 pos, float mult = 1f)
    {
        PlaySound(clips[UnityEngine.Random.Range(0, clips.Length)], pos, mult);
    }

    public float GetVolume() => sfxVolume;

    public void SetVolume(float v)
    {
        sfxVolume = Mathf.Clamp01(v);
        PlayerPrefs.SetFloat(SFX_VOL_KEY, sfxVolume);
    }

    // If you still use the old cyclic button:
    public void ChangeVolume()
    {
        float v = sfxVolume + 0.1f; if (v > 1f) v = 0f; SetVolume(v);
    }
}
