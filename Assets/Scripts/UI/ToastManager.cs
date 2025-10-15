using UnityEngine;
using TMPro;

public class ToastManager : MonoBehaviour
{
    public static ToastManager Instance { get; private set; }

    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private TextMeshProUGUI messageText;
    [SerializeField] private float showDuration = 1.6f;
    [SerializeField] private Vector2 screenPadding = new Vector2(16, 16);

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

        // 1) Convert screen -> canvas local
        Vector2 localPoint;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvasRT,
            screenPos,
            canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera,
            out localPoint
        );

        // 2) Optionally clamp inside canvas rect (respect padding & toast size/pivot)
        if (clampToScreen)
        {
            var canvasSize = canvasRT.rect.size;
            var toastSize = rt.rect.size;
            var pivot = rt.pivot;

            float halfW = canvasSize.x * 0.5f;
            float halfH = canvasSize.y * 0.5f;

            float minX = -halfW + screenPadding.x + toastSize.x * pivot.x;
            float maxX = halfW - screenPadding.x - toastSize.x * (1f - pivot.x);
            float minY = -halfH + screenPadding.y + toastSize.y * pivot.y;
            float maxY = halfH - screenPadding.y - toastSize.y * (1f - pivot.y);

            localPoint.x = Mathf.Clamp(localPoint.x, minX, maxX);
            localPoint.y = Mathf.Clamp(localPoint.y, minY, maxY);
        }

        // 3) Apply anchored position (now in canvas-local space)
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
