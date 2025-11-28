using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class CounterHighlighter : MonoBehaviour
{
    [Tooltip("The counter this highlighter belongs to")]
    [SerializeField] private BaseCounter baseCounter;

    [Tooltip("Optional transform to pulse/scale for the highlight. If empty, the counter GameObject is used.")]
    [SerializeField] private Transform highlightTransform;

    [Header("Scale Pulse")]
    [SerializeField] private bool useScalePulse = true;
    [SerializeField] private float minScale = 0.9f;
    [SerializeField] private float maxScale = 1.12f;
    [SerializeField] private float pulseSpeed = 2.5f; // pulses per second

    [Header("Alternate Visuals")]
    [SerializeField] private GameObject[] visualGameObjects; // things to toggle on/off for highlight (glow, outline, etc.)

    private Coroutine pulseCoroutine;
    private Vector3 originalScale;

    private void Awake()
    {
        if (highlightTransform == null) highlightTransform = transform;
        originalScale = highlightTransform.localScale;
    }

    private void OnEnable()
    {
        // ensure visuals start hidden
        SetVisualsActive(false);
    }

    private void OnDisable()
    {
        StopPulse();
        SetVisualsActive(false);
    }

    public BaseCounter GetCounter() => baseCounter;

    public void ShowHighlight()
    {
        SetVisualsActive(true);
        if (useScalePulse)
        {
            StartPulse();
        }
    }

    public void HideHighlight()
    {
        StopPulse();
        SetVisualsActive(false);
        // restore scale
        if (highlightTransform != null)
            highlightTransform.localScale = originalScale;
    }

    private void SetVisualsActive(bool active)
    {
        if (visualGameObjects == null) return;
        foreach (var go in visualGameObjects)
        {
            if (go == null) continue;
            go.SetActive(active);
        }
    }

    private void StartPulse()
    {
        if (pulseCoroutine != null) return;
        pulseCoroutine = StartCoroutine(PulseCoroutine());
    }

    private void StopPulse()
    {
        if (pulseCoroutine != null)
        {
            StopCoroutine(pulseCoroutine);
            pulseCoroutine = null;
        }
    }

    private IEnumerator PulseCoroutine()
    {
        float phase = 0f;
        while (true)
        {
            phase += pulseSpeed * Time.deltaTime * Mathf.PI * 2f;
            float s = 0.5f + 0.5f * Mathf.Sin(phase);
            float scale = Mathf.Lerp(minScale, maxScale, s);
            if (highlightTransform != null)
                highlightTransform.localScale = originalScale * scale;
            yield return null;
        }
    }
}
