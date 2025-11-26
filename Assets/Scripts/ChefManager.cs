using System;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Privy;
using Newtonsoft.Json;
using System.Collections;

/// <summary>
/// Manages chef NFT minting and player profile
/// Integrates Privy authentication with backend API
/// </summary>
public class ChefManager : MonoBehaviour
{
    public static ChefManager Instance { get; private set; }

    [Header("UI References")]
    [SerializeField] private GameObject mintPanel; // Panel to show/hide based on chef status
    [SerializeField] private GameObject playPanel; // Panel to show/hide based on chef status
    [SerializeField] private GameObject GetEnergyPanel; // Panel to show/hide based on chef status
    [SerializeField] private GameObject StatusPanel; 
    [SerializeField] private Button playButton; // Play/Start Game button

    [SerializeField] private TMP_InputField chefNameInput;
    [SerializeField] private Button mintChefButton;
    [SerializeField] private Button refillEnergyButton; // Free energy refill
    [SerializeField] private Button refillEnergyUSDCButton; // USDC payment refill
    [SerializeField] private Button copyWABtn; // USDC payment refill
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
    
    private Coroutine currentStatusCoroutine;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        
        Instance = this;
        
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
        }
        if (copyWABtn != null)
        {
            copyWABtn.onClick.AddListener(CopyWalletAddress);
        }
        // if (refillEnergyUSDCButton != null)
        // {
        //     refillEnergyUSDCButton.onClick.AddListener(OnRefillEnergyUSDCClicked);
        // }
    }

    private void Start()
    {
        // Wait for Privy to initialize
        StartCoroutine(WaitForPrivyAndInitialize());
    }

    private System.Collections.IEnumerator WaitForPrivyAndInitialize()
    {
        // Wait for PrivyAndroidLogin to initialize Privy first
        yield return new WaitForSeconds(2f);
        
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
            ShowStatusMessage("Privy not initialized. Please go to login scene first.", Color.red);
            if (mintChefButton != null) mintChefButton.interactable = false;
            yield break;
        }

        // Check if player is logged in
        if (PrivyManager.Instance?.User == null)
        {
            ShowStatusMessage("Please login first", Color.yellow);
            if (mintChefButton != null) mintChefButton.interactable = false;
            yield break;
        }

        // Display wallet address
        UpdateWalletDisplay();
        
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
        try
        {
            ShowStatusMessage("Checking for existing chef...", Color.yellow);
            
            // Update wallet display first (in case Privy wasn't ready earlier)
            UpdateWalletDisplay();
            
            var profile = await GameAPIClient.Instance.GetProfile();
            
            if (profile != null && profile.player != null)
            {
                // Player already has chef
                hasChef = true;
                chefName = profile.player.name;
                tokenId = profile.player.tokenId;
                playPanelUsernameTxt.text = profile.player.name;
                ShowStatusMessage($"Welcome back, {chefName}! (Chef #{tokenId})", Color.green);

                playPanel.SetActive(true);
                
                // HIDE mint panel - player already has chef
                if (mintPanel != null) mintPanel.SetActive(false);
                
                // Disable minting controls
                if (mintChefButton != null) mintChefButton.interactable = false;
                if (chefNameInput != null) chefNameInput.interactable = false;
                
                UpdateUI(profile);
                
                // Update wallet display again after profile is loaded (ensures it's set)
                UpdateWalletDisplay();
                
                Debug.Log($"✅ Existing chef found: {chefName} (#{tokenId})");
            }
            else
            {
                // No chef found - show mint panel
                if (mintPanel != null) mintPanel.SetActive(true);
                
                ShowStatusMessage("Create your chef to start playing!", Color.white);
                if (mintChefButton != null) mintChefButton.interactable = true;
            }
        }
        catch (Exception e)
        {
            // Likely 404 - no chef exists
            Debug.Log("No existing chef found (this is normal for new players)");
            
            // SHOW mint panel - new player needs to mint
            if (mintPanel != null) mintPanel.SetActive(true);
            
            // Still try to update wallet display even if no chef exists
            UpdateWalletDisplay();
            
            ShowStatusMessage("Create your chef to start playing!", Color.white);
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
                
                ShowStatusMessage($"Chef minted! You are {chefName} (#{tokenId})", Color.green);
                
                if (response.gasSponsored)
                {
                    ShowStatusMessage($"{chefName} #{tokenId} | Gas: FREE (sponsored)", Color.green);
                }
                playPanel.SetActive(true);

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
                if (mintPanel != null) mintPanel.SetActive(false);
                
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
                UpdateUI(profile);
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
        ColorUtility.TryParseHtmlString("#E8914C", out color);
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
        // Start game session - SessionManager will handle it
        if (energyVal < 5)
        {
            ShowStatusMessage("Refill more energy", Color.red);
            return;
        }
        if (SessionManager.Instance != null)
        {
            await SessionManager.Instance.StartGameSession();
        }
        else
        {
            Debug.LogError("❌ SessionManager not found! Add SessionManager GameObject to scene.");
            ShowStatusMessage("Session manager missing", Color.red);
        }
    }

    // ============ Public Getters ============

    public bool HasChef() => hasChef;
    public string GetChefName() => chefName;
    public int GetTokenId() => tokenId;
}

