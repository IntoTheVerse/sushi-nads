/*using UnityEngine;
using UnityEngine.UI;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.SceneManagement;
using TMPro;

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
#if USE_SOLANA_SDK
            RefreshButtons();
            if (Web3.Account != null && Web3.Account.PublicKey != null)
            {
                yield break;
            }
            yield return new WaitForSecondsRealtime(step);
            timeout -= step;
#endif
        }
                yield break;
    }

    private void OnEnable()
    {
#if USE_SOLANA_SDK
        Web3.OnLogin += HandleOnLogin;
        Web3.OnLogout += HandleOnLogout;
        Web3.OnWalletChangeState += HandleWalletChange;
#endif
        RefreshButtons();
        // Extra refresh shortly after enable to catch late Web3 session initialization
        InvokeRepeating(nameof(RefreshButtons), 0.1f, 0.5f);
    }

    private void OnDisable()
    {  
        #if USE_SOLANA_SDK
        Web3.OnLogin -= HandleOnLogin;
        Web3.OnLogout -= HandleOnLogout;
        Web3.OnWalletChangeState -= HandleWalletChange;
#endif
        CancelInvoke(nameof(RefreshButtons));
        if (loginButton != null)
        {
            loginButton.onClick.RemoveListener(OnLoginClicked);
        }
    }

#if USE_SOLANA_SDK
    private void HandleOnLogin(Account account)
    {
        RefreshButtons();
    }
#endif

    private void HandleOnLogout()
    {
        RefreshButtons();
    }

    private void RefreshButtons()
    {
        bool isAuthenticated = false;
#if USE_SOLANA_SDK
        try
        {
            isAuthenticated = Web3.Account != null && Web3.Account.PublicKey != null && !string.IsNullOrEmpty(Web3.Account.PublicKey.ToString());
        }
        catch { isAuthenticated = false; }
#endif
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
        #if USE_SOLANA_SDK
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
#endif
#if USE_SOLANA_SDK
        // Fallback: call SDK directly on main thread (do not offload to Task.Run)
#if UNITY_WEBGL && !UNITY_EDITOR
        await Web3.Instance.LoginWalletAdapter();
#else
        if (Web3.Instance.web3AuthWalletOptions != null)
            await Web3.Instance.LoginWeb3Auth(Provider.GOOGLE);
        else
            await Web3.Instance.LoginInGameWallet("kitchen-dash");
#endif
#endif
        RefreshButtons();
    }

    private void QuitToMainMenu()
    {
        // Logout to ensure default UI state without reloading scene
#if USE_SOLANA_SDK
        try
        {
            if (Web3.Instance != null)
            {
                Web3.Instance.Logout();
            }
        }
        catch { }
#endif
        // Refresh buttons after logout
        RefreshButtons();
        // Also schedule another refresh on next frame to catch async state
        Invoke(nameof(RefreshButtons), 0.1f);
    }
}
*/using UnityEngine;
using UnityEngine.UI;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.SceneManagement;
using TMPro;
using UnityEditor.UI;
public class MainMenuUI : MonoBehaviour
{
    [Serializable]
    public class Tab
    {
        public Button button;          // the clickable button
        public GameObject panel;       // the content panel to show
        public Image highlightImage;   // the image you want enabled for the ACTIVE tab
    }

    [SerializeField] private List<Tab> tabs = new();
    [SerializeField] private int defaultIndex = 0;
    [SerializeField] private bool disableSelectedButton = true; // optional UX

    private int currentIndex = -1;

    private void Awake()
    {
        // Auto-fill highlight with the button's Image if left empty
        for (int i = 0; i < tabs.Count; i++)
        {
            var t = tabs[i];
            if (t == null || t.button == null) continue;
            if (t.highlightImage == null) t.highlightImage = t.button.GetComponent<Image>();

            int captured = i; // capture loop var
            t.button.onClick.AddListener(() => Select(captured));
        }
    }

    private void Start()
    {
        Select(Mathf.Clamp(defaultIndex, 0, Mathf.Max(0, tabs.Count - 1)));
    }

    public void Select(int index)
    {
        if (tabs.Count == 0) return;
        if (index < 0 || index >= tabs.Count) index = 0;
        if (currentIndex == index) return;

        currentIndex = index;

        for (int i = 0; i < tabs.Count; i++)
        {
            bool active = (i == index);
            var t = tabs[i];

            if (t.panel) t.panel.SetActive(active);
            if (t.highlightImage) t.highlightImage.enabled = active;
            if (t.button && disableSelectedButton) t.button.interactable = !active;
        }
    }

    // Optional hook if you want to call from a UnityEvent passing the button
    public void SelectByButton(Button clicked)
    {
        int idx = tabs.FindIndex(t => t.button == clicked);
        if (idx >= 0) Select(idx);
    }
}