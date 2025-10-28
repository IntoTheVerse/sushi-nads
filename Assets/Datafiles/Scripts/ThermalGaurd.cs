using UnityEngine;
public class ThermalGuard : MonoBehaviour
{
    public int coolFps = 60;
    public int hotFps = 30;
    public float hotMsThreshold = 22f;   // ~45 fps
    public float cooldownSeconds = 15f;

    float hotTimer;

    void Start()
    {
        QualitySettings.vSyncCount = 0;           // mobile vsync is not reliable
        Application.targetFrameRate = 60;         // try 60 first; use 45 or 30 if needed
                                                  // Physics at 30 Hz = less CPU:
        Time.fixedDeltaTime = 1f / 30f;

        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = coolFps;
    }

    void Update()
    {
        float ms = Time.unscaledDeltaTime * 1000f;

        if (ms > hotMsThreshold) hotTimer += Time.unscaledDeltaTime;
        else hotTimer = Mathf.Max(0, hotTimer - Time.unscaledDeltaTime);

        if (hotTimer > 2f && Application.targetFrameRate != hotFps)
        {
            Application.targetFrameRate = hotFps;                // drop to 30
            UnityEngine.ScalableBufferManager.ResizeBuffers(0.8f, 0.8f);
        }

        if (hotTimer <= 0 && Application.targetFrameRate != coolFps)
        {
            // cool again for a while? restore
            cooldownSeconds -= Time.unscaledDeltaTime;
            if (cooldownSeconds <= 0)
            {
                Application.targetFrameRate = coolFps;
                UnityEngine.ScalableBufferManager.ResizeBuffers(1f, 1f);
                cooldownSeconds = 15f;
            }
        }
    }
}
