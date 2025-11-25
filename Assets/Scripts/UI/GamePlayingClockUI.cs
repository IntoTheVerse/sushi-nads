using UnityEngine;
using UnityEngine.UI;
using System;
using TMPro;
using System.Collections;

public class GamePlayingClockUI : MonoBehaviour
{
    [SerializeField] private Image timerImage;
    [SerializeField] private bool hideWhenTimerUnavailable = true;

    private void Awake()
    {
        if (timerImage == null)
        {
            timerImage = GetComponent<Image>();
        }
    }

    private void OnEnable()
    {
        UpdateClock(0f);
    }

    private void Update()
    {
        var manager = KitchenGameManager.Instance;

        if (timerImage == null)
        {
            return;
        }

        if (manager == null)
        {
            UpdateClock(0f);
            if (hideWhenTimerUnavailable && timerImage.enabled)
            {
                timerImage.enabled = false;
            }
            return;
        }

        if (hideWhenTimerUnavailable && !timerImage.enabled)
        {
            timerImage.enabled = true;
        }

        float fill = manager.IsGamePlaying()
            ? Mathf.Clamp01(manager.GetGamePlayingTimerNormalized())
            : 0f;

        UpdateClock(fill);
    }

    private void UpdateClock(float fillAmount)
    {
        if (timerImage != null)
        {
            timerImage.fillAmount = fillAmount;
        }
    }
}
