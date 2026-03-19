using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System;

public class DeliveryStatusUI : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private Image checkmarkImage;

    [Header("Animation Settings")]
    [SerializeField] private float displayDuration = 1.0f;
    [SerializeField] private float fadeDuration = 0.2f;

    private Coroutine currentAnimationCoroutine;

    private void Start()
    {
        if (checkmarkImage != null)
        {
            checkmarkImage.enabled = false;
            
            // Ensure alpha is 0 to start
            Color c = checkmarkImage.color;
            c.a = 0f;
            checkmarkImage.color = c;
        }
        else
        {
            Debug.LogWarning("DeliveryStatusUI requires a checkmark Image component to be assigned in the Inspector.");
        }

        SubscribeToEvents();
    }

    private void SubscribeToEvents()
    {
        // 1. Listen for successful deliveries
        if (DeliveryManager.Instance != null)
        {
            DeliveryManager.Instance.OnRecipeSuccess += DeliveryManager_OnRecipeSuccess;
        }

        // 2. Listen for fully cooked items on the stove
        StoveCounter.OnAnyStoveFried += StoveCounter_OnAnyStoveFried;

        // 3. Listen for discarded items in the trash
        TrashCounter.OnAnyObjectTrashed += TrashCounter_OnAnyObjectTrashed;
    }

    private void UnsubscribeFromEvents()
    {
        if (DeliveryManager.Instance != null)
        {
            DeliveryManager.Instance.OnRecipeSuccess -= DeliveryManager_OnRecipeSuccess;
        }

        StoveCounter.OnAnyStoveFried -= StoveCounter_OnAnyStoveFried;
        TrashCounter.OnAnyObjectTrashed -= TrashCounter_OnAnyObjectTrashed;
    }

    private void DeliveryManager_OnRecipeSuccess(object sender, EventArgs e)
    {
        TriggerStatusPopup();
    }

    private void StoveCounter_OnAnyStoveFried(object sender, EventArgs e)
    {
        TriggerStatusPopup();
    }

    private void TrashCounter_OnAnyObjectTrashed(object sender, EventArgs e)
    {
        TriggerStatusPopup();
    }

    private void TriggerStatusPopup()
    {
        if (checkmarkImage == null) return;

        if (currentAnimationCoroutine != null)
        {
            StopCoroutine(currentAnimationCoroutine);
        }

        currentAnimationCoroutine = StartCoroutine(AnimateCheckmark());
    }

    private IEnumerator AnimateCheckmark()
    {
        checkmarkImage.enabled = true;

        // Reset scale and alpha
        checkmarkImage.transform.localScale = Vector3.one * 0.5f; // Start a bit smaller
        Color c = checkmarkImage.color;
        c.a = 0f;
        checkmarkImage.color = c;

        // 1. Fade / Pop In
        float timer = 0f;
        while (timer < fadeDuration)
        {
            timer += Time.deltaTime;
            float progress = timer / fadeDuration;

            // Fade in
            c.a = Mathf.Lerp(0f, 1f, progress);
            checkmarkImage.color = c;

            // Bounce scale
            float scale = Mathf.Lerp(0.5f, 1.2f, Mathf.Sin(progress * Mathf.PI / 2f)); // Ease out
            checkmarkImage.transform.localScale = Vector3.one * scale;

            yield return null;
        }

        // Snap to final loaded state
        c.a = 1f;
        checkmarkImage.color = c;
        checkmarkImage.transform.localScale = Vector3.one;

        // 2. Wait
        yield return new WaitForSeconds(displayDuration);

        // 3. Fade Out
        timer = 0f;
        while (timer < fadeDuration)
        {
            timer += Time.deltaTime;
            float progress = timer / fadeDuration;

            // Fade out
            c.a = Mathf.Lerp(1f, 0f, progress);
            checkmarkImage.color = c;

            yield return null;
        }

        // Hide completely
        checkmarkImage.enabled = false;
        currentAnimationCoroutine = null;
    }

    private void OnDestroy()
    {
        UnsubscribeFromEvents();
    }
}
