using UnityEngine;
using TMPro;

public class ToastManager : MonoBehaviour
{
    public static ToastManager Instance { get; private set; }

    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private TextMeshProUGUI messageText;
    [SerializeField] private float showDuration = 1.6f;
    [SerializeField] private Vector2 screenPadding = new Vector2(16, 16);
    private enum ToastPlacement { TopCenter, Center, BottomCenter }
    [SerializeField] private ToastPlacement placement = ToastPlacement.TopCenter;
    [SerializeField] private float verticalOffset = 0f; // additional offset from edge/center

    float timer;
    bool showing;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        HideInstant();
    }

    void Update()
    {
        if (!showing) return;
        timer -= Time.unscaledDeltaTime;
        if (timer <= 0f) HideInstant();
    }

    public void Show(string msg, Vector2 screenPos, bool clampToScreen = true)
    {
        messageText.text = msg;
        timer = showDuration;
        showing = true;
        canvasGroup.alpha = 1f;

        var canvas = GetComponentInParent<Canvas>();
        var canvasRT = (RectTransform)canvas.transform;
        var rt = (RectTransform)canvasGroup.transform;

        // Compute canvas/local sizes and toast rect
        var canvasSize = canvasRT.rect.size;
        var toastSize = rt.rect.size;
        var pivot = rt.pivot;

        float halfW = canvasSize.x * 0.5f;
        float halfH = canvasSize.y * 0.5f;

        Vector2 localPoint = Vector2.zero;

        // Static placement: ignore incoming screenPos and position relative to canvas
        switch (placement)
        {
            case ToastPlacement.Center:
                localPoint = new Vector2(0f, verticalOffset);
                break;
            case ToastPlacement.TopCenter:
                localPoint = new Vector2(
                    0f,
                    halfH - screenPadding.y - toastSize.y * (1f - pivot.y) + verticalOffset
                );
                break;
            case ToastPlacement.BottomCenter:
                localPoint = new Vector2(
                    0f,
                    -halfH + screenPadding.y + toastSize.y * pivot.y + verticalOffset
                );
                break;
        }

        // Optionally still clamp to ensure toast remains inside canvas
        if (clampToScreen)
        {
            float minX = -halfW + screenPadding.x + toastSize.x * pivot.x;
            float maxX = halfW - screenPadding.x - toastSize.x * (1f - pivot.x);
            float minY = -halfH + screenPadding.y + toastSize.y * pivot.y;
            float maxY = halfH - screenPadding.y - toastSize.y * (1f - pivot.y);

            localPoint.x = Mathf.Clamp(localPoint.x, minX, maxX);
            localPoint.y = Mathf.Clamp(localPoint.y, minY, maxY);
        }

        // Apply anchored position (canvas-local space)
        rt.anchoredPosition = localPoint;
    }


    public void ShowCentered(string msg)
    {
        var rt = (RectTransform)canvasGroup.transform;
        Show(msg, Vector2.zero, false);
        rt.anchoredPosition = Vector2.zero; // center
    }

    void HideInstant()
    {
        canvasGroup.alpha = 0f;
        showing = false;
    }
}
