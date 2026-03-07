using System;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Privy;
using Newtonsoft.Json;
using System.Collections;
using UnityEngine.SceneManagement;

/// <summary>
/// Manages chef NFT minting and player profile
/// Integrates Privy authentication with backend API
/// </summary>
public class ChefManager : MonoBehaviour
{
    public static ChefManager Instance { get; private set; }

    private enum MenuUiState
    {
        Loading,
        LoggedOut,
        NeedsMint,
        Ready
    }

    [Header("UI References")]
    [SerializeField] private GameObject loginPanel; // Login/OTP panel
    [SerializeField] private GameObject mintPanel; // Panel to show/hide based on chef status
    [SerializeField] private GameObject playPanel; // Panel to show/hide based on chef status
    [SerializeField] private GameObject tutorialPromptPanel; // Forced tutorial prompt for first-time users
    [SerializeField] private GameObject GetEnergyPanel; // Panel to show/hide based on chef status
    [SerializeField] private GameObject StatusPanel; 
    [SerializeField] private Button playButton; // Play/Start Game button

    [SerializeField] private TMP_InputField chefNameInput;
    [SerializeField] private Button mintChefButton;
    [SerializeField] private Button refillEnergyButton; // Free energy refill
    [SerializeField] private Button refillEnergyUSDCButton; // USDC payment refill
    [SerializeField] private Button copyWABtn; // USDC payment refill
    [SerializeField] private Button tutorialBtn; // USDC payment refill
    
    [Header("Tutorial Confirmation")]
    [SerializeField] private GameObject tutorialConfirmationPanel;
    [SerializeField] private Button confirmTutorialYesBtn;
    [SerializeField] private Button confirmTutorialNoBtn;

    [SerializeField] private Button tutorialPromptYesButton; // "Yes" button inside forced tutorial prompt
    [SerializeField] private bool enableTutorialGateDebug = true; // Verbose gate diagnostics in Console
    [SerializeField] private TMP_Text statusText;
    [SerializeField] private TMP_Text walletAddressText;
    [SerializeField] private TMP_Text chefTokenIdText;
    [SerializeField] private TMP_Text energyText;
    [SerializeField] private TMP_Text playPanelUsernameTxt;
    [SerializeField] private TMP_Text playPanelWalletAddressTxt;
    [SerializeField] private TMP_Text playerProfileUsernameTxt;
    [SerializeField] private TMP_Text playerProfileChefIdTxt;
    [SerializeField] private TMP_Text playerProfilewalletAddressTxt;
    [SerializeField] private TMP_Text playerProfileEnergyTxt;
    [SerializeField] private Image energyFill;
    [SerializeField] private Image playerProfileenergyFill;
    [SerializeField] private int maxEnergy = 1000; 

    private int energyVal;

    [Header("Chef NFT Data")]
    private string chefName;
    private int tokenId;
    private bool hasChef;

    // In-memory cache so returning from GameScene goes straight to menu panel.
    private static bool cachedHasChef;
    private static string cachedWalletAddress = string.Empty;
    private static string cachedChefName = string.Empty;
    private static int cachedChefTokenId;

    private MainMenuUI mainMenuUi;
    private bool mustPlayTutorialFirst;
    private bool hasResolvedTutorialGate;
    private Task profileRefreshTask;
    private enum PromptIntent
    {
        None,
        Tutorial,
        SessionRecovery
    }

    [Header("Session Recovery Prompt")]
    [SerializeField] private Button tutorialPromptNoButton;
    [SerializeField] private TMP_Text tutorialPromptText;
    [SerializeField] private string sessionRecoveryPromptMessage = "End previous session to proceed to next?";
    [SerializeField] private string sessionRecoveryDeclineMessage = "Previous session kept active. Try again later after timeout.";
    [SerializeField] private float sessionRecoveryRetryDelaySeconds = 5f;

    private PromptIntent activePromptIntent = PromptIntent.None;
    private int pendingRecoverySessionId = 0;
    private bool isProcessingSessionRecovery = false;
    private string defaultTutorialPromptMessage = string.Empty;

    private const string TutorialDoneKeyPrefix = "mandatory_tutorial_done_";
    
    private Coroutine currentStatusCoroutine;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        
        Instance = this;
        ResolvePanelReferences();
        SetMenuUiState(MenuUiState.Loading);
        
        if (mintChefButton != null)
        {
            mintChefButton.onClick.AddListener(OnMintChefClicked);
        }
        
        if (refillEnergyButton != null)
        {
            refillEnergyButton.onClick.AddListener(OnRefillEnergyClicked);
        }
        
        if (playButton != null)
        {
            playButton.onClick.AddListener(OnPlayButtonClicked);
            playButton.onClick.AddListener(OnPlayButtonRawClicked);
            LogTutorialGate("Awake", $"Bound Play button listener at path={GetHierarchyPath(playButton.transform)}");
        }
        else
        {
            LogTutorialGate("Awake", "Play button is NULL in ChefManager inspector. Tutorial gate click cannot run.");
        }
        if (tutorialPromptYesButton != null)
        {
            tutorialPromptYesButton.onClick.AddListener(OnTutorialPromptYesClicked);
        }
        ResolveTutorialPromptControls();
        if (tutorialPromptNoButton != null)
        {
            tutorialPromptNoButton.onClick.AddListener(OnTutorialPromptNoClicked);
        }
        if (copyWABtn != null)
        {
            copyWABtn.onClick.AddListener(CopyWalletAddress);
        }
        
        if (tutorialBtn != null)
        {
            tutorialBtn.onClick.AddListener(ShowTutorialConfirmationPanel);
        }
        if (confirmTutorialYesBtn != null)
        {
            confirmTutorialYesBtn.onClick.AddListener(OnTutorialConfirmYes);
        }
        if (confirmTutorialNoBtn != null)
        {
            confirmTutorialNoBtn.onClick.AddListener(OnTutorialConfirmNo);
        }

        if (tutorialConfirmationPanel != null)
        {
            tutorialConfirmationPanel.SetActive(false);
        }

        // if (refillEnergyUSDCButton != null)
        // {
        //     refillEnergyUSDCButton.onClick.AddListener(OnRefillEnergyUSDCClicked);
        // }

        if (tutorialPromptPanel != null)
        {
            tutorialPromptPanel.SetActive(false);
        }

        LogTutorialGate("Awake", $"tutorialPromptPanel={DescribePanel(tutorialPromptPanel)}, tutorialPromptYesButton={(tutorialPromptYesButton != null ? tutorialPromptYesButton.name : "null")}");
    }

    private void Start()
    {
        // Wait for Privy to initialize
        StartCoroutine(WaitForPrivyAndInitialize());
    }

    private System.Collections.IEnumerator WaitForPrivyAndInitialize()
    {
        SetMenuUiState(MenuUiState.Loading);

        // Wait for PrivyAndroidLogin to initialize Privy first
        yield return new WaitForSeconds(2f);
        hasResolvedTutorialGate = false;
        
        // Try to safely check if Privy is initialized
        bool privyReady = false;
        int attempts = 0;
        
        while (!privyReady && attempts < 10)
        {
            try
            {
                // Try to access PrivyManager - will throw if not initialized
                if (PrivyManager.Instance != null && PrivyManager.Instance.IsReady)
                {
                    privyReady = true;
                    break;
                }
            }
            catch (System.Exception)
            {
                // Privy not initialized yet
                Debug.Log("Waiting for Privy to initialize...");
            }
            
            attempts++;
            yield return new WaitForSeconds(0.5f);
        }

        if (!privyReady)
        {
            SetMenuUiState(MenuUiState.LoggedOut);
            ShowStatusMessage("Privy not initialized. Please go to login scene first.", Color.red);
            if (mintChefButton != null) mintChefButton.interactable = false;
            yield break;
        }

        // Check if player is logged in
        if (PrivyManager.Instance?.User == null)
        {
            SetMenuUiState(MenuUiState.LoggedOut);
            //ShowStatusMessage("Please login first", Color.yellow);
            if (mintChefButton != null) mintChefButton.interactable = false;
            yield break;
        }

        // Display wallet address
        UpdateWalletDisplay();

        if (TryApplyCachedChefState())
        {
            _ = RefreshProfileSilently();
            yield break;
        }
        
        // Check if player already has chef
        CheckExistingChef();
    }

    private void UpdateWalletDisplay()
    {
        var wallet = PrivyManager.Instance?.User?.EmbeddedWallets?.FirstOrDefault();
        if (wallet != null && walletAddressText != null)
        {
            string address = wallet.Address;
            playerProfilewalletAddressTxt.text = $"Wallet: {address.Substring(0, 6)}...{address.Substring(address.Length - 4)}";
            playPanelWalletAddressTxt.text = $"{address.Substring(0, 6)}...{address.Substring(address.Length - 4)}";
        }
    }

    public async void CheckExistingChef()
    {
        await EnsureProfileRefreshAsync(showStatus: true, source: "CheckExistingChef");
    }

    private Task EnsureProfileRefreshAsync(bool showStatus, string source)
    {
        if (profileRefreshTask != null && !profileRefreshTask.IsCompleted)
        {
            LogTutorialGate(source, "Profile refresh already in flight. Reusing existing request.");
            return profileRefreshTask;
        }

        profileRefreshTask = RefreshProfileInternalAsync(showStatus, source);
        return profileRefreshTask;
    }

    private async Task RefreshProfileInternalAsync(bool showStatus, string source)
    {
        try
        {
            if (showStatus)
            {
                ShowStatusMessage("Checking for existing chef...", Color.yellow);
            }
            hasResolvedTutorialGate = false;
            LogTutorialGate(source, "Starting profile check from backend.");
            
            // Update wallet display first (in case Privy wasn't ready earlier)
            UpdateWalletDisplay();
            
            var profile = await GameAPIClient.Instance.GetProfile();
            
            if (profile != null && profile.player != null)
            {
                ApplyReadyState(profile);
                ShowStatusMessage($"Welcome back, {chefName}! (Chef #{tokenId})", Color.green, 4f);

                
                // Disable minting controls
                if (mintChefButton != null) mintChefButton.interactable = false;
                if (chefNameInput != null) chefNameInput.interactable = false;
                
                
                Debug.Log($"✅ Existing chef found: {chefName} (#{tokenId})");
            }
            else
            {
                ClearCachedChefState();
                hasResolvedTutorialGate = false;
                // No chef found - show mint panel
                SetMenuUiState(MenuUiState.NeedsMint);
                
                if (showStatus)
                {
                    ShowStatusMessage("Create your chef to start playing!", Color.white);
                }
                if (mintChefButton != null) mintChefButton.interactable = true;
            }
        }
        catch (Exception e)
        {
            // Likely 404 - no chef exists
            Debug.Log("No existing chef found (this is normal for new players)");
            ClearCachedChefState();
            hasResolvedTutorialGate = false;
            
            // SHOW mint panel - new player needs to mint
            SetMenuUiState(MenuUiState.NeedsMint);
            
            // Still try to update wallet display even if no chef exists
            UpdateWalletDisplay();
            
            if (showStatus)
            {
                ShowStatusMessage("Create your chef to start playing!", Color.white);
            }
            if (mintChefButton != null) mintChefButton.interactable = true;
        }
    }

    private async void OnMintChefClicked()
    {
        string name = chefNameInput?.text?.Trim();
        
        // Validation
        if (string.IsNullOrWhiteSpace(name))
        {
            ShowStatusMessage("Please enter a chef name", Color.red);
            return;
        }
        
        if (name.Length < 3 || name.Length > 20)
        {
            ShowStatusMessage("Name must be 3-20 characters", Color.red);
            return;
        }

        // Check if user is logged in
        if (PrivyManager.Instance?.User == null)
        {
            ShowStatusMessage("Please login first", Color.red);
            return;
        }

        await MintChef(name);
    }

    public async Task MintChef(string playerName)
    {
        try
        {
            ShowStatusMessage("Minting your chef NFT...", Color.yellow);
            
            if (mintChefButton != null) mintChefButton.interactable = false;
            if (chefNameInput != null) chefNameInput.interactable = false;

            // Call backend API
            var response = await GameAPIClient.Instance.MintChef(playerName);

            if (response.success)
            {
                hasChef = true;
                chefName = response.name;
                tokenId = response.tokenId;
                CacheChefState(chefName, tokenId);
                
                ShowStatusMessage($"Chef minted! You are {chefName} (#{tokenId})", Color.green);
                
                if (response.gasSponsored)
                {
                    ShowStatusMessage($"{chefName} #{tokenId} | Gas: FREE (sponsored)", Color.green);
                }
                SetMenuUiState(MenuUiState.Ready);

                Debug.Log($"✅ Chef NFT minted successfully!");
                Debug.Log($"   Name: {chefName}");
                Debug.Log($"   Token ID: {tokenId}");
                Debug.Log($"   Tx Hash: {response.txHash}");
                Debug.Log($"   Gas Sponsored: {response.gasSponsored}");
                
                // Update UI
                if (chefTokenIdText != null)
                {
                    chefTokenIdText.text = $"Chef #{tokenId}: {chefName}";
                }
                
                // Hide mint panel after successful mint
                SetMenuUiState(MenuUiState.Ready);
                
                // Wait for blockchain to index the transaction
                Debug.Log("⏳ Waiting for blockchain confirmation...");
                await Task.Delay(5000); // 5 second delay
                
                // Load player profile
                await LoadPlayerProfile();
                
                // Update wallet display after minting (ensures it's visible)
                UpdateWalletDisplay();
                
                // Show next step message
                ShowStatusMessage($"Chef ready! Refill energy to play.", Color.green);
            }
            else
            {
                ShowStatusMessage("Minting failed", Color.red);
                if (mintChefButton != null) mintChefButton.interactable = true;
                if (chefNameInput != null) chefNameInput.interactable = true;
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"❌ Mint chef error: {e.Message}");
            ShowStatusMessage($"Error: {e.Message}", Color.red);
            
            if (mintChefButton != null) mintChefButton.interactable = true;
            if (chefNameInput != null) chefNameInput.interactable = true;
        }
    }

    public async Task LoadPlayerProfile()
    {
        try
        {
            Debug.Log("📊 Loading player profile...");
            var profile = await GameAPIClient.Instance.GetProfile();
            
            if (profile != null)
            {
                ApplyReadyState(profile);
                Debug.Log("✅ Profile loaded successfully!");
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning($"⚠️ Profile not ready yet: {e.Message}");
            Debug.LogWarning("This is normal right after minting. Profile will be available on next login.");
        }
    }

    private void UpdateUI(PlayerProfileResponse profile)
    {
        UpdateEnergyUI(profile.player.energy);
        // Update energy display
        energyVal = profile.player.energy;
        
        // Update chef info
        if (chefTokenIdText != null)
        {
            chefTokenIdText.text = $"Chef #{profile.player.tokenId}: {profile.player.name}";
            playerProfileChefIdTxt.text = $"#{profile.player.tokenId}";
            playerProfileUsernameTxt.text = profile.player.name;
            // playerProfileEnergyTxt.text = $"Energy: {profile.player.energy}";
            // playerProfileenergyFill.fillAmount = Mathf.Clamp01((float)profile.player.energy / maxEnergy);
        }
        
        // Update wallet address display (in case it wasn't ready earlier)
        UpdateWalletDisplay();
        
        Debug.Log("📊 Player Stats:");
        Debug.Log($"   Energy: {profile.player.energy}");
        Debug.Log($"   Sessions Played: {profile.globalStats.totalSessionsPlayed}");
        Debug.Log($"   Recipes Delivered: {profile.globalStats.totalRecipesDelivered}");
        Debug.Log($"   Well Cooked: {profile.globalStats.totalWellCooked}");
    }

    private void ShowStatusMessage(string message, Color color, float durationSeconds = 100f)
    {
        // Stop previous status coroutine if running
        if (currentStatusCoroutine != null)
        {
            StopCoroutine(currentStatusCoroutine);
            currentStatusCoroutine = null;
        }
        
        // Start new status coroutine
        currentStatusCoroutine = StartCoroutine(ShowStatus(message, color, durationSeconds));
    }
    
    private IEnumerator ShowStatus(string message, Color color, float durationSeconds)
    {
        ColorUtility.TryParseHtmlString("#F5F2FC", out color);
        StatusPanel.SetActive(true);
        if (statusText != null)
        {
            statusText.text = message;
            statusText.color = color;
        }
        yield return new WaitForSeconds(durationSeconds);
        StatusPanel.SetActive(false);
        Debug.Log($"[ChefManager] {message}");
        currentStatusCoroutine = null; // Clear reference when done
    }

    // ============ Energy Refill ============
    
    private async void OnRefillEnergyClicked()
    {
        if (PrivyManager.Instance?.User == null)
        {
            ShowStatusMessage("Please login first", Color.red);
            return;
        }
        
        if (!hasChef)
        {
            ShowStatusMessage("Please mint a chef first", Color.red);
            return;
        }
        
        // Check if energy is too high to refill
        if (energyVal >= 700)
        {
            ShowStatusMessage($"Your energy is too high ({energyVal}/{maxEnergy}). Energy must be below 700 to refill.", Color.yellow);
            if (GetEnergyPanel != null) GetEnergyPanel.SetActive(false);
            return;
        }
        
        await RefillEnergy();
    }
    
    public async Task RefillEnergy()
    {
        try
        {
            ShowStatusMessage("Refilling energy...", Color.yellow, 6f);
            
            if (refillEnergyButton != null) refillEnergyButton.interactable = false;
            
            // Call backend API
            var response = await GameAPIClient.Instance.RefillEnergy();
            
            if (response.success)
            {
                Debug.Log($"✅ Energy refilled!");
                Debug.Log($"   New Energy: {response.newEnergy}");
                Debug.Log($"   Tx Hash: {response.txHash}");
                Debug.Log($"   Cost: 0.1 USDC (FREE in testing)");
                
                // Get fresh energy count from blockchain
                var profile = await GameAPIClient.Instance.GetProfile();
                int currentEnergy = response.newEnergy;
                if (profile != null)
                {
                    // Update both energyVal and UI to keep them in sync
                    UpdateUI(profile);
                }
                else
                {
                    // Fallback if profile fetch fails
                    energyVal = currentEnergy;
                    UpdateEnergyUI(currentEnergy);
                }
                
                // Get explorer URL from backend (Base Sepolia or Monad)
                string explorerUrl = await GameAPIClient.Instance.GetExplorerUrl();
                string explorerName = explorerUrl.Contains("basescan") ? "Basescan" : "MonVision";
                string txUrl = $"{explorerUrl}/tx/{response.txHash}";
                
                ShowStatusMessage(
                    $"+300 Energy!\nCost: 0.1 USDC (FREE)\nClick to view transaction", 
                    Color.green,
                    6f
                );
                
                Debug.Log($"✅ Energy refilled!");
                Debug.Log($"🔗 View on {explorerName}: {txUrl}");
                Debug.Log($"💡 TIP: In production, this costs 0.1 USDC per refill");
                
                // Open explorer in browser
                Debug.Log($"🌐 Opening {explorerName}...");
                Application.OpenURL(txUrl);
                GetEnergyPanel.SetActive(false);
            }
            else
            {
                ShowStatusMessage("Refill failed", Color.red);
            }
            
            if (refillEnergyButton != null) refillEnergyButton.interactable = true;
        }
        catch (Exception e)
        {
            Debug.LogError($"❌ Refill energy error: {e.Message}");
            ShowStatusMessage($"Error: {e.Message}", Color.red);
            
            if (refillEnergyButton != null) refillEnergyButton.interactable = true;
        }
    }    
    
    private void ShowTutorialConfirmationPanel()
    {
        if (tutorialConfirmationPanel != null)
        {
            tutorialConfirmationPanel.SetActive(true);
            tutorialConfirmationPanel.transform.SetAsLastSibling();
        }
    }

    private void OnTutorialConfirmYes()
    {
        if (tutorialConfirmationPanel != null)
        {
            tutorialConfirmationPanel.SetActive(false);
        }
        OpenTutorial();
    }

    private void OnTutorialConfirmNo()
    {
        if (tutorialConfirmationPanel != null)
        {
            tutorialConfirmationPanel.SetActive(false);
        }
    }

    public void OpenTutorial()
    {
        LogTutorialGate("OpenTutorial", $"Starting tutorial launch. key={GetMandatoryTutorialKey()}");
        MarkMandatoryTutorialCompleted();
        mustPlayTutorialFirst = false;
        hasResolvedTutorialGate = true;

        if (tutorialPromptPanel != null)
        {
            tutorialPromptPanel.SetActive(false);
            LogTutorialGate("OpenTutorial", $"Prompt hidden after confirmation. {DescribePanel(tutorialPromptPanel)}");
        }

        GameSceneLaunchContext.SetTutorialMode();
        SceneManager.LoadScene("GameScene");
    }

    public void OnTutorialPromptYesClicked()
    {
        if (activePromptIntent == PromptIntent.SessionRecovery)
        {
            _ = HandleSessionRecoveryYesAsync();
            return;
        }

        LogTutorialGate("OnTutorialPromptYesClicked", "User confirmed tutorial prompt.");
        OpenTutorial();
    }

    private void OnPlayButtonRawClicked()
    {
        LogTutorialGate("OnPlayButtonRawClicked", "Raw UnityEvent click received on configured Play button.");
    }

    // Call this from a UI button
    public void CopyWalletAddress()
    {
        var addr = PrivyAndroidLogin.Instance.GetAddress();
        if (string.IsNullOrEmpty(addr))
        {
            Debug.Log("No wallet address yet.");
            return;
        }

        // 1) Cross-platform clipboard
        GUIUtility.systemCopyBuffer = addr;

#if UNITY_ANDROID && !UNITY_EDITOR
    try
    {
        // 2) Native Android clipboard (extra reliability)
        using var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
        using var activity    = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
        using var clipDataCls = new AndroidJavaClass("android.content.ClipData");
        using var clip        = clipDataCls.CallStatic<AndroidJavaObject>("newPlainText", "Wallet", addr);
        using var clipboard   = activity.Call<AndroidJavaObject>("getSystemService", "clipboard");
        clipboard.Call("setPrimaryClip", clip);

        // 3) Tiny Toast so user sees feedback
        using var toastCls = new AndroidJavaClass("android.widget.Toast");
        activity.Call("runOnUiThread", new AndroidJavaRunnable(() =>
        {
            using var appCtx = activity.Call<AndroidJavaObject>("getApplicationContext");
            using var toast  = toastCls.CallStatic<AndroidJavaObject>("makeText",
                                 appCtx, "Address copied", toastCls.GetStatic<int>("LENGTH_SHORT"));
            toast.Call("show");
        }));
    }
    catch { /* ignore: clipboard/Toast not critical */ }
#endif

        Debug.Log("Address copied: " + addr);
    }
    private void UpdateEnergyUI(int currentEnergy)
    {
        energyVal = currentEnergy;
        playerProfileEnergyTxt.text = $"{currentEnergy.ToString()}/{maxEnergy}";
        // Text
        if (energyText != null)
        {
            energyText.text = currentEnergy.ToString();
        }

        // Fill
        if (energyFill != null && maxEnergy > 0)
        {
            float normalized = Mathf.Clamp01((float)currentEnergy / maxEnergy);
            energyFill.fillAmount = normalized;
        }// Player Profile Energy Fill
        if (playerProfileenergyFill != null && maxEnergy > 0)
        {
            float normalized = Mathf.Clamp01((float)currentEnergy / maxEnergy);
            playerProfileenergyFill.fillAmount = normalized;
        }
    }
    // ============ USDC Energy Refill ============

    // private async void OnRefillEnergyUSDCClicked()
    // {
    //     if (PrivyManager.Instance?.User == null)
    //     {
    //         ShowStatus("❌ Please login first", Color.red);
    //         return;
    //     }

    //     if (!hasChef)
    //     {
    //         ShowStatus("❌ Please mint a chef first", Color.red);
    //         return;
    //     }

    //     await RefillEnergyWithUSDC();
    // }

    // public async Task RefillEnergyWithUSDC()
    // {
    //     try
    //     {
    //         ShowStatus("💰 Processing USDC payment...", Color.yellow);

    //         if (refillEnergyUSDCButton != null) refillEnergyUSDCButton.interactable = false;

    //         Debug.Log("💳 Calling backend for USDC refill...");
    //         Debug.Log("   Backend will handle USDC approval if needed");

    //         // Backend handles everything (approval + refill)
    //         var response = await GameAPIClient.Instance.RefillEnergyWithUSDC();

    //         if (response.success)
    //         {
    //             Debug.Log($"✅ Energy refilled with USDC!");
    //             Debug.Log($"   New Energy: {response.newEnergy}");
    //             Debug.Log($"   USDC Paid: {response.usdcPaid}");
    //             Debug.Log($"   Tx Hash: {response.txHash}");

    //             // Get fresh energy count
    //             var profile = await GameAPIClient.Instance.GetProfile();
    //             int currentEnergy = profile?.player?.energy ?? response.newEnergy;

    //             // Update energy UI
    //             if (energyText != null)
    //             {
    //                 energyText.text = $"⚡ Energy: {currentEnergy}";
    //             }

    //             // Show success with Basescan link
    //             string basescanUrl = $"https://sepolia.basescan.org/tx/{response.txHash}";

    //             ShowStatus(
    //                 $"✅ +300 Energy!\n💰 Spent: 0.1 USDC\n🔗 Opening transaction...", 
    //                 Color.green
    //             );

    //             Debug.Log($"🔗 View on Basescan: {basescanUrl}");
    //             Application.OpenURL(basescanUrl);
    //         }
    //         else
    //         {
    //             ShowStatus("❌ Refill failed", Color.red);
    //         }

    //         if (refillEnergyUSDCButton != null) refillEnergyUSDCButton.interactable = true;
    //     }
    //     catch (Exception e)
    //     {
    //         Debug.LogError($"❌ USDC refill error: {e.Message}");
    //         ShowStatus($"❌ Error: {e.Message}", Color.red);

    //         if (refillEnergyUSDCButton != null) refillEnergyUSDCButton.interactable = true;
    //     }
    // }

    // ============ Play Button ============

    private async void OnPlayButtonClicked()
    {
        LogTutorialGate("OnPlayButtonClicked", $"Clicked. hasChef={hasChef}, hasResolvedTutorialGate={hasResolvedTutorialGate}, mustPlayTutorialFirst={mustPlayTutorialFirst}, energyVal={energyVal}, prompt={DescribePanel(tutorialPromptPanel)}");

        // Guard against early clicks before profile gate has been resolved.
        if (hasChef && !hasResolvedTutorialGate)
        {
            LogTutorialGate("OnPlayButtonClicked", "Gate not resolved yet. Refreshing profile before deciding.");
            await RefreshProfileSilently();
            LogTutorialGate("OnPlayButtonClicked", $"After refresh. hasResolvedTutorialGate={hasResolvedTutorialGate}, mustPlayTutorialFirst={mustPlayTutorialFirst}");

            if (!hasResolvedTutorialGate)
            {
                ShowStatusMessage("Syncing profile. Please tap Play again.", Color.yellow, 3f);
                LogTutorialGate("OnPlayButtonClicked", "Gate unresolved after refresh. Blocking play to avoid incorrect mode.");
                return;
            }
        }

        if (mustPlayTutorialFirst)
        {
            if (!ShowTutorialPromptPanel("OnPlayButtonClicked"))
            {
                // Fallback: if prompt panel is not wired, force tutorial directly.
                LogTutorialGate("OnPlayButtonClicked", "Prompt panel is null, falling back to direct tutorial launch.");
                OnTutorialPromptYesClicked();
            }
            return;
        }

        GameSceneLaunchContext.SetNormalMode();

        // Start game session - SessionManager will handle it
        if (energyVal < 5)
        {
            ShowStatusMessage("Refill more energy", Color.red);
            return;
        }
        
        // Get button text component
        TMP_Text playButtonText = playButton?.GetComponentInChildren<TMP_Text>();
        string originalButtonText = playButtonText != null ? playButtonText.text : "Play";
        
        // Disable button and change text immediately
        if (playButton != null)
        {
            playButton.interactable = false;
        }
        if (playButtonText != null)
        {
            playButtonText.text = "Starting game...";
        }
        
        try
        {
            if (SessionManager.Instance != null)
            {
                await SessionManager.Instance.StartGameSession();
                // If successful, button state is handled by game session (game starts)
            }
            else
            {
                Debug.LogError("❌ SessionManager not found! Add SessionManager GameObject to scene.");
                ShowStatusMessage("Session manager missing", Color.red);
                
                // Restore button state on failure
                if (playButton != null)
                {
                    playButton.interactable = true;
                }
                if (playButtonText != null)
                {
                    playButtonText.text = originalButtonText;
                }
            }
        }
        catch (Exception e)
        {
            // Restore button state before handling error outcomes.
            if (playButton != null)
            {
                playButton.interactable = true;
            }
            if (playButtonText != null)
            {
                playButtonText.text = originalButtonText;
            }

            if (TryExtractActiveSessionId(e, out int activeSessionId))
            {
                ShowSessionRecoveryPrompt(activeSessionId);
                return;
            }

            Debug.LogError($"❌ Failed to start game session: {e.Message}");
            ShowStatusMessage($"Failed to start game: {e.Message}", Color.red);
        }
    }

    private void ResolvePanelReferences()
    {
        if (mainMenuUi == null)
        {
            mainMenuUi = FindFirstObjectByType<MainMenuUI>();
            if (mainMenuUi != null)
            {
                mainMenuUi.SetExternalControl(true);
            }
        }

        if (loginPanel == null)
        {
            var login = GameObject.Find("LoginPanel");
            if (login != null)
            {
                loginPanel = login;
            }
        }

        if (mintPanel == null)
        {
            var mint = GameObject.Find("MintPanel");
            if (mint != null)
            {
                mintPanel = mint;
            }
        }

        if (playPanel == null)
        {
            var play = GameObject.Find("PlaygamePanel");
            if (play == null)
            {
                play = GameObject.Find("PlayPanel");
            }
            if (play != null)
            {
                playPanel = play;
            }
        }
    }

    private void SetMenuUiState(MenuUiState state)
    {
        GameObject targetPanel = null;
        switch (state)
        {
            case MenuUiState.LoggedOut:
                targetPanel = loginPanel;
                break;
            case MenuUiState.NeedsMint:
                targetPanel = mintPanel;
                break;
            case MenuUiState.Ready:
                targetPanel = playPanel;
                break;
        }

        if (mainMenuUi != null && targetPanel != null)
        {
            if (mainMenuUi.SelectPanel(targetPanel))
            {
                return;
            }
        }

        if (loginPanel != null)
        {
            loginPanel.SetActive(state == MenuUiState.LoggedOut);
        }

        if (mintPanel != null)
        {
            mintPanel.SetActive(state == MenuUiState.NeedsMint);
        }

        if (playPanel != null)
        {
            playPanel.SetActive(state == MenuUiState.Ready);
        }
    }

    private string GetCurrentWalletAddress()
    {
        var wallet = PrivyManager.Instance?.User?.EmbeddedWallets?.FirstOrDefault();
        return wallet?.Address ?? string.Empty;
    }

    private bool TryApplyCachedChefState()
    {
        if (!cachedHasChef)
        {
            return false;
        }

        string walletAddress = GetCurrentWalletAddress();
        if (string.IsNullOrEmpty(walletAddress))
        {
            return false;
        }

        if (!walletAddress.Equals(cachedWalletAddress, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        hasChef = true;
        chefName = cachedChefName;
        tokenId = cachedChefTokenId;
        hasResolvedTutorialGate = false;

        if (playPanelUsernameTxt != null) playPanelUsernameTxt.text = chefName;
        if (chefTokenIdText != null) chefTokenIdText.text = $"Chef #{tokenId}: {chefName}";
        if (playerProfileChefIdTxt != null) playerProfileChefIdTxt.text = $"#{tokenId}";
        if (playerProfileUsernameTxt != null) playerProfileUsernameTxt.text = chefName;

        SetMenuUiState(MenuUiState.Ready);
        LogTutorialGate("TryApplyCachedChefState", $"Applied cached chef. wallet={GetCurrentWalletAddress()}, chef={chefName}, tokenId={tokenId}. Waiting for silent refresh.");
        return true;
    }

    private void ApplyReadyState(PlayerProfileResponse profile)
    {
        if (profile == null || profile.player == null)
        {
            return;
        }

        hasChef = true;
        chefName = profile.player.name;
        tokenId = profile.player.tokenId;

        if (playPanelUsernameTxt != null)
        {
            playPanelUsernameTxt.text = profile.player.name;
        }

        UpdateUI(profile);
        UpdateWalletDisplay();
        CacheChefState(chefName, tokenId);
        SetMenuUiState(MenuUiState.Ready);

        int sessionsPlayed = profile.globalStats != null ? profile.globalStats.totalSessionsPlayed : 0;
        bool hasCompletedTutorial = HasCompletedMandatoryTutorial();
        mustPlayTutorialFirst = sessionsPlayed <= 0 && !hasCompletedTutorial;
        hasResolvedTutorialGate = true;

        LogTutorialGate(
            "ApplyReadyState",
            $"wallet={GetCurrentWalletAddress()}, sessionsPlayed={sessionsPlayed}, hasCompletedTutorialLocal={hasCompletedTutorial}, mustPlayTutorialFirst={mustPlayTutorialFirst}");

        if (mustPlayTutorialFirst)
        {
            if (!ShowTutorialPromptPanel("ApplyReadyState"))
            {
                LogTutorialGate("ApplyReadyState", "mustPlayTutorialFirst=true but tutorialPromptPanel is NULL.");
            }
        }
        else if (tutorialPromptPanel != null)
        {
            tutorialPromptPanel.SetActive(false);
            LogTutorialGate("ApplyReadyState", $"Prompt hidden because gate=false. {DescribePanel(tutorialPromptPanel)}");
        }
    }

    private async Task RefreshProfileSilently()
    {
        await EnsureProfileRefreshAsync(showStatus: false, source: "RefreshProfileSilently");
    }

    private void CacheChefState(string name, int id)
    {
        string walletAddress = GetCurrentWalletAddress();
        if (string.IsNullOrEmpty(walletAddress))
        {
            return;
        }

        cachedHasChef = true;
        cachedWalletAddress = walletAddress;
        cachedChefName = name ?? string.Empty;
        cachedChefTokenId = id;
    }

    private void ClearCachedChefState()
    {
        cachedHasChef = false;
        cachedWalletAddress = string.Empty;
        cachedChefName = string.Empty;
        cachedChefTokenId = 0;
    }

    private string GetMandatoryTutorialKey()
    {
        string walletAddress = GetCurrentWalletAddress();
        if (string.IsNullOrEmpty(walletAddress))
        {
            return string.Empty;
        }

        return $"{TutorialDoneKeyPrefix}{walletAddress.ToLowerInvariant()}";
    }

    private bool HasCompletedMandatoryTutorial()
    {
        string key = GetMandatoryTutorialKey();
        if (string.IsNullOrEmpty(key))
        {
            LogTutorialGate("HasCompletedMandatoryTutorial", "No wallet key yet. Returning false.");
            return false;
        }

        int pref = PlayerPrefs.GetInt(key, 0);
        bool completed = pref == 1;
        LogTutorialGate("HasCompletedMandatoryTutorial", $"key={key}, pref={pref}, completed={completed}");
        return completed;
    }

    private void MarkMandatoryTutorialCompleted()
    {
        string key = GetMandatoryTutorialKey();
        if (string.IsNullOrEmpty(key))
        {
            LogTutorialGate("MarkMandatoryTutorialCompleted", "Skipped saving because wallet key is empty.");
            return;
        }

        PlayerPrefs.SetInt(key, 1);
        PlayerPrefs.Save();
        LogTutorialGate("MarkMandatoryTutorialCompleted", $"Saved tutorial completion key={key}");
    }

    public void ShowLoggedOutState()
    {
        hasChef = false;
        mustPlayTutorialFirst = false;
        hasResolvedTutorialGate = false;
        ClearCachedChefState();
        SetMenuUiState(MenuUiState.LoggedOut);

        if (tutorialPromptPanel != null)
        {
            tutorialPromptPanel.SetActive(false);
        }

        if (mintChefButton != null) mintChefButton.interactable = false;
        if (chefNameInput != null) chefNameInput.interactable = true;
        if (playButton != null) playButton.interactable = false;
        LogTutorialGate("ShowLoggedOutState", "Reset all profile and tutorial gate state to logged out.");
    }

    private void LogTutorialGate(string context, string message)
    {
        if (!enableTutorialGateDebug)
        {
            return;
        }

        Debug.Log($"[ChefManager][TutorialGate][{context}] {message}");
    }

    private string DescribePanel(GameObject panel)
    {
        if (panel == null)
        {
            return "null";
        }

        string parent = panel.transform.parent != null ? panel.transform.parent.name : "none";
        CanvasGroup group = panel.GetComponent<CanvasGroup>();
        string canvasGroupInfo = group == null
            ? "canvasGroup=none"
            : $"canvasGroup(alpha={group.alpha}, interactable={group.interactable}, blocksRaycasts={group.blocksRaycasts})";
        return $"name={panel.name}, activeSelf={panel.activeSelf}, activeInHierarchy={panel.activeInHierarchy}, parent={parent}, {canvasGroupInfo}";
    }

    private string GetHierarchyPath(Transform node)
    {
        if (node == null)
        {
            return "null";
        }

        string path = node.name;
        Transform current = node.parent;
        while (current != null)
        {
            path = $"{current.name}/{path}";
            current = current.parent;
        }

        return path;
    }

    private bool ShowTutorialPromptPanel(string context)
    {
        if (tutorialPromptPanel == null)
        {
            return false;
        }

        ConfigurePromptForTutorial();
        tutorialPromptPanel.SetActive(true);
        tutorialPromptPanel.transform.SetAsLastSibling();

        CanvasGroup group = tutorialPromptPanel.GetComponent<CanvasGroup>();
        if (group != null)
        {
            group.alpha = 1f;
            group.interactable = true;
            group.blocksRaycasts = true;
        }

        LogTutorialGate(context, $"Forced tutorial prompt visible. {DescribePanel(tutorialPromptPanel)}");
        return true;
    }

    private void ResolveTutorialPromptControls()
    {
        if (tutorialPromptPanel == null)
        {
            return;
        }

        if (tutorialPromptNoButton == null)
        {
            var buttons = tutorialPromptPanel.GetComponentsInChildren<Button>(true);
            tutorialPromptNoButton = buttons.FirstOrDefault(b =>
                b != null &&
                b != tutorialPromptYesButton &&
                b.name.Trim().Equals("NoButton", StringComparison.OrdinalIgnoreCase));
        }

        if (tutorialPromptText == null)
        {
            var texts = tutorialPromptPanel.GetComponentsInChildren<TMP_Text>(true);
            tutorialPromptText = texts.FirstOrDefault(t =>
                t != null && t.name.Trim().Equals("PromptTxt", StringComparison.OrdinalIgnoreCase));
        }

        if (tutorialPromptText != null && string.IsNullOrEmpty(defaultTutorialPromptMessage))
        {
            defaultTutorialPromptMessage = tutorialPromptText.text;
        }
    }

    private bool TryExtractActiveSessionId(Exception error, out int sessionId)
    {
        sessionId = 0;
        if (error == null || string.IsNullOrEmpty(error.Message))
        {
            return false;
        }

        string message = error.Message;
        if (message.IndexOf("session already active", StringComparison.OrdinalIgnoreCase) < 0)
        {
            return false;
        }

        int jsonStart = message.IndexOf('{');
        if (jsonStart >= 0)
        {
            string jsonText = message.Substring(jsonStart);
            try
            {
                var payload = JsonConvert.DeserializeObject<Dictionary<string, object>>(jsonText);
                if (payload != null && payload.TryGetValue("sessionId", out object rawSessionId))
                {
                    if (rawSessionId is long longId)
                    {
                        sessionId = (int)longId;
                        return sessionId > 0;
                    }

                    if (rawSessionId is int intId)
                    {
                        sessionId = intId;
                        return sessionId > 0;
                    }

                    if (int.TryParse(rawSessionId?.ToString(), out int parsedId))
                    {
                        sessionId = parsedId;
                        return sessionId > 0;
                    }
                }
            }
            catch (Exception parseError)
            {
                Debug.LogWarning($"⚠️ Could not parse active session payload: {parseError.Message}");
            }
        }

        return false;
    }

    private void ConfigurePromptForTutorial()
    {
        activePromptIntent = PromptIntent.Tutorial;
        pendingRecoverySessionId = 0;
        isProcessingSessionRecovery = false;

        if (tutorialPromptText != null && !string.IsNullOrEmpty(defaultTutorialPromptMessage))
        {
            tutorialPromptText.text = defaultTutorialPromptMessage;
        }

        if (tutorialPromptNoButton != null)
        {
            tutorialPromptNoButton.gameObject.SetActive(false);
            tutorialPromptNoButton.interactable = true;
        }

        if (tutorialPromptYesButton != null)
        {
            tutorialPromptYesButton.interactable = true;
        }
    }

    private void ShowSessionRecoveryPrompt(int sessionId)
    {
        if (tutorialPromptPanel == null || tutorialPromptYesButton == null)
        {
            ShowStatusMessage("Active session detected. Please wait for timeout or contact support.", Color.yellow, 5f);
            return;
        }

        ResolveTutorialPromptControls();
        activePromptIntent = PromptIntent.SessionRecovery;
        pendingRecoverySessionId = sessionId;
        isProcessingSessionRecovery = false;

        if (tutorialPromptText != null)
        {
            tutorialPromptText.text = sessionRecoveryPromptMessage;
        }

        tutorialPromptPanel.SetActive(true);
        tutorialPromptPanel.transform.SetAsLastSibling();

        if (tutorialPromptNoButton != null)
        {
            tutorialPromptNoButton.gameObject.SetActive(true);
            tutorialPromptNoButton.interactable = true;
        }

        tutorialPromptYesButton.interactable = true;
        ShowStatusMessage("Active session found. Choose Yes to end it and start a new one.", Color.yellow, 5f);
    }

    private void OnTutorialPromptNoClicked()
    {
        if (activePromptIntent != PromptIntent.SessionRecovery || isProcessingSessionRecovery)
        {
            if (tutorialPromptPanel != null)
            {
                tutorialPromptPanel.SetActive(false);
            }
            ConfigurePromptForTutorial();
            return;
        }

        if (tutorialPromptPanel != null)
        {
            tutorialPromptPanel.SetActive(false);
        }

        activePromptIntent = PromptIntent.None;
        pendingRecoverySessionId = 0;
        ShowStatusMessage(sessionRecoveryDeclineMessage, Color.yellow, 4f);
    }

    private async Task HandleSessionRecoveryYesAsync()
    {
        if (isProcessingSessionRecovery || pendingRecoverySessionId <= 0)
        {
            return;
        }

        isProcessingSessionRecovery = true;
        if (tutorialPromptYesButton != null)
        {
            tutorialPromptYesButton.interactable = false;
        }
        if (tutorialPromptNoButton != null)
        {
            tutorialPromptNoButton.interactable = false;
        }

        try
        {
            ShowStatusMessage("Ending previous session...", Color.yellow, 5f);
            await GameAPIClient.Instance.ForceEndSession(pendingRecoverySessionId, 0);

            if (tutorialPromptPanel != null)
            {
                tutorialPromptPanel.SetActive(false);
            }

            activePromptIntent = PromptIntent.None;
            pendingRecoverySessionId = 0;

            ShowStatusMessage("Previous session ended. Starting new session...", Color.green, 4f);
            int retryDelayMs = Mathf.Max(0, Mathf.RoundToInt(sessionRecoveryRetryDelaySeconds * 1000f));
            if (retryDelayMs > 0)
            {
                await Task.Delay(retryDelayMs);
            }
            if (SessionManager.Instance != null)
            {
                await SessionManager.Instance.StartGameSession();
            }
            else
            {
                ShowStatusMessage("Session manager missing", Color.red, 4f);
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"❌ Session recovery failed: {e.Message}");
            ShowStatusMessage($"Failed to end previous session: {e.Message}", Color.red, 5f);
        }
        finally
        {
            isProcessingSessionRecovery = false;
            if (tutorialPromptYesButton != null)
            {
                tutorialPromptYesButton.interactable = true;
            }
            if (tutorialPromptNoButton != null)
            {
                tutorialPromptNoButton.interactable = true;
            }
        }
    }

    // ============ Public Getters ============

    public bool HasChef() => hasChef;
    public string GetChefName() => chefName;
    public int GetTokenId() => tokenId;
}

