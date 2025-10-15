using UnityEngine;
using TMPro;
using System;
using System.Collections;
using System.Collections.Generic;

public class GameOverUI : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI recipesDeliveredText;

    
    private void Start() {
        if (KitchenGameManager.Instance != null) {
            KitchenGameManager.Instance.OnStateChanged += KitchenGameManager_OnStateChanged;
        } else {
            Debug.LogError("KitchenGameManager.Instance is null in GameStartCountdownUI.Start()");
        }
        Hide();
    }


    private void KitchenGameManager_OnStateChanged(object sender, System.EventArgs e) {
        if (KitchenGameManager.Instance.IsGameOver()) {
            Show();
            recipesDeliveredText.text = DeliveryManager.Instance.GetSuccessfulRecipesAmount().ToString();
        } else {
            Hide();
        }
    }


    private void Show() {
        gameObject.SetActive(true);
    }

    private void Hide() {
        gameObject.SetActive(false);
    }
}
