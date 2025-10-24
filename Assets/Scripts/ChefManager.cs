using System;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Privy;

/// <summary>
/// Manages chef NFT minting and player profile
/// Integrates Privy authentication with backend API
/// </summary>
public class ChefManager : MonoBehaviour
{
    public static ChefManager Instance { get; private set; }

    [Header("UI References")]
    [SerializeField] private TMP_InputField chefNameInput;
    [SerializeField] private Button mintChefButton;
    [SerializeField] private TMP_Text statusText;
    [SerializeField] private TMP_Text walletAddressText;
    [SerializeField] private TMP_Text chefTokenIdText;
    [SerializeField] private TMP_Text energyText;

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
        }
    }

    private async void CheckExistingChef()
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
                
                ShowStatus($"✅ You are: {chefName} (Chef #{tokenId})", Color.green);
                
                if (mintChefButton != null) mintChefButton.interactable = false;
                if (chefNameInput != null) chefNameInput.interactable = false;
                
                UpdateUI(profile);
            }
            else
            {
                // No chef found
                ShowStatus("🎨 Create your chef to start playing!", Color.white);
                if (mintChefButton != null) mintChefButton.interactable = true;
            }
        }
        catch (Exception e)
        {
            // Likely 404 - no chef exists
            Debug.Log("No existing chef found (this is normal for new players)");
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
                
                // Wait for blockchain to index the transaction
                Debug.Log("⏳ Waiting for blockchain confirmation...");
                await Task.Delay(5000); // 5 second delay
                
                // Load player profile
                await LoadPlayerProfile();
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
        // Update energy display
        if (energyText != null)
        {
            energyText.text = $"⚡ {profile.player.energy}/{profile.player.maxEnergy}";
        }
        
        // Update chef info
        if (chefTokenIdText != null)
        {
            chefTokenIdText.text = $"Chef #{profile.player.tokenId}: {profile.player.name}";
        }
        
        Debug.Log("📊 Player Stats:");
        Debug.Log($"   Energy: {profile.player.energy}/{profile.player.maxEnergy}");
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

    // ============ Public Getters ============

    public bool HasChef() => hasChef;
    public string GetChefName() => chefName;
    public int GetTokenId() => tokenId;
}

