using UnityEngine;
using System;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using TMPro;

public class GamePauseUI : MonoBehaviour
{
    public static GamePauseUI instance; 
    [SerializeField] private Button resumeButton;
    [SerializeField] private Button mainMenuButton;
    [SerializeField] private Button optionsButton;
    // [SerializeField] private Button quitButton;

    private void Awake() {
        resumeButton.onClick.AddListener(() => {
            KitchenGameManager.Instance.TogglePauseGame();
        });
        mainMenuButton.onClick.AddListener(OnMainMenuButtonClicked);
        optionsButton.onClick.AddListener(() => {
            OptionsUI.Instance.Show();
        });
    }

    public void Show() {
        gameObject.SetActive(true);
    }

    public void Hide() {
        //KitchenGameManager.Instance.Resume();
        gameObject.SetActive(false);
    }
    private async void OnMainMenuButtonClicked() {
        mainMenuButton.interactable = false;

        // Ensure gameplay is unpaused before scene transition.
        Time.timeScale = 1f;

        try {
            if (SessionManager.Instance != null && SessionManager.Instance.IsSessionActive()) {
                await SessionManager.Instance.EndGameSession();
            }
        }
        catch (Exception e) {
            Debug.LogError($"Failed to end session before returning to main menu: {e.Message}");
        }
        finally {
            Time.timeScale = 1f;
            Loader.Load(Loader.Scene.MainMenuScene);
        }
    }
    private void Start() {
        instance = this;
        KitchenGameManager.Instance.OnGamePaused += KitchenGameManager_OnGamePaused;
        KitchenGameManager.Instance.OnGameUnpaused += KitchenGameManager_OnGameUnpaused;
        Hide();
    }

    private void KitchenGameManager_OnGamePaused(object sender, EventArgs e) {
        Show();
    }   

    private void KitchenGameManager_OnGameUnpaused(object sender, EventArgs e) {
        Hide();
    }
}
