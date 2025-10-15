using UnityEngine;

public class TimerPulse : MonoBehaviour
{
    [Header("Ranges (start -> end as time runs out)")]
    [SerializeField] float minScaleStart = 0.90f;
    [SerializeField] float minScaleEnd = 0.85f;
    [SerializeField] float maxScaleStart = 1.15f;
    [SerializeField] float maxScaleEnd = 1.30f;
    [SerializeField] float speedStart = 2.0f;   // pulses per second at start
    [SerializeField] float speedEnd = 5.5f;   // pulses per second near the end
    [SerializeField] bool useUnscaled = true;
    [SerializeField] RectTransform target;         // assign; defaults to self

    // smoothing toward targets
    [SerializeField] float smooth = 8f;            // higher = snappier

    float minCur, maxCur, spdCur;
    float minTgt, maxTgt, spdTgt;
    float phase;
    bool active;

    void Awake() { if (!target) target = GetComponent<RectTransform>(); }

    void Update()
    {
        if (!active) return;

        float dt = useUnscaled ? Time.unscaledDeltaTime : Time.deltaTime;
        float k = 1f - Mathf.Exp(-smooth * dt);      // stable exponential smoothing

        // ease current values toward targets
        minCur = Mathf.Lerp(minCur, minTgt, k);
        maxCur = Mathf.Lerp(maxCur, maxTgt, k);
        spdCur = Mathf.Lerp(spdCur, spdTgt, k);

        // oscillate 0..1
        phase += spdCur * dt * Mathf.PI * 2f;
        float s = 0.5f + 0.5f * Mathf.Sin(phase);

        float scale = Mathf.Lerp(minCur, maxCur, s);
        target.localScale = Vector3.one * scale;
    }

    public void StartPulse()
    {
        active = true;
        // initialize to "start" settings
        minCur = minTgt = minScaleStart;
        maxCur = maxTgt = maxScaleStart;
        spdCur = spdTgt = speedStart;
        // keep current phase so it looks continuous
    }

    public void StopPulse()
    {
        active = false;
        target.localScale = Vector3.one;
    }

    /// progressNormalized: 0 = just started, 1 = almost done
    public void UpdateUrgency(float progressNormalized)
    {
        float t = Mathf.Clamp01(progressNormalized);
        minTgt = Mathf.Lerp(minScaleStart, minScaleEnd, t);
        maxTgt = Mathf.Lerp(maxScaleStart, maxScaleEnd, t);
        spdTgt = Mathf.Lerp(speedStart, speedEnd, t);
    }
}
