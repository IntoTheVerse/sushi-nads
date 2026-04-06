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
    private const string PRIVY_REDIRECT = "sushinads://";

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
    private bool hasRequestedProfileForCurrentLogin = false;
    private string _pendingOtpEmail = string.Empty;
    private bool _isOtpStepActive = false;
    private bool _isAuthActionInFlight = false;
    private bool _isPostLoginRecoveryInProgress = false;

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
        bool hasWallet = !string.IsNullOrEmpty(GetAddress());
        bool otpFlowActive = _isOtpStepActive || !string.IsNullOrWhiteSpace(_pendingOtpEmail);
        if (isLoggingOut && !hasUser)
        {
            isLoggingOut = false;
        }

        bool isLoggedIn = hasUser && !isLoggingOut;
        if (isLoggedIn)
        {
            if (!hasWallet)
            {
                // WebGL can lag wallet hydration after auth; avoid premature profile checks.
                hasRequestedProfileForCurrentLogin = false;
            }
            else if (!hasRequestedProfileForCurrentLogin)
            {
                hasRequestedProfileForCurrentLogin = true;
                ChefManager.Instance?.CheckExistingChef();
            }
        }
        else
        {
            hasRequestedProfileForCurrentLogin = false;
            // Keep OTP step stable while waiting for verify; avoid flipping back to "Send otp" mid-flow.
            if (!otpFlowActive)
            {
                ChefManager.Instance?.ShowLoggedOutState();
            }
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
            if (!otpFlowActive)
            {
                otpField.text = string.Empty;
                SetOtpStep(false);
            }
        }
        if (playButton) { playButton.gameObject.SetActive(isLoggedIn); playButton.interactable = isLoggedIn; }
        if (loginButton)
        {
            loginButton.gameObject.SetActive(!isLoggedIn);
            loginButton.interactable = !isLoggedIn && !_isAuthActionInFlight;
        }
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

    private void SetAuthInFlight(bool isInFlight)
    {
        _isAuthActionInFlight = isInFlight;
        if (loginButton) loginButton.interactable = !isInFlight;
        if (googleLoginButton) googleLoginButton.interactable = !isInFlight && string.IsNullOrEmpty(emailField?.text);
        if (xLoginButton) xLoginButton.interactable = !isInFlight && string.IsNullOrEmpty(emailField?.text);
    }

    private void SetOtpStep(bool active)
    {
        _isOtpStepActive = active;
        if (_loginBtnTxt != null) _loginBtnTxt.text = active ? "Login" : "Send otp";
        if (emailField != null) emailField.gameObject.SetActive(!active);
        if (otpField != null) otpField.gameObject.SetActive(active);
    }

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
        if (_isAuthActionInFlight)
        {
            return;
        }

        var email = (emailField?.text ?? "").Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(email))
        {
            Log("Enter email"); return;
        }

        SetAuthInFlight(true);
        try
        {
            bool ok = await PrivyManager.Instance.Email.SendCode(email);
            if (ok)
            {
                _pendingOtpEmail = email;
                SetOtpStep(true);
            }
            else
            {
                SetOtpStep(false);
            }

            Log(ok ? $"Code sent to {email}" : "Failed to send code");
        }
        catch (Exception ex)
        {
            SetOtpStep(false);
            Log("Failed to send code: " + ex.Message);
        }
        finally
        {
            SetAuthInFlight(false);
        }
    }

    public async void VerifyOtp()
    {
        if (_isAuthActionInFlight)
        {
            return;
        }

        var email = string.IsNullOrWhiteSpace(_pendingOtpEmail)
            ? (emailField?.text ?? "").Trim().ToLowerInvariant()
            : _pendingOtpEmail;
        var code = new string(((otpField?.text ?? "").Trim()).Where(char.IsLetterOrDigit).ToArray());
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(code))
        {
            Log("Enter email + code"); return;
        }

        SetAuthInFlight(true);
        try
        {
            await PrivyManager.Instance.Email.LoginWithCode(email, code);
            var user = PrivyManager.Instance.User;
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
            _pendingOtpEmail = string.Empty;
            if (otpField != null) otpField.text = string.Empty;
            SetOtpStep(false);
            Log("Login complete. Finalizing wallet/session...");
            RefreshButtons();
            BeginPostLoginRecovery("otp");
        }
        catch (Exception ex)
        {
            string msg = ex.Message ?? string.Empty;
            if (msg.Contains("Invalid email and code combination") || msg.Contains("422"))
            {
                try { await PrivyManager.Instance.GetAuthState(); } catch { }
                if (PrivyManager.Instance?.User != null)
                {
                    // Session already established despite 422 in some webgl race paths.
                    RefreshButtons();
                }
                else
                {
                    Log("Invalid or expired OTP. Request a new code and try again.");
                    _pendingOtpEmail = string.Empty;
                    if (otpField != null) otpField.text = string.Empty;
                    SetOtpStep(false);
                }
            }
            else
            {
                Log("Login failed: " + msg);
            }
        }
        finally
        {
            SetAuthInFlight(false);
        }
    }

    public string GetAddress()
    {
        return GetAnyKnownWalletAddress();
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
    private async System.Threading.Tasks.Task<bool> EnsureWalletAsync(float timeoutSeconds = 8f, int pollMs = 250)
    {
        var user = PrivyManager.Instance?.User;
        if (user == null)
        {
            Debug.Log("[PrivyAuth] EnsureWalletAsync: user is null at start.");
            return false;
        }

        string userId = user.Id;
        int startingWalletCount = user.EmbeddedWallets?.Length ?? 0;
        string startingPrimary = user.EmbeddedWallets?.FirstOrDefault()?.Address ?? string.Empty;
        string startingKnownAddress = GetAnyKnownWalletAddress(user);
        Debug.Log($"[PrivyAuth] EnsureWalletAsync start. userId={userId}, walletCount={startingWalletCount}, primary={startingPrimary}, knownAddress={startingKnownAddress}");

        if (!string.IsNullOrEmpty(startingKnownAddress))
        {
            Debug.Log($"[PrivyAuth] EnsureWalletAsync: wallet already known at start: {startingKnownAddress}");
            return true;
        }

        if (user.EmbeddedWallets == null || !user.EmbeddedWallets.Any())
        {
            try
            {
                Debug.Log("[PrivyAuth] EnsureWalletAsync: no wallets found, calling CreateWallet().");
                var createWalletTask = user.CreateWallet();
                var createWalletTimeoutTask = System.Threading.Tasks.Task.Delay(3000);
                var createWalletCompleted = await System.Threading.Tasks.Task.WhenAny(createWalletTask, createWalletTimeoutTask);
                if (createWalletCompleted == createWalletTask)
                {
                    await createWalletTask;
                    Debug.Log("[PrivyAuth] EnsureWalletAsync: CreateWallet() completed.");
                }
                else
                {
                    Debug.LogWarning("[PrivyAuth] EnsureWalletAsync: CreateWallet() timed out after 3000ms; continuing with polling.");
                }
            }
            catch (Exception ex)
            {
                // WebGL can report create failure before user/wallet state catches up.
                Debug.LogWarning($"[PrivyAuth] EnsureWalletAsync: CreateWallet() threw: {ex.Message}");
            }
        }
        else
        {
            Debug.Log("[PrivyAuth] EnsureWalletAsync: wallet already present, skipping CreateWallet().");
        }

        DateTime start = DateTime.UtcNow;
        TimeSpan timeout = TimeSpan.FromSeconds(Mathf.Max(1f, timeoutSeconds));
        int pollCount = 0;
        while (DateTime.UtcNow - start < timeout)
        {
            pollCount++;
            string address = GetAnyKnownWalletAddress(PrivyManager.Instance?.User);
            int walletCount = PrivyManager.Instance?.User?.EmbeddedWallets?.Length ?? 0;
            if (!string.IsNullOrEmpty(address))
            {
                Debug.Log($"[PrivyAuth] EnsureWalletAsync success on poll {pollCount}. walletCount={walletCount}, address={address}");
                return true;
            }

            try
            {
                bool refreshed = await TryRefreshAuthStateWithTimeout(1500);
                if (!refreshed && (pollCount == 1 || pollCount % 4 == 0))
                {
                    Debug.LogWarning($"[PrivyAuth] EnsureWalletAsync poll {pollCount}: auth refresh timed out; continuing.");
                }
            }
            catch
            {
                // Keep polling through transient WebGL hydration delays.
                if (pollCount == 1 || pollCount % 4 == 0)
                {
                    Debug.LogWarning($"[PrivyAuth] EnsureWalletAsync poll {pollCount}: GetAuthState() threw.");
                }
            }

            await System.Threading.Tasks.Task.Delay(Mathf.Max(50, pollMs));
        }

        Debug.LogWarning($"[PrivyAuth] EnsureWalletAsync timeout after {pollCount} polls and {(DateTime.UtcNow - start).TotalMilliseconds:0}ms.");
        Debug.Log("Wallet: (none)");
        return false;
    }

    private async System.Threading.Tasks.Task<bool> TryRefreshAuthStateWithTimeout(int timeoutMs)
    {
        var getAuthStateTask = PrivyManager.Instance.GetAuthState();
        var timeoutTask = System.Threading.Tasks.Task.Delay(Mathf.Max(250, timeoutMs));
        var completed = await System.Threading.Tasks.Task.WhenAny(getAuthStateTask, timeoutTask);
        if (completed == getAuthStateTask)
        {
            await getAuthStateTask;
            return true;
        }

        return false;
    }

    private string GetAnyKnownWalletAddress()
    {
        return GetAnyKnownWalletAddress(PrivyManager.Instance?.User);
    }

    private string GetAnyKnownWalletAddress(IPrivyUser user)
    {
        if (user == null)
        {
            return string.Empty;
        }

        var embeddedWallet = user.EmbeddedWallets?.FirstOrDefault();
        if (embeddedWallet != null && !string.IsNullOrEmpty(embeddedWallet.Address))
        {
            return embeddedWallet.Address;
        }

        var primaryLinkedEmbedded = user.LinkedAccounts?
            .OfType<PrivyEmbeddedWalletAccount>()
            .FirstOrDefault(account => account != null
                                       && account.WalletIndex == 0
                                       && !string.IsNullOrEmpty(account.Address));
        if (primaryLinkedEmbedded != null)
        {
            return primaryLinkedEmbedded.Address;
        }

        var anyLinkedEmbedded = user.LinkedAccounts?
            .OfType<PrivyEmbeddedWalletAccount>()
            .FirstOrDefault(account => account != null && !string.IsNullOrEmpty(account.Address));
        if (anyLinkedEmbedded != null)
        {
            return anyLinkedEmbedded.Address;
        }

        var externalWallet = user.LinkedAccounts?
            .OfType<ExternalWalletAccount>()
            .FirstOrDefault(account =>
                account != null
                && !string.IsNullOrEmpty(account.Address)
                && (string.IsNullOrEmpty(account.ChainType)
                    || account.ChainType.Equals("ethereum", StringComparison.OrdinalIgnoreCase)));
        if (externalWallet != null)
        {
            return externalWallet.Address;
        }

        return string.Empty;
    }
    private async System.Threading.Tasks.Task<bool> EnsureWalletWithTimeout(int timeoutMs)
    {
        var ensureTask = EnsureWalletAsync();
        var timeoutTask = System.Threading.Tasks.Task.Delay(Mathf.Max(500, timeoutMs));
        var completed = await System.Threading.Tasks.Task.WhenAny(ensureTask, timeoutTask);
        if (completed == ensureTask)
        {
            return await ensureTask;
        }

        bool hasAnyAddress = !string.IsNullOrEmpty(GetAnyKnownWalletAddress());
        Debug.LogWarning($"[PrivyAuth] EnsureWalletWithTimeout hit timeout ({timeoutMs}ms). knownAddressPresent={hasAnyAddress}");
        return hasAnyAddress;
    }

    private void BeginPostLoginRecovery(string source)
    {
        if (_isPostLoginRecoveryInProgress)
        {
            Debug.Log($"[PrivyAuth] BeginPostLoginRecovery skipped (already in progress). source={source}");
            return;
        }

        _ = RunPostLoginRecoveryAsync(source);
    }

    private async System.Threading.Tasks.Task RunPostLoginRecoveryAsync(string source)
    {
        if (PrivyManager.Instance?.User == null)
        {
            return;
        }

        _isPostLoginRecoveryInProgress = true;
        try
        {
            bool hasWallet = !string.IsNullOrEmpty(GetAddress());
            for (int attempt = 1; attempt <= 4 && !hasWallet; attempt++)
            {
                hasWallet = await EnsureWalletWithTimeout(2500);
                if (!hasWallet)
                {
                    Debug.LogWarning($"[PrivyAuth] Post-login recovery attempt {attempt}/4 has no wallet yet. source={source}");
                    await System.Threading.Tasks.Task.Delay(600);
                }
            }

            string address = GetAddress();
            if (!string.IsNullOrEmpty(address))
            {
                Log("Wallet: " + address);
            }
            else
            {
                Debug.LogWarning($"[PrivyAuth] Post-login recovery could not resolve wallet. source={source}");
            }

            // Force one profile refresh attempt after auth settles, even if wallet hydration lags.
            // GameAPIClient now uses wallet fallbacks and cached values.
            hasRequestedProfileForCurrentLogin = true;
            ChefManager.Instance?.CheckExistingChef();
            RefreshButtons();
        }
        finally
        {
            _isPostLoginRecoveryInProgress = false;
        }
    }

    void OnEnable()
    {
        try
        {
            PrivyManager.Instance?.SetAuthStateChangeCallback(async state =>
            {
                if (PrivyManager.Instance.User != null)
                {
                    BeginPostLoginRecovery("auth-state-change");
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
                BeginPostLoginRecovery("resume-session");
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
        hasRequestedProfileForCurrentLogin = false;
        _isPostLoginRecoveryInProgress = false;
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
            _pendingOtpEmail = string.Empty;
            SetOtpStep(false);
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
            BeginPostLoginRecovery("google-oauth");
            Log("Google login complete. Finalizing wallet/session...");
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
            BeginPostLoginRecovery("x-oauth");
            Log("X login complete. Finalizing wallet/session...");
            RefreshButtons();
        }
        catch (System.Exception e) { Log("X login failed: " + e.Message); }
    }

    private void ShowLoginStep()
    {
        if (_isAuthActionInFlight)
        {
            return;
        }

        if (_isOtpStepActive) VerifyOtp();
        else SendOtp();
    }
}
