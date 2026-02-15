using System;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Privy;
using UnityEngine.SceneManagement;

public class PrivyAndroidLogin : MonoBehaviour
{
    public static PrivyAndroidLogin Instance;
    [Header("Privy Dashboard")]
    [SerializeField] private string appId = "YOUR_APP_ID";
    [SerializeField] private string clientId = "YOUR_CLIENT_ID";
    private const string PRIVY_REDIRECT = "sushibonks://";

    [Header("UI")]
    [SerializeField] private Button playButton;
    [SerializeField] private Button quitButton;
    [SerializeField] private Button loginButton;
    [SerializeField] private Button playPanelLogoutButton;
    [SerializeField] private Button mintPanelLogoutButton;
    [SerializeField] private Button googleLoginButton;
    [SerializeField] private Button xLoginButton;
    [SerializeField] private TMP_InputField emailField;
    [SerializeField] private TMP_InputField otpField;
    [SerializeField] private TMP_Text statusLabel;
    [SerializeField] private TMP_Text _loginBtnTxt;
    [SerializeField] private GameObject loginPanel;
    [SerializeField] private GameObject playPanel;
    [SerializeField] private GameObject mintPanel;
    [SerializeField] private GameObject SessionOverlayPanel;
    [SerializeField] private bool managePanelsDirectly = false; // Keep false to let ChefManager own panel flow


    private static bool _inited;
    private bool isLoggingOut = false;

    private void Awake()
    {
        Instance = this;
        _loginBtnTxt.text = "Send otp";
        loginButton.interactable = false;
        if (loginButton) loginButton.onClick.AddListener(ShowLoginStep);
/*        if (playButton) playButton.onClick.AddListener(() => {
            SceneManager.LoadScene(2);
         });*/
        if (quitButton) quitButton.onClick.AddListener(Application.Quit);
        if (playPanelLogoutButton) playPanelLogoutButton.onClick.AddListener(Logout);
        if (mintPanelLogoutButton) mintPanelLogoutButton.onClick.AddListener(Logout);
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
        await ResumeSessionAsync();
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
        bool hasUser = PrivyManager.Instance?.User != null;
        if (isLoggingOut && !hasUser)
        {
            isLoggingOut = false;
        }

        bool isLoggedIn = hasUser && !isLoggingOut;
        if (isLoggedIn)
        {
            ChefManager.Instance?.CheckExistingChef();
        }
        else
        {
            ChefManager.Instance?.ShowLoggedOutState();
        }
        if (managePanelsDirectly)
        {
            if (isLoggedIn)
            {
                mintPanel.SetActive(isLoggedIn);
                loginPanel.SetActive(!isLoggedIn);
            }
            else
            {
                otpField.text = string.Empty;
                loginPanel.SetActive(!isLoggedIn);
                mintPanel.SetActive(isLoggedIn);
            }
        }
        else if (!isLoggedIn)
        {
            otpField.text = string.Empty;
        }
        if (playButton) { playButton.gameObject.SetActive(isLoggedIn); playButton.interactable = isLoggedIn; }
        if (loginButton) { loginButton.gameObject.SetActive(!isLoggedIn); loginButton.interactable = !isLoggedIn; }
        if (quitButton) quitButton.gameObject.SetActive(true);
        if (playPanelLogoutButton)
        {
            playPanelLogoutButton.interactable = isLoggedIn;
        }
        if (mintPanelLogoutButton)
        {
            mintPanelLogoutButton.interactable = isLoggedIn;
        }
    }

    private void Log(string msg) { Debug.Log(msg); if (statusLabel) statusLabel.text = msg; }

    //When user starts typing in email field ->
    public void DisableOtherLoginOpt()
    {
        if (!string.IsNullOrEmpty(emailField.text)){
            googleLoginButton.interactable= false;
            xLoginButton.interactable= false;
            loginButton.interactable= true;
        }else if (string.IsNullOrEmpty(emailField.text))
        {
            googleLoginButton.interactable = true;
            xLoginButton.interactable = true;
            loginButton.interactable= false;
        }
    }

    // ----------------- Auth -----------------

    public async void SendOtp()
    {
        var email = (emailField?.text ?? "").Trim();
        if (string.IsNullOrWhiteSpace(email))
        {
            Log("Enter email"); return;
        }
        bool ok = await PrivyManager.Instance.Email.SendCode(email);
        if (ok)
        {
            _loginBtnTxt.text = "Login";
            emailField.gameObject.SetActive(false);
            otpField.gameObject.SetActive(true);
        }
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
        {
            await user.CreateWallet();
        }
        if (user != null)
        {
            playPanelLogoutButton.gameObject.SetActive(true);
            mintPanelLogoutButton.gameObject.SetActive(true);
        }
        else
        {
            Log("user is null");
        }
            // No token needed - using wallet-based auth
            Log("Login complete. Wallet: " + GetAddress());
        RefreshButtons();
    }

    public string GetAddress()
    {
        var w = PrivyManager.Instance?.User?.EmbeddedWallets?.FirstOrDefault();
        return w != null ? w.Address : "";
    }

    private void SaveAuthToken()
    {
        Debug.Log("🔍 SaveAuthToken called!");
        
        // Manual check - inspect PrivyManager directly
        try
        {
            var manager = PrivyManager.Instance;
            Debug.Log($"✅ PrivyManager exists: {manager != null}");
            
            if (manager != null)
            {
                Debug.Log($"✅ User exists: {manager.User != null}");
                
                // Check if PrivyManager has any method to get token
                var managerType = manager.GetType();
                var methods = managerType.GetMethods();
                
                Debug.Log("🔍 Checking PrivyManager methods for token...");
                foreach (var method in methods)
                {
                    if (method.Name.ToLower().Contains("token") || method.Name.ToLower().Contains("access"))
                    {
                        Debug.Log($"  Found method: {method.Name}()");
                    }
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"❌ Exception in manual check: {e.Message}");
        }
        
        // Use PrivyTokenHelper to extract and save token
        PrivyTokenHelper.SaveToken();
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
    void OnEnable()
    {
        try
        {
            PrivyManager.Instance?.SetAuthStateChangeCallback(async state =>
            {
                if (PrivyManager.Instance.User != null)
                {
                    await EnsureWalletAsync();
                    Log("Wallet: " + GetAddress());
                }
                RefreshButtons();
            });
        }
        catch { }
        Application.deepLinkActivated += OnDeepLink;
    }
    void OnDisable()
    {
        try { PrivyManager.Instance?.SetAuthStateChangeCallback(null); } catch { }

        Application.deepLinkActivated -= OnDeepLink;
    }
    void OnDeepLink(string url) { Debug.Log("Deep link: " + url); }
    private async System.Threading.Tasks.Task ResumeSessionAsync()
    {
        try
        {
            // Ask Privy to hydrate any existing session
            await PrivyManager.Instance.GetAuthState();

            // If a session exists, User will be populated
            if (PrivyManager.Instance.User != null)
            {
                await EnsureWalletAsync();
                Log("Wallet: " + GetAddress());
                if (managePanelsDirectly)
                {
                    loginPanel.SetActive(false);
                    mintPanel.SetActive(true);
                }
                playPanelLogoutButton.interactable = true;
                playPanelLogoutButton.gameObject.SetActive(true);
                mintPanelLogoutButton.interactable = true;
                mintPanelLogoutButton.gameObject.SetActive(true);
            }
            else
            {
                playPanelLogoutButton.interactable = false;
                mintPanelLogoutButton.interactable = false;
            }
        }
        catch { /* ignore */ }
        RefreshButtons();
    }

    public async void Logout()
    {
        isLoggingOut = true;
        try
        {
           PrivyManager.Instance.Logout();   // if your SDK is sync: PrivyManager.Instance.Logout();
            if (managePanelsDirectly)
            {
                loginPanel.SetActive(true);
                playPanel.SetActive(false);
            }
           PrivyTokenHelper.ClearToken();  // Clear saved auth token
            otpField.gameObject.SetActive(false);
            emailField.gameObject.SetActive(true);
            _loginBtnTxt.text = "Send otp";
            playPanelLogoutButton.gameObject.SetActive(false);
            mintPanelLogoutButton.gameObject.SetActive(false);
           
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("Logout failed: " + e.Message);
        }
        ChefManager.Instance?.ShowLoggedOutState();
        RefreshButtons();
        Log("Logged out");
    }
    // ---------- NEW: Google ----------

    public async void LoginWithGoogle()
    {
        try
        {
            // provider keys are typically "google" and "x" (sometimes "twitter")
            await PrivyManager.Instance.OAuth.LoginWithProvider(OAuthProvider.Google, PRIVY_REDIRECT);
            if (PrivyManager.Instance.GetUser() != null)
            {
                playPanelLogoutButton.gameObject.SetActive(true);
                mintPanelLogoutButton.gameObject.SetActive(true);
            }
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
