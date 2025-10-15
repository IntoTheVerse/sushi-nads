using UnityEngine;
using UnityEngine.UI;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.SceneManagement;
using TMPro;
using Solana.Unity.SDK;
using Solana.Unity.Wallet;

public class MainMenuUI : MonoBehaviour
{
    [SerializeField] private Button playButton;
    [SerializeField] private Button quitButton;
    [SerializeField] private Button loginButton; // New serialized LOGIN button

    private void Awake() {
        playButton.onClick.AddListener(() => {
            Loader.Load(Loader.Scene.GameScene);
        });
        quitButton.onClick.AddListener(QuitToMainMenu);
        if (loginButton != null)
        {
            loginButton.onClick.AddListener(OnLoginClicked);
        }
        else
        {
            Debug.LogWarning("MainMenuUI: 'loginButton' is not assigned in the Inspector. Add a UI Button to the Main Menu canvas and assign it to show the LOGIN control.");
        }

        Time.timeScale = 1f;
        RefreshButtons();
    }

    private System.Collections.IEnumerator Start()
    {
        // Give Web3 a moment to restore any existing session (e.g., Web3Auth)
        float timeout = 3f;
        float step = 0.1f;
        while (timeout > 0f)
        {
            RefreshButtons();
            if (Web3.Account != null && Web3.Account.PublicKey != null)
            {
                yield break;
            }
            yield return new WaitForSecondsRealtime(step);
            timeout -= step;
        }
    }

    private void OnEnable()
    {
        Web3.OnLogin += HandleOnLogin;
        Web3.OnLogout += HandleOnLogout;
        Web3.OnWalletChangeState += HandleWalletChange;
        RefreshButtons();
        // Extra refresh shortly after enable to catch late Web3 session initialization
        InvokeRepeating(nameof(RefreshButtons), 0.1f, 0.5f);
    }

    private void OnDisable()
    {
        Web3.OnLogin -= HandleOnLogin;
        Web3.OnLogout -= HandleOnLogout;
        Web3.OnWalletChangeState -= HandleWalletChange;
        CancelInvoke(nameof(RefreshButtons));
        if (loginButton != null)
        {
            loginButton.onClick.RemoveListener(OnLoginClicked);
        }
    }

    private void HandleOnLogin(Account account)
    {
        RefreshButtons();
    }

    private void HandleOnLogout()
    {
        RefreshButtons();
    }

    private void RefreshButtons()
    {
        bool isAuthenticated = false;
        try
        {
            isAuthenticated = Web3.Account != null && Web3.Account.PublicKey != null && !string.IsNullOrEmpty(Web3.Account.PublicKey.ToString());
        }
        catch { isAuthenticated = false; }
        if (playButton != null)
        {
            playButton.gameObject.SetActive(isAuthenticated);
            playButton.interactable = isAuthenticated;
        }

        if (loginButton != null)
        {
            loginButton.gameObject.SetActive(!isAuthenticated);
            loginButton.interactable = !isAuthenticated;
        }

        if (quitButton != null)
        {
            quitButton.gameObject.SetActive(true);
        }
    }

    private void HandleWalletChange()
    {
        RefreshButtons();
    }

    private async void OnLoginClicked()
    {
        // Prefer using the dedicated UI helper if present
        var walletUi = FindFirstObjectByType<SolanaWalletUI>();
        if (walletUi != null)
        {
            Debug.Log($"{walletUi.gameObject.name}");
            await walletUi.RequestLoginAsync();
            // Ensure UI refresh happens after await returns
            RefreshButtons();
            return;
        }
        else
        {
            Debug.Log("Could not find SolanawalletUI");
        }

        // Fallback: call SDK directly on main thread (do not offload to Task.Run)
#if UNITY_WEBGL && !UNITY_EDITOR
        await Web3.Instance.LoginWalletAdapter();
#else
        if (Web3.Instance.web3AuthWalletOptions != null)
            await Web3.Instance.LoginWeb3Auth(Provider.GOOGLE);
        else
            await Web3.Instance.LoginInGameWallet("kitchen-dash");
#endif
        RefreshButtons();
    }

    private void QuitToMainMenu()
    {
        // Logout to ensure default UI state without reloading scene
        try
        {
            if (Web3.Instance != null)
            {
                Web3.Instance.Logout();
            }
        }
        catch { }
        // Refresh buttons after logout
        RefreshButtons();
        // Also schedule another refresh on next frame to catch async state
        Invoke(nameof(RefreshButtons), 0.1f);
    }
}
