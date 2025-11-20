using System;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Privy;
using Newtonsoft.Json;

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
    [SerializeField] private Button playButton; // Play/Start Game button

    [SerializeField] private TMP_InputField chefNameInput;
    [SerializeField] private Button mintChefButton;
    [SerializeField] private Button refillEnergyButton; // Free energy refill
    [SerializeField] private Button refillEnergyUSDCButton; // USDC payment refill
    [SerializeField] private TMP_Text statusText;
    [SerializeField] private TMP_Text walletAddressText;
    [SerializeField] private TMP_Text chefTokenIdText;
    [SerializeField] private TMP_Text energyText;
    [SerializeField] private TMP_Text profileUsernameTxt;
    [SerializeField] private TMP_Text profilewalletAddressTxt;
    [SerializeField] private Image energyFill;
    [SerializeField] private int maxEnergy = 1000; 

    private int energyVal;

    [Header("Chef NFT Data")]
    private string chefName;
    private int tokenId;
    private bool hasChef;

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
            ShowStatus("❌ Privy not initialized. Please go to login scene first.", Color.red);
            if (mintChefButton != null) mintChefButton.interactable = false;
            yield break;
        }

        // Check if player is logged in
        if (PrivyManager.Instance?.User == null)
        {
            ShowStatus("⚠️ Please login first", Color.yellow);
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
            walletAddressText.text = $"Wallet: {address.Substring(0, 6)}...{address.Substring(address.Length - 4)}";
            profilewalletAddressTxt.text = $"{address.Substring(0, 6)}...{address.Substring(address.Length - 4)}";
        }
    }

    public async void CheckExistingChef()
    {
        try
        {
            ShowStatus("Checking for existing chef...", Color.yellow);
            
            var profile = await GameAPIClient.Instance.GetProfile();
            
            if (profile != null && profile.player != null)
            {
                // Player already has chef
                hasChef = true;
                chefName = profile.player.name;
                tokenId = profile.player.tokenId;
                profileUsernameTxt.text = profile.player.name;
                ShowStatus($"✅ Welcome back, {chefName}! (Chef #{tokenId})", Color.green);

                playPanel.SetActive(true);
                
                // HIDE mint panel - player already has chef
                if (mintPanel != null) mintPanel.SetActive(false);
                
                // Disable minting controls
                if (mintChefButton != null) mintChefButton.interactable = false;
                if (chefNameInput != null) chefNameInput.interactable = false;
                
                UpdateUI(profile);
                
                Debug.Log($"✅ Existing chef found: {chefName} (#{tokenId})");
            }
            else
            {
                // No chef found - show mint panel
                if (mintPanel != null) mintPanel.SetActive(true);
                
                ShowStatus("🎨 Create your chef to start playing!", Color.white);
                if (mintChefButton != null) mintChefButton.interactable = true;
            }
        }
        catch (Exception e)
        {
            // Likely 404 - no chef exists
            Debug.Log("No existing chef found (this is normal for new players)");
            
            // SHOW mint panel - new player needs to mint
            if (mintPanel != null) mintPanel.SetActive(true);
            
            ShowStatus("🎨 Create your chef to start playing!", Color.white);
            if (mintChefButton != null) mintChefButton.interactable = true;
        }
    }

    private async void OnMintChefClicked()
    {
        string name = chefNameInput?.text?.Trim();
        
        // Validation
        if (string.IsNullOrWhiteSpace(name))
        {
            ShowStatus("❌ Please enter a chef name", Color.red);
            return;
        }
        
        if (name.Length < 3 || name.Length > 20)
        {
            ShowStatus("❌ Name must be 3-20 characters", Color.red);
            return;
        }

        // Check if user is logged in
        if (PrivyManager.Instance?.User == null)
        {
            ShowStatus("❌ Please login first", Color.red);
            return;
        }

        await MintChef(name);
    }

    public async Task MintChef(string playerName)
    {
        try
        {
            ShowStatus("🎨 Minting your chef NFT...", Color.yellow);
            
            if (mintChefButton != null) mintChefButton.interactable = false;
            if (chefNameInput != null) chefNameInput.interactable = false;

            // Call backend API
            var response = await GameAPIClient.Instance.MintChef(playerName);

            if (response.success)
            {
                hasChef = true;
                chefName = response.name;
                tokenId = response.tokenId;
                
                ShowStatus($"✅ Chef minted! You are {chefName} (#{tokenId})", Color.green);
                
                if (response.gasSponsored)
                {
                    ShowStatus($"✅ {chefName} #{tokenId} | Gas: FREE (sponsored)", Color.green);
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
                
                // Show next step message
                ShowStatus($"✅ Chef ready! Refill energy to play.", Color.green);
            }
            else
            {
                ShowStatus("❌ Minting failed", Color.red);
                if (mintChefButton != null) mintChefButton.interactable = true;
                if (chefNameInput != null) chefNameInput.interactable = true;
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"❌ Mint chef error: {e.Message}");
            ShowStatus($"❌ Error: {e.Message}", Color.red);
            
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
        }
        
        Debug.Log("📊 Player Stats:");
        Debug.Log($"   Energy: {profile.player.energy}");
        Debug.Log($"   Sessions Played: {profile.globalStats.totalSessionsPlayed}");
        Debug.Log($"   Recipes Delivered: {profile.globalStats.totalRecipesDelivered}");
        Debug.Log($"   Well Cooked: {profile.globalStats.totalWellCooked}");
    }

    private void ShowStatus(string message, Color color)
    {
        if (statusText != null)
        {
            statusText.text = message;
            statusText.color = color;
        }
        
        Debug.Log($"[ChefManager] {message}");
    }

    // ============ Energy Refill ============
    
    private async void OnRefillEnergyClicked()
    {
        if (PrivyManager.Instance?.User == null)
        {
            ShowStatus("❌ Please login first", Color.red);
            return;
        }
        
        if (!hasChef)
        {
            ShowStatus("❌ Please mint a chef first", Color.red);
            return;
        }
        
        await RefillEnergy();
    }
    
    public async Task RefillEnergy()
    {
        try
        {
            ShowStatus("⚡ Refilling energy...", Color.yellow);
            
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
                
                ShowStatus(
                    $"✅ +300 Energy!\n💰 Cost: 0.1 USDC (FREE)\n🔗 Click to view transaction", 
                    Color.green
                );
                
                Debug.Log($"✅ Energy refilled!");
                Debug.Log($"🔗 View on {explorerName}: {txUrl}");
                Debug.Log($"💡 TIP: In production, this costs 0.1 USDC per refill");
                
                // Open explorer in browser
                Debug.Log($"🌐 Opening {explorerName}...");
                Application.OpenURL(txUrl);
            }
            else
            {
                ShowStatus("❌ Refill failed", Color.red);
            }
            
            if (refillEnergyButton != null) refillEnergyButton.interactable = true;
        }
        catch (Exception e)
        {
            Debug.LogError($"❌ Refill energy error: {e.Message}");
            ShowStatus($"❌ Error: {e.Message}", Color.red);
            
            if (refillEnergyButton != null) refillEnergyButton.interactable = true;
        }
    }
    private void UpdateEnergyUI(int currentEnergy)
    {
        energyVal = currentEnergy;

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
            ShowStatus("❌ Refill more energy", Color.red);
            return;
        }
        if (SessionManager.Instance != null)
        {
            await SessionManager.Instance.StartGameSession();
        }
        else
        {
            Debug.LogError("❌ SessionManager not found! Add SessionManager GameObject to scene.");
            ShowStatus("❌ Session manager missing", Color.red);
        }
    }

    // ============ Public Getters ============

    public bool HasChef() => hasChef;
    public string GetChefName() => chefName;
    public int GetTokenId() => tokenId;
}

