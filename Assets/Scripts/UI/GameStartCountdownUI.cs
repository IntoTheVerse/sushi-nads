using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;


public class GameStartCountdownUI : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI countdownText;

    private void Start() {
        if (KitchenGameManager.Instance != null) {
            KitchenGameManager.Instance.OnStateChanged += KitchenGameManager_OnStateChanged;
        } else {
            Debug.LogError("KitchenGameManager.Instance is null in GameStartCountdownUI.Start()");
        }
        Hide();
    }


    private void KitchenGameManager_OnStateChanged(object sender, System.EventArgs e) {
        if (KitchenGameManager.Instance.IsCountdownToStartActive()) {
            Show();
        } else {
            Hide();
        }
    }

    private void Update() {
        // if (KitchenGameManager.Instance.IsCountdownToStartActive()) {
        //     countdownText.text = Mathf.Ceil(KitchenGameManager.Instance.GetCountdownToStartTimer()).ToString();
        // }
        if (KitchenGameManager.Instance != null) {
            countdownText.text = Mathf.Ceil(KitchenGameManager.Instance.GetCountdownToStartTimer()).ToString();
        }
    }

    private void Show() {
        gameObject.SetActive(true);
    }

    private void Hide() {
        gameObject.SetActive(false);
    }
}
