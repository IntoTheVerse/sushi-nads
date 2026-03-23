using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System;

public class DeliveryStatusUI : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private TMPro.TextMeshProUGUI statusText;
    [SerializeField] private UnityEngine.UI.Image bgImage;

    [Header("Animation Settings")]
    [SerializeField] private float displayDuration = 1.0f;
    [SerializeField] private float fadeDuration = 0.2f;

    [Header("Prompt Background Sprites")]
    [SerializeField] private Sprite successSprite;
    [SerializeField] private Sprite errorSprite;
    [SerializeField] private Sprite warningSprite;

    [Header("Screen Shake Options")]
    [SerializeField] private RectTransform screenShakeTarget;
    [SerializeField] private float shakeIntensity = 5f;

    private Coroutine currentAnimationCoroutine;

    private void Start()
    {
        if (statusText != null && bgImage != null)
        {
            statusText.enabled = false;
            bgImage.enabled = false;
            
            // Ensure alphas are 0 to start
            Color textColor = statusText.color;
            textColor.a = 0f;
            statusText.color = textColor;

            Color bgColor = bgImage.color;
            bgColor.a = 0f;
            bgImage.color = bgColor;
        }
        else
        {
            Debug.LogWarning("DeliveryStatusUI requires both a TextMeshProUGUI and an Image component assigned in the Inspector.");
        }

        SubscribeToEvents();
    }

    private void SubscribeToEvents()
    {
        if (DeliveryManager.Instance != null)
        {
            DeliveryManager.Instance.OnRecipeSuccess += DeliveryManager_OnRecipeSuccess;
            DeliveryManager.Instance.OnRecipeFailed += DeliveryManager_OnRecipeFailed;
        }

        StoveCounter.OnAnyStoveFried += StoveCounter_OnAnyStoveFried;
        StoveCounter.OnAnyStoveBurned += StoveCounter_OnAnyStoveBurned;
        StoveCounter.OnAnyStoveWarning += StoveCounter_OnAnyStoveWarning;
        TrashCounter.OnAnyObjectTrashed += TrashCounter_OnAnyObjectTrashed;
    }

    private void UnsubscribeFromEvents()
    {
        if (DeliveryManager.Instance != null)
        {
            DeliveryManager.Instance.OnRecipeSuccess -= DeliveryManager_OnRecipeSuccess;
            DeliveryManager.Instance.OnRecipeFailed -= DeliveryManager_OnRecipeFailed;
        }

        StoveCounter.OnAnyStoveFried -= StoveCounter_OnAnyStoveFried;
        StoveCounter.OnAnyStoveBurned -= StoveCounter_OnAnyStoveBurned;
        StoveCounter.OnAnyStoveWarning -= StoveCounter_OnAnyStoveWarning;
        TrashCounter.OnAnyObjectTrashed -= TrashCounter_OnAnyObjectTrashed;
    }

    private void DeliveryManager_OnRecipeSuccess(object sender, EventArgs e)
    {
        TriggerStatusPopup("PERFECT PLATE!! 🍣", successSprite);
    }
    
    private void DeliveryManager_OnRecipeFailed(object sender, EventArgs e)
    {
        TriggerStatusPopup("ORDER LOST!!", errorSprite);
    }

    private void StoveCounter_OnAnyStoveFried(object sender, EventArgs e)
    {
        TriggerStatusPopup("PERFECT PLATE!! 🍣", successSprite);
    }

    private void StoveCounter_OnAnyStoveBurned(object sender, EventArgs e)
    {
        TriggerStatusPopup("OVERCOOKED!!", errorSprite);
    }

    private void StoveCounter_OnAnyStoveWarning(object sender, EventArgs e)
    {
        TriggerStatusPopup("WATCH THE HEAT!", warningSprite);
    }

    private void TrashCounter_OnAnyObjectTrashed(object sender, EventArgs e)
    {
        TriggerStatusPopup("ORDER LOST!!", errorSprite);
    }

    private void TriggerStatusPopup(string message, Sprite bgSpriteAsset)
    {
        if (statusText == null || bgImage == null) return;

        if (currentAnimationCoroutine != null)
        {
            StopCoroutine(currentAnimationCoroutine);
        }

        currentAnimationCoroutine = StartCoroutine(AnimatePopup(message, bgSpriteAsset));
    }

    private IEnumerator AnimatePopup(string message, Sprite bgSpriteAsset)
    {
        statusText.text = message;
        statusText.enabled = true;

        if (bgSpriteAsset != null)
        {
            bgImage.sprite = bgSpriteAsset;
        }
        bgImage.enabled = true;

        // Reset scale and alpha
        Vector3 initialScale = Vector3.one * 0.5f;
        statusText.transform.localScale = initialScale;
        bgImage.transform.localScale = initialScale;

        Color textColor = statusText.color;
        textColor.a = 0f;
        statusText.color = textColor;

        Color bgColor = bgImage.color;
        bgColor.a = 0f;
        bgImage.color = bgColor;

        // 1. Fade / Pop In
        float timer = 0f;
        while (timer < fadeDuration)
        {
            timer += Time.deltaTime;
            float progress = timer / fadeDuration;

            // Fade in alpha
            float currentAlpha = Mathf.Lerp(0f, 1f, progress);
            textColor.a = currentAlpha;
            statusText.color = textColor;
            bgColor.a = currentAlpha;
            bgImage.color = bgColor;

            // Bounce scale
            float scale = Mathf.Lerp(0.5f, 1.2f, Mathf.Sin(progress * Mathf.PI / 2f)); // Ease out
            Vector3 targetScale = Vector3.one * scale;
            statusText.transform.localScale = targetScale;
            bgImage.transform.localScale = targetScale;

            yield return null;
        }

        // Snap to final loaded state
        textColor.a = 1f;
        statusText.color = textColor;
        bgColor.a = 1f;
        bgImage.color = bgColor;

        statusText.transform.localScale = Vector3.one;
        bgImage.transform.localScale = Vector3.one;

        // 2. Wait (with optional screen shake for errors)
        bool isError = (bgSpriteAsset == errorSprite && errorSprite != null);
        float waitTimer = 0f;
        Vector3 defaultShakePos = screenShakeTarget != null ? screenShakeTarget.localPosition : Vector3.zero;

        while (waitTimer < displayDuration)
        {
            waitTimer += Time.deltaTime;
            
            if (isError && screenShakeTarget != null)
            {
                float shakeOffsetX = UnityEngine.Random.Range(-shakeIntensity, shakeIntensity);
                float shakeOffsetY = UnityEngine.Random.Range(-shakeIntensity, shakeIntensity);
                screenShakeTarget.localPosition = defaultShakePos + new Vector3(shakeOffsetX, shakeOffsetY, 0);
            }
            
            yield return null;
        }

        if (isError && screenShakeTarget != null)
        {
            screenShakeTarget.localPosition = defaultShakePos;
        }

        // 3. Fade Out
        timer = 0f;
        while (timer < fadeDuration)
        {
            timer += Time.deltaTime;
            float progress = timer / fadeDuration;

            // Fade out alpha
            float currentAlpha = Mathf.Lerp(1f, 0f, progress);
            textColor.a = currentAlpha;
            statusText.color = textColor;
            bgColor.a = currentAlpha;
            bgImage.color = bgColor;

            yield return null;
        }

        // Hide completely
        statusText.enabled = false;
        bgImage.enabled = false;
        currentAnimationCoroutine = null;
    }

    private void OnDestroy()
    {
        UnsubscribeFromEvents();
    }
}
