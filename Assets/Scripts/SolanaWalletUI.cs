using System;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Solana.Unity.SDK;
using Solana.Unity.Wallet;

/// <summary>
/// Minimal runtime UI to connect/disconnect a Solana wallet from MainMenuScene.
/// Creates a Canvas + Panel with buttons and a label showing the connected public key.
/// </summary>
[DefaultExecutionOrder(-1000)]
public class SolanaWalletUI : MonoBehaviour
{
    [Header("Optional RPC Override (leave empty for SDK default)")]
    [SerializeField] private string customRpcUrl = string.Empty;
    [SerializeField] private bool enableDevKeyLogging = false;

    private Canvas rootCanvas;
    private Text publicKeyLabel;
    private Button disconnectButton;
    private Button copyButton;
    private GameObject panelRoot;

    private const string TargetSceneName = "MainMenuScene";
    private const string DevnetRpcUrl = "https://api.devnet.solana.com";
    private const string LocalKeystoreKey = "EncryptedKeystore";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (FindFirstObjectByType<SolanaWalletUI>() != null) return;
        var go = new GameObject("SolanaWalletUI");
        DontDestroyOnLoad(go);
        go.AddComponent<SolanaWalletUI>();
    }

    private void Awake()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        enableDevKeyLogging = true;
#endif
        EnsureEventSystem();
        EnsureWeb3Singleton();
        ApplyOptionalRpcOverrides();
        BuildUi();

        Web3.OnLogin += HandleOnLogin;
        Web3.OnLogout += HandleOnLogout;
        Web3.OnWalletChangeState += HandleWalletChangeState;

        // Only visible on MainMenuScene
        rootCanvas.gameObject.SetActive(SceneManager.GetActiveScene().name == TargetSceneName);
        RefreshUiState();
    }

    private void OnEnable()
    {
        SceneManager.activeSceneChanged += OnActiveSceneChanged;
    }

    private void OnDisable()
    {
        SceneManager.activeSceneChanged -= OnActiveSceneChanged;
    }

    private void OnDestroy()
    {
        Web3.OnLogin -= HandleOnLogin;
        Web3.OnLogout -= HandleOnLogout;
        Web3.OnWalletChangeState -= HandleWalletChangeState;
    }

    private void EnsureEventSystem()
    {
        if (FindFirstObjectByType<EventSystem>() == null)
        {
            var es = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            DontDestroyOnLoad(es);
        }
    }

    // private void EnsureWeb3Singleton()
    // {
    //     // Ensure a Web3 MonoBehaviour exists in the scene if the SDK expects one
    //     if (Web3.Instance == null)
    //     {
    //         var web3Go = new GameObject("Web3");
    //         web3Go.AddComponent<Web3>();
    //         DontDestroyOnLoad(web3Go);
    //         // Initialize default options to avoid null usage inside SDK constructors
    //         if (Web3.Instance.web3AuthWalletOptions == null)
    //         {
    //             Web3.Instance.web3AuthWalletOptions = new Web3AuthWalletOptions();
    //         }
    //     }
    // }

    private void EnsureWeb3Singleton()
    {
        if (Web3.Instance == null)
        {
            var web3Go = new GameObject("Web3");
            web3Go.AddComponent<Web3>();
            DontDestroyOnLoad(web3Go);
        }

#if UNITY_ANDROID
        var redirect = "torusapp://com.torus.web3authunity"; // Android package id as host
#else
        var redirect = "torusapp://com.torus.web3authunity";
#endif

        Web3.Instance.web3AuthWalletOptions = new Web3AuthWalletOptions
        {
            clientId    = "BEh8yBk4DgRklugjd57od7cYNbiM5sgr9u_r-d93zYAZQ0jYX6mFy1ol_FnKKiDhRXpmMpqWvMwlCdWFnhq69GI",
            network     = Web3Auth.Network.SAPPHIRE_DEVNET,
            redirectUrl = redirect
        };

        if (string.IsNullOrEmpty(Web3.Instance.customRpc))
        {
            Web3.Instance.customRpc = DevnetRpcUrl;
        }

        Web3.Instance.rpcCluster = RpcCluster.DevNet; // Solana devnet
    }


    private void ApplyOptionalRpcOverrides()
    {
        // Force Devnet RPC unless explicitly overridden
        var rpcToUse = string.IsNullOrWhiteSpace(customRpcUrl) ? DevnetRpcUrl : customRpcUrl;
        Web3.Instance.rpcCluster = RpcCluster.DevNet;
        Web3.Instance.customRpc = rpcToUse;
        // Derive ws endpoint if possible
        try
        {
            if (Uri.TryCreate(rpcToUse, UriKind.Absolute, out var uri))
            {
                var ws = uri.Scheme == "https" ? new UriBuilder(uri) { Scheme = "wss", Port = -1 }.Uri.ToString() : null;
                if (!string.IsNullOrEmpty(ws)) Web3.Instance.webSocketsRpc = ws;
            }
        }
        catch { }
    }

    private void BuildUi()
    {
        // Root canvas
        var canvasGo = new GameObject("WalletUICanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        DontDestroyOnLoad(canvasGo);
        rootCanvas = canvasGo.GetComponent<Canvas>();
        rootCanvas.renderMode = RenderMode.ScreenSpaceOverlay;

        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);

        // Panel (bottom-right)
        var panelGo = new GameObject("WalletPanel", typeof(Image));
        panelGo.transform.SetParent(canvasGo.transform, false);
        panelRoot = panelGo;
        var panelImg = panelGo.GetComponent<Image>();
        panelImg.color = new Color(0f, 0f, 0f, 0.5f);

        var panelRect = panelGo.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(1f, 0f);
        panelRect.anchorMax = new Vector2(1f, 0f);
        panelRect.pivot = new Vector2(1f, 0f);
        panelRect.anchoredPosition = new Vector2(-20f, 20f);
        panelRect.sizeDelta = new Vector2(560f, 120f);

        // Public key label
        publicKeyLabel = CreateText(panelGo.transform, "PublicKeyLabel", "Not connected", 16, new Vector2(-12f, 12f), new Vector2(536f, 40f));
        var pkRt = publicKeyLabel.GetComponent<RectTransform>();
        pkRt.anchorMin = new Vector2(1f, 0f);
        pkRt.anchorMax = new Vector2(1f, 0f);
        pkRt.pivot = new Vector2(1f, 0f);

        // Buttons row (right-aligned)
        disconnectButton = CreateButton(panelGo.transform, "DisconnectButton", "Disconnect", new Vector2(-352f, 64f), new Vector2(160f, 44f));
        var dRt = disconnectButton.GetComponent<RectTransform>();
        dRt.anchorMin = new Vector2(1f, 0f);
        dRt.anchorMax = new Vector2(1f, 0f);
        dRt.pivot = new Vector2(1f, 0f);
        copyButton = CreateButton(panelGo.transform, "CopyButton", "Copy Address", new Vector2(-180f, 64f), new Vector2(160f, 44f));
        var cRt = copyButton.GetComponent<RectTransform>();
        cRt.anchorMin = new Vector2(1f, 0f);
        cRt.anchorMax = new Vector2(1f, 0f);
        cRt.pivot = new Vector2(1f, 0f);

        disconnectButton.onClick.AddListener(Disconnect);
        copyButton.onClick.AddListener(CopyAddress);
    }

    private void OnActiveSceneChanged(Scene current, Scene next)
    {
        if (rootCanvas != null)
        {
            rootCanvas.gameObject.SetActive(next.name == TargetSceneName);
        }
    }

    private Text CreateText(Transform parent, string name, string content, int fontSize, Vector2 anchoredPos, Vector2 size)
    {
        var go = new GameObject(name, typeof(Text));
        go.transform.SetParent(parent, false);
        var txt = go.GetComponent<Text>();
        txt.text = content;
        txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        txt.fontSize = fontSize;
        txt.color = Color.white;
        txt.alignment = TextAnchor.MiddleLeft;

        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = size;

        return txt;
    }

    private Button CreateButton(Transform parent, string name, string label, Vector2 anchoredPos, Vector2 size)
    {
        var go = new GameObject(name, typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);

        var img = go.GetComponent<Image>();
        img.color = new Color(1f, 1f, 1f, 0.9f);

        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = size;

        var textGo = new GameObject("Text", typeof(Text));
        textGo.transform.SetParent(go.transform, false);
        var txt = textGo.GetComponent<Text>();
        txt.text = label;
        txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        txt.fontSize = 16;
        txt.color = Color.black;
        txt.alignment = TextAnchor.MiddleCenter;

        var textRt = textGo.GetComponent<RectTransform>();
        textRt.anchorMin = Vector2.zero;
        textRt.anchorMax = Vector2.one;
        textRt.offsetMin = Vector2.zero;
        textRt.offsetMax = Vector2.zero;

        return go.GetComponent<Button>();
    }

    private void HandleOnLogin(Account account)
    {
        RefreshUiState();
        DevLogKeys(account);
        LogLamportsAsync();
    }

    private void HandleOnLogout()
    {
        RefreshUiState();
    }

    private void HandleWalletChangeState()
    {
        RefreshUiState();
    }

    private void RefreshUiState()
    {
        bool isLoggedIn = Web3.Account != null && !string.IsNullOrEmpty(Web3.Account.PublicKey);
        publicKeyLabel.text = isLoggedIn ? $"Public Key: {Web3.Account.PublicKey}" : "Not connected";
        if (panelRoot != null) panelRoot.SetActive(isLoggedIn);
        disconnectButton.interactable = isLoggedIn;
        copyButton.interactable = isLoggedIn;
    }

    // Expose methods to trigger login from external UI (e.g., MainMenuUI)
    public void RequestLogin()
    {
        _ = Connect();
    }

    public Task RequestLoginAsync()
    {
        return Connect();
    }

    private void DevLogKeys(Account account)
    {
        if (!enableDevKeyLogging) return;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        try
        {
            var pub = Web3.Account?.PublicKey?.ToString();
            var priv = Web3.Account?.PrivateKey?.ToString();
            Debug.Log($"[Wallet Debug] Public Key: {pub}");
            Debug.Log($"[Wallet Debug] Private Key: {priv}");
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[Wallet Debug] Failed to log keys: {e.Message}");
        }
#endif
    }

    private async Task Connect()
    {
        try
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            await Web3.Instance.LoginWalletAdapter();
#else
            bool loggedIn = false;
            // Try Web3Auth only if options are present
            if (Web3.Instance.web3AuthWalletOptions != null)
            {
                try
                {
                    var acc = await Web3.Instance.LoginWeb3Auth(Provider.GOOGLE);
                    loggedIn = acc != null;
                }
                catch (Exception)
                {
                    loggedIn = false;
                }
            }

            if (!loggedIn)
            {
                // Fallback to In-Game wallet: create if missing, else login
                var hasLocal = PlayerPrefs.HasKey(LocalKeystoreKey) && !string.IsNullOrEmpty(PlayerPrefs.GetString(LocalKeystoreKey));
                if (!hasLocal)
                {
                    var created = await Web3.Instance.CreateAccount(null, "kitchen-dash");
                    loggedIn = created != null;
                }
                if (!loggedIn)
                {
                    var acc = await Web3.Instance.LoginInGameWallet("kitchen-dash");
                    loggedIn = acc != null;
                }
            }
#endif
        }
        catch (Exception e)
        {
            Debug.LogError($"Wallet connect failed: {e}");
        }
    }

    private void Disconnect()
    {
        try
        {
            Web3.Instance.Logout();
        }
        catch (Exception e)
        {
            Debug.LogError($"Wallet disconnect failed: {e}");
        }
    }

    private void CopyAddress()
    {
        if (Web3.Account == null || string.IsNullOrEmpty(Web3.Account.PublicKey))
        {
            return;
        }

        GUIUtility.systemCopyBuffer = Web3.Account.PublicKey;
        Debug.Log("Copied public key to clipboard.");
    }

    private async void LogLamportsAsync()
    {
        try
        {
            if (Web3.Account == null || Web3.Rpc == null) return;
            var result = await Web3.Rpc.GetBalanceAsync(Web3.Account.PublicKey);
            var lamports = result.Result?.Value ?? 0UL;
            var sol = lamports / 1_000_000_000d;
            Debug.Log($"[Devnet] Balance: {lamports} lamports ({sol} SOL) @ {DevnetRpcUrl}");
        }
        catch (Exception e)
        {
            Debug.LogWarning($"Failed to fetch lamports: {e.Message}");
        }
    }
}