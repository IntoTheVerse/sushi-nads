using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Privy;
using UnityEngine.SceneManagement;

public class PrivyAndroidLogin : MonoBehaviour
{
    [Header("Privy Dashboard")]
    [SerializeField] private string appId = "YOUR_APP_ID";
    [SerializeField] private string clientId = "YOUR_CLIENT_ID";

    [Header("UI")]
    [SerializeField] private Button playButton;
    [SerializeField] private Button quitButton;
    [SerializeField] private Button loginButton;
    [SerializeField] private Button googleLoginButton;
    [SerializeField] private Button xLoginButton;
    [SerializeField] private TMP_InputField emailField;
    [SerializeField] private TMP_InputField otpField;
    [SerializeField] private TMP_Text statusLabel;

    private static bool _inited;

    private void Awake()
    {
        if (loginButton) loginButton.onClick.AddListener(ShowLoginStep);
        if (playButton) playButton.onClick.AddListener(() => SceneManager.LoadScene(2));
        if (quitButton) quitButton.onClick.AddListener(Application.Quit);
    }

    private async void Start()
    {
        if (!string.IsNullOrEmpty(Application.absoluteURL)) OnDeepLink(Application.absoluteURL);
        if (_inited) { RefreshButtons(); return; }
        _inited = true;
        xLoginButton.onClick.AddListener(LoginWithX);
        googleLoginButton.onClick.AddListener(LoginWithGoogle);
        Debug.Log($"Initialising with appId {appId} clientId {clientId} Package={Application.identifier}");
        PrivyManager.Initialize(new PrivyConfig { AppId = appId, ClientId = clientId });

        // Non-blocking warmup; ok if it times out in Editor
        var warm = PrivyManager.Instance.GetAuthState();
        await System.Threading.Tasks.Task.WhenAny(warm, System.Threading.Tasks.Task.Delay(3000));

        if (string.IsNullOrWhiteSpace(appId) || appId == "YOUR_APP_ID" ||
            string.IsNullOrWhiteSpace(clientId) || clientId == "YOUR_CLIENT_ID")
            Log(" Set your Privy App ID + Client ID in the Inspector.");

        RefreshButtons();
    }

    private void OnDestroy()
    {
        if (loginButton) loginButton.onClick.RemoveListener(ShowLoginStep);
        if (playButton) playButton.onClick.RemoveAllListeners();
        if (quitButton) quitButton.onClick.RemoveAllListeners();
    }

    private void RefreshButtons()
    {
        bool isLoggedIn = PrivyManager.Instance?.User != null;
        if (playButton) { playButton.gameObject.SetActive(isLoggedIn); playButton.interactable = isLoggedIn; }
        if (loginButton) { loginButton.gameObject.SetActive(!isLoggedIn); loginButton.interactable = !isLoggedIn; }
        if (quitButton) quitButton.gameObject.SetActive(true);
    }

    private void Log(string msg) { Debug.Log(msg); if (statusLabel) statusLabel.text = msg; }

    // ----------------- Auth -----------------

    public async void SendOtp()
    {
        var email = (emailField?.text ?? "").Trim();
        if (string.IsNullOrWhiteSpace(email)) { Log("Enter email"); return; }

        bool ok = await PrivyManager.Instance.Email.SendCode(email);
        Log(ok ? $"Code sent to {email}" : "Failed to send code");
    }

    public async void VerifyOtp()
    {
        var email = (emailField?.text ?? "").Trim();
        var code = (otpField?.text ?? "").Trim();
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(code))
        {
            Log("Enter email + code"); return;
        }

        await PrivyManager.Instance.Email.LoginWithCode(email, code);

        // Ensure embedded wallet exists
        var user = PrivyManager.Instance.User;
        if (user != null && !user.EmbeddedWallets.Any())
            await user.CreateWallet();

        Log("Login complete. Wallet: " + GetAddress());
        RefreshButtons();
    }

    public string GetAddress()
    {
        var w = PrivyManager.Instance?.User?.EmbeddedWallets?.FirstOrDefault();
        return w != null ? w.Address : "";
    }

    // ----------------- Signing -----------------

    public async void SignEip191(string message = "Hello from Privy+Unity (EIP-191)")
    {
        var w = PrivyManager.Instance?.User?.EmbeddedWallets?.FirstOrDefault();
        if (w == null) { Log("Login first"); return; }

        var req = new RpcRequest { Method = "personal_sign", Params = new[] { message, w.Address } };
        var res = await w.RpcProvider.Request(req);
        Log("EIP-191 sig: " + (res.Data as string));
    }

    public async void SignEip712(string typedDataJson)
    {
        var w = PrivyManager.Instance?.User?.EmbeddedWallets?.FirstOrDefault();
        if (w == null) { Log("Login first"); return; }

        var req = new RpcRequest { Method = "eth_signTypedData_v4", Params = new[] { w.Address, typedDataJson } };
        var res = await w.RpcProvider.Request(req);
        Log("EIP-712 sig: " + (res.Data as string));
    }

    // Ensure an embedded EVM wallet exists after any login
    private async System.Threading.Tasks.Task EnsureWalletAsync()
    {
        var user = PrivyManager.Instance.User;
        if (user != null && !user.EmbeddedWallets.Any())
            await user.CreateWallet();
        Debug.Log("Wallet: " + (PrivyManager.Instance?.User?.EmbeddedWallets?.FirstOrDefault()?.Address ?? "(none)"));
    }
    void OnEnable() => Application.deepLinkActivated += OnDeepLink;
    void OnDisable() => Application.deepLinkActivated -= OnDeepLink;
    void OnDeepLink(string url) { Debug.Log("Deep link: " + url); }

    // ---------- NEW: Google ----------
    private const string PRIVY_REDIRECT = "https://auth.privy.io/api/v1/oauth/callback";

    public async void LoginWithGoogle()
    {
        try
        {
            // provider keys are typically "google" and "x" (sometimes "twitter")
            await PrivyManager.Instance.OAuth.LoginWithProvider(OAuthProvider.Google, PRIVY_REDIRECT);
            await EnsureWalletAsync();
            Log("Google login complete. " + GetAddress());
            RefreshButtons();
        }
        catch (System.Exception e) { Log("Google login failed: " + e.Message); }
    }

    // ---------- NEW: X / Twitter ----------
    public async void LoginWithX()
    {
        try
        {
            await PrivyManager.Instance.OAuth.LoginWithProvider(OAuthProvider.Twitter, PRIVY_REDIRECT); // if this errors, try "twitter"
            await EnsureWalletAsync();
            Log("X login complete. " + GetAddress());
            RefreshButtons();
        }
        catch (System.Exception e) { Log("X login failed: " + e.Message); }
    }

    private void ShowLoginStep()
    {
        // if code is filled, verify; else send code
        if (!string.IsNullOrWhiteSpace(otpField?.text)) VerifyOtp();
        else SendOtp();
    }
}
