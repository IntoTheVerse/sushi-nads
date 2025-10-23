# Privy Integration Guide - Sushi Bonks

Complete implementation guide for integrating Privy embedded wallets with Unity client and Node.js backend.

---

## 📋 Table of Contents

1. [Prerequisites](#prerequisites)
2. [Privy Dashboard Setup](#privy-dashboard-setup)
3. [Unity Integration](#unity-integration)
4. [Backend Integration](#backend-integration)
5. [Testing](#testing)
6. [Troubleshooting](#troubleshooting)

---

## 1. Prerequisites

### **Required Accounts**
- [ ] Privy account ([privy.io](https://privy.io))
- [ ] GCP account (for backend hosting)
- [ ] Firebase project
- [ ] Base Sepolia ETH for contract deployment

### **Required Tools**
- [ ] Unity 2022.3+ LTS
- [ ] Node.js 20 LTS
- [ ] Git

### **Privy Documentation**
- [Unity SDK Installation](https://docs.privy.io/basics/unity/installation)
- [Unity SDK Setup](https://docs.privy.io/basics/unity/setup)
- [Node.js Server Auth](https://docs.privy.io/basics/nodeJS/quickstart)
- [Gas Sponsorship](https://docs.privy.io/wallets/gas-and-asset-management/gas/ethereum#nodejs)

---

## 2. Privy Dashboard Setup

### **Step 1: Create Privy App**

1. Go to [dashboard.privy.io](https://dashboard.privy.io)
2. Click "Create App"
3. Name: "Sushi Bonks"
4. Select: **Embedded Wallets**

### **Step 2: Configure App Settings**

#### **General Settings**
```
App Name: Sushi Bonks
App ID: clxxx... (auto-generated)
```

#### **Authentication Methods**
Enable the following:
- ✅ Email (OTP)
- ✅ Google OAuth (optional)
- ✅ Discord OAuth (optional)

#### **Embedded Wallet Settings**
```
Wallet Provider: Privy
Chain Support: Ethereum (Base Sepolia)
Create wallet on: Sign up
```

#### **Security Settings**
```
Allowed Origins:
  - http://localhost:3000 (dev)
  - https://sushibonks.com (prod)
  - chrome-extension://* (Unity WebGL)

Session Duration: 7 days
Require re-auth for: Sensitive actions
```

### **Step 3: Create App Client (for Unity)**

According to [Privy docs](https://docs.privy.io/basics/unity/setup#prerequisites), Unity requires an **app client**:

1. Go to **Settings** → **App Clients**
2. Click **Create App Client**
3. Configuration:
```
Name: Unity Client
Type: Native Mobile App
Redirect URI: 
  - sushibonks://callback
  - http://localhost (for testing)
```

4. Save and note:
   - **App ID**: `clxxx...`
   - **Client ID**: `client_xxx...`

### **Step 4: Enable Gas Sponsorship**

1. Go to **Wallets** → **Gas Sponsorship**
2. Click **Enable Gas Sponsorship**
3. Configure:
```
Networks: 
  - Base Sepolia (testnet)
  
Spend Limits:
  - Daily: $50 USD
  - Per Transaction: $0.50 USD
  - Per User (daily): $5 USD

Allowed Contracts:
  - ChefNFT: 0x... (your deployed address)
  - GameCore: 0x...
  - Leaderboard: 0x...
```

4. **Fund Sponsorship Wallet**:
   - Privy will give you a deposit address
   - Send Base Sepolia ETH to this address
   - Minimum: 0.1 ETH (~$200 worth for testing)

### **Step 5: Get API Keys**

1. Go to **Settings** → **API Keys**
2. Create server-side key:
```
Name: Backend Server
Environment: Production
Permissions: Full access
```

3. Note down:
   - **App ID**: `clxxx...`
   - **App Secret**: `secret_xxx...` (⚠️ Keep secret!)
   - **Client ID**: `client_xxx...`

---

## 3. Unity Integration

### **Step 1: Request Privy Unity SDK Access**

According to [Privy docs](https://docs.privy.io/basics/unity/installation), the SDK is distributed as `.unitypackage`:

1. Email: [email protected]
2. Subject: "Unity SDK Access Request - Sushi Bonks"
3. Include: Your Privy App ID

### **Step 2: Install Privy Unity Package**

Once you receive the `.unitypackage`:

1. Open Unity project
2. **Assets** → **Import Package** → **Custom Package**
3. Select `Privy-Unity-SDK.unitypackage`
4. Import all assets

**Dependencies** (should auto-detect):
- `Newtonsoft.Json` (already in your project)
- `unity-webview`

### **Step 3: Configure WebGL Build Settings**

If building for WebGL (per [Privy WebGL setup](https://docs.privy.io/basics/unity/installation#webgl-setup)):

1. **Edit** → **Project Settings** → **Player** → **WebGL**
2. **Resolution and Presentation**:
   - Template: `unity-webview` or `unity-webview-2020`
3. **Other Settings** → **Optimization**:
   - Managed Stripping Level: **Minimal**

### **Step 4: Create Privy Manager Script**

Create `Assets/Scripts/Privy/PrivyAuthManager.cs`:

```csharp
using UnityEngine;
using Privy;
using System;
using System.Threading.Tasks;

public class PrivyAuthManager : MonoBehaviour
{
    public static PrivyAuthManager Instance { get; private set; }

    [Header("Privy Configuration")]
    [SerializeField] private string appId = "clxxx..."; // Your Privy App ID
    [SerializeField] private string clientId = "client_xxx..."; // Your Client ID

    private PrivyUser currentUser;
    
    public event Action<PrivyUser> OnLoginSuccess;
    public event Action<string> OnLoginFailed;
    public event Action OnLogout;

    private void Awake()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }
        
        Instance = this;
        DontDestroyOnLoad(gameObject);
        
        InitializePrivy();
    }

    private void InitializePrivy()
    {
        var config = new PrivyConfig
        {
            AppId = appId,
            ClientId = clientId
        };

        try
        {
            PrivyManager.Initialize(config);
            Debug.Log("✅ Privy initialized successfully");
            
            // Check if user is already authenticated
            CheckAuthStatus();
        }
        catch (Exception ex)
        {
            Debug.LogError($"❌ Privy initialization failed: {ex.Message}");
        }
    }

    private void CheckAuthStatus()
    {
        if (PrivyManager.Instance.IsAuthenticated())
        {
            currentUser = PrivyManager.Instance.User;
            Debug.Log($"✅ User already authenticated: {currentUser.Id}");
            OnLoginSuccess?.Invoke(currentUser);
        }
    }

    // STEP 1: Send OTP to email
    public async Task<bool> SendEmailOTP(string email)
    {
        try
        {
            await PrivyManager.Instance.Email.SendCode(email);
            Debug.Log($"✅ OTP sent to {email}");
            return true;
        }
        catch (Exception ex)
        {
            Debug.LogError($"❌ Failed to send OTP: {ex.Message}");
            OnLoginFailed?.Invoke($"Failed to send code: {ex.Message}");
            return false;
        }
    }

    // STEP 2: Verify OTP and login
    public async Task<bool> VerifyEmailOTP(string email, string code)
    {
        try
        {
            var user = await PrivyManager.Instance.Email.LoginWithCode(email, code);
            currentUser = user;
            
            Debug.Log($"✅ Login successful!");
            Debug.Log($"   User ID: {user.Id}");
            Debug.Log($"   Email: {user.Email?.Address}");
            
            // Check if user has embedded wallet
            if (user.EmbeddedWallets != null && user.EmbeddedWallets.Count > 0)
            {
                var wallet = user.EmbeddedWallets[0];
                Debug.Log($"   Wallet: {wallet.Address}");
                
                // Store auth token for API calls
                PlayerPrefs.SetString("privyAuthToken", user.Token);
                PlayerPrefs.SetString("walletAddress", wallet.Address);
                PlayerPrefs.Save();
            }
            else
            {
                // Create embedded wallet
                await CreateEmbeddedWallet();
            }
            
            OnLoginSuccess?.Invoke(user);
            return true;
        }
        catch (PrivyException.AuthenticationException ex)
        {
            Debug.LogError($"❌ Authentication failed: {ex.Error} - {ex.Message}");
            OnLoginFailed?.Invoke($"Invalid code: {ex.Message}");
            return false;
        }
        catch (Exception ex)
        {
            Debug.LogError($"❌ Login failed: {ex.Message}");
            OnLoginFailed?.Invoke($"Login failed: {ex.Message}");
            return false;
        }
    }

    private async Task CreateEmbeddedWallet()
    {
        try
        {
            var wallet = await currentUser.CreateWallet();
            Debug.Log($"✅ Embedded wallet created: {wallet.Address}");
            
            PlayerPrefs.SetString("walletAddress", wallet.Address);
            PlayerPrefs.Save();
        }
        catch (Exception ex)
        {
            Debug.LogError($"❌ Failed to create wallet: {ex.Message}");
        }
    }

    public void Logout()
    {
        try
        {
            PrivyManager.Instance.Logout();
            currentUser = null;
            
            PlayerPrefs.DeleteKey("privyAuthToken");
            PlayerPrefs.DeleteKey("walletAddress");
            PlayerPrefs.Save();
            
            Debug.Log("✅ Logged out successfully");
            OnLogout?.Invoke();
        }
        catch (Exception ex)
        {
            Debug.LogError($"❌ Logout failed: {ex.Message}");
        }
    }

    // Getters
    public bool IsAuthenticated() => currentUser != null;
    public PrivyUser GetUser() => currentUser;
    public string GetWalletAddress() => currentUser?.EmbeddedWallets?[0]?.Address ?? "";
    public string GetAuthToken() => PlayerPrefs.GetString("privyAuthToken", "");
}
```

### **Step 5: Create Login UI**

Create `Assets/Scripts/UI/LoginUI.cs`:

```csharp
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class LoginUI : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private TMP_InputField emailInput;
    [SerializeField] private TMP_InputField codeInput;
    [SerializeField] private Button sendCodeButton;
    [SerializeField] private Button verifyCodeButton;
    [SerializeField] private TextMeshProUGUI statusText;
    [SerializeField] private GameObject codePanel;

    private void Start()
    {
        codePanel.SetActive(false);
        
        sendCodeButton.onClick.AddListener(OnSendCodeClicked);
        verifyCodeButton.onClick.AddListener(OnVerifyCodeClicked);
        
        // Subscribe to Privy events
        PrivyAuthManager.Instance.OnLoginSuccess += OnLoginSuccess;
        PrivyAuthManager.Instance.OnLoginFailed += OnLoginFailed;
        
        // Check if already logged in
        if (PrivyAuthManager.Instance.IsAuthenticated())
        {
            OnLoginSuccess(PrivyAuthManager.Instance.GetUser());
        }
    }

    private async void OnSendCodeClicked()
    {
        string email = emailInput.text.Trim();
        
        if (string.IsNullOrEmpty(email) || !email.Contains("@"))
        {
            ShowStatus("❌ Please enter a valid email", Color.red);
            return;
        }
        
        sendCodeButton.interactable = false;
        ShowStatus("📧 Sending code...", Color.yellow);
        
        bool success = await PrivyAuthManager.Instance.SendEmailOTP(email);
        
        if (success)
        {
            ShowStatus("✅ Code sent! Check your email", Color.green);
            codePanel.SetActive(true);
        }
        else
        {
            sendCodeButton.interactable = true;
        }
    }

    private async void OnVerifyCodeClicked()
    {
        string email = emailInput.text.Trim();
        string code = codeInput.text.Trim();
        
        if (string.IsNullOrEmpty(code))
        {
            ShowStatus("❌ Please enter the code", Color.red);
            return;
        }
        
        verifyCodeButton.interactable = false;
        ShowStatus("🔐 Verifying...", Color.yellow);
        
        await PrivyAuthManager.Instance.VerifyEmailOTP(email, code);
    }

    private void OnLoginSuccess(PrivyUser user)
    {
        ShowStatus("✅ Login successful!", Color.green);
        
        // Navigate to main menu
        UnityEngine.SceneManagement.SceneManager.LoadScene("MainMenuScene");
    }

    private void OnLoginFailed(string error)
    {
        ShowStatus($"❌ {error}", Color.red);
        sendCodeButton.interactable = true;
        verifyCodeButton.interactable = true;
    }

    private void ShowStatus(string message, Color color)
    {
        statusText.text = message;
        statusText.color = color;
    }

    private void OnDestroy()
    {
        PrivyAuthManager.Instance.OnLoginSuccess -= OnLoginSuccess;
        PrivyAuthManager.Instance.OnLoginFailed -= OnLoginFailed;
    }
}
```

### **Step 6: Create API Client**

Create `Assets/Scripts/API/GameAPIClient.cs`:

```csharp
using UnityEngine;
using UnityEngine.Networking;
using System;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

public class GameAPIClient : MonoBehaviour
{
    public static GameAPIClient Instance { get; private set; }

    [Header("Configuration")]
    [SerializeField] private string baseUrl = "https://api.sushibonks.com";
    // For local testing: "http://localhost:3000"

    private void Awake()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }
        
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    // Generic POST request
    private async Task<T> PostAsync<T>(string endpoint, object body = null)
    {
        string url = $"{baseUrl}{endpoint}";
        string jsonBody = body != null ? JsonConvert.SerializeObject(body) : "{}";
        
        using (UnityWebRequest request = new UnityWebRequest(url, "POST"))
        {
            byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonBody);
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            
            // Add headers
            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("Authorization", $"Bearer {PrivyAuthManager.Instance.GetAuthToken()}");
            
            // Send request
            await request.SendWebRequest();
            
            if (request.result == UnityWebRequest.Result.Success)
            {
                string responseText = request.downloadHandler.text;
                return JsonConvert.DeserializeObject<T>(responseText);
            }
            else
            {
                Debug.LogError($"❌ API Error: {request.error}");
                throw new Exception($"API request failed: {request.error}");
            }
        }
    }

    // Generic GET request
    private async Task<T> GetAsync<T>(string endpoint)
    {
        string url = $"{baseUrl}{endpoint}";
        
        using (UnityWebRequest request = UnityWebRequest.Get(url))
        {
            request.SetRequestHeader("Authorization", $"Bearer {PrivyAuthManager.Instance.GetAuthToken()}");
            
            await request.SendWebRequest();
            
            if (request.result == UnityWebRequest.Result.Success)
            {
                string responseText = request.downloadHandler.text;
                return JsonConvert.DeserializeObject<T>(responseText);
            }
            else
            {
                Debug.LogError($"❌ API Error: {request.error}");
                throw new Exception($"API request failed: {request.error}");
            }
        }
    }

    // === CHEF NFT APIs ===
    
    public async Task<MintChefResponse> MintChef(string playerName)
    {
        return await PostAsync<MintChefResponse>("/api/chef/mint", new { playerName });
    }

    public async Task<PlayerProfile> GetProfile()
    {
        return await GetAsync<PlayerProfile>("/api/chef/profile");
    }

    public async Task<RefillEnergyResponse> RefillEnergy()
    {
        return await PostAsync<RefillEnergyResponse>("/api/chef/refill-energy");
    }

    // === SESSION APIs ===
    
    public async Task<StartSessionResponse> StartSession()
    {
        return await PostAsync<StartSessionResponse>("/api/session/start");
    }

    public async Task<EndSessionResponse> EndSession(int sessionId)
    {
        return await PostAsync<EndSessionResponse>("/api/session/end", new { sessionId });
    }

    // === GAMEPLAY APIs ===
    
    public async Task RecordMove(int sessionId)
    {
        await PostAsync<object>("/api/gameplay/move", new { sessionId });
    }

    public async Task RecordRecipeDelivered(int sessionId)
    {
        await PostAsync<object>("/api/gameplay/recipe-delivered", new { sessionId });
    }

    public async Task RecordRecipeDiscarded(int sessionId)
    {
        await PostAsync<object>("/api/gameplay/recipe-discarded", new { sessionId });
    }

    // === LEADERBOARD APIs ===
    
    public async Task<LeaderboardResponse> GetTopPlayers(int limit = 100)
    {
        return await GetAsync<LeaderboardResponse>($"/api/leaderboard/top?limit={limit}");
    }
}

// Response models
[Serializable]
public class MintChefResponse
{
    public bool success;
    public string txHash;
    public int tokenId;
}

[Serializable]
public class PlayerProfile
{
    public PlayerInfo player;
    public GlobalStats globalStats;
    public PersonalRecords personalRecords;
}

[Serializable]
public class PlayerInfo
{
    public string address;
    public string name;
    public int tokenId;
    public int energy;
    public int maxEnergy;
}

[Serializable]
public class GlobalStats
{
    public int totalSessionsPlayed;
    public int totalMoves;
    public int totalRecipesSpawned;
    public int totalRecipesDelivered;
    public int totalRecipesDiscarded;
    public int totalWellCooked;
    public int totalOverCooked;
}

// ... more models
```

### **Step 7: Unity Build Settings**

1. **File** → **Build Settings**
2. Add scenes:
   - LoginScene
   - MainMenuScene
   - GameScene
3. **Player Settings**:
   - Company Name: Your company
   - Product Name: Sushi Bonks
   - Bundle Identifier: com.yourcompany.sushibonks

---

## 4. Backend Integration

### **Step 1: Initialize Node.js Project**

```bash
mkdir backend
cd backend
npm init -y
```

### **Step 2: Install Dependencies**

```bash
npm install express
npm install @privy-io/server-auth
npm install ethers@6
npm install firebase-admin
npm install dotenv
npm install cors
npm install helmet

# Dev dependencies
npm install --save-dev nodemon typescript @types/node @types/express
```

### **Step 3: Create Environment File**

Create `.env`:

```bash
# Privy Configuration
PRIVY_APP_ID=clxxx...
PRIVY_APP_SECRET=secret_xxx...
PRIVY_CLIENT_ID=client_xxx...

# Blockchain
BASE_SEPOLIA_RPC_URL=https://sepolia.base.org
BACKEND_PRIVATE_KEY=0x... # Your backend signer private key

# Contract Addresses (from deployment)
CHEFNFT_ADDRESS=0x...
GAMECORE_ADDRESS=0x...
LEADERBOARD_ADDRESS=0x...

# Firebase
FIREBASE_PROJECT_ID=your-project-id
FIREBASE_CREDENTIALS={"type":"service_account",...}

# Server
PORT=3000
NODE_ENV=development
```

### **Step 4: Initialize Privy Server SDK**

Create `src/services/privy.service.js`:

```javascript
import { PrivyClient } from '@privy-io/server-auth';

const privy = new PrivyClient(
    process.env.PRIVY_APP_ID,
    process.env.PRIVY_APP_SECRET
);

// Verify authentication token
export async function verifyAuthToken(token) {
    try {
        const user = await privy.verifyAuthToken(token);
        return user;
    } catch (error) {
        throw new Error('Invalid authentication token');
    }
}

// Get user by ID
export async function getUserById(userId) {
    return await privy.getUser(userId);
}

// Relay transaction with gas sponsorship
export async function sponsorTransaction(txParams) {
    try {
        const sponsoredTx = await privy.relayTransaction({
            chain: 'base-sepolia',
            transaction: txParams
        });
        
        return sponsoredTx;
    } catch (error) {
        console.error('Gas sponsorship failed:', error);
        throw error;
    }
}

export default privy;
```

### **Step 5: Create Blockchain Service**

Create `src/services/blockchain.service.js`:

```javascript
import { ethers } from 'ethers';
import ChefNFTABI from '../abis/ChefNFT.json' assert { type: 'json' };
import GameCoreABI from '../abis/GameCore.json' assert { type: 'json' };
import LeaderboardABI from '../abis/Leaderboard.json' assert { type: 'json' };

// Initialize provider
const provider = new ethers.JsonRpcProvider(process.env.BASE_SEPOLIA_RPC_URL);

// Initialize backend signer
const signer = new ethers.Wallet(process.env.BACKEND_PRIVATE_KEY, provider);

// Initialize contracts
export const chefNFT = new ethers.Contract(
    process.env.CHEFNFT_ADDRESS,
    ChefNFTABI.abi,
    signer
);

export const gameCore = new ethers.Contract(
    process.env.GAMECORE_ADDRESS,
    GameCoreABI.abi,
    signer
);

export const leaderboard = new ethers.Contract(
    process.env.LEADERBOARD_ADDRESS,
    LeaderboardABI.abi,
    signer
);

// Helper functions
export async function waitForTransaction(tx) {
    const receipt = await tx.wait();
    return receipt;
}

export async function getTransactionGasCost(receipt) {
    const gasUsed = receipt.gasUsed;
    const gasPrice = receipt.effectiveGasPrice || receipt.gasPrice;
    return gasUsed * gasPrice;
}
```

### **Step 6: Create Authentication Middleware**

Create `src/middleware/auth.js`:

```javascript
import { verifyAuthToken } from '../services/privy.service.js';

export async function requireAuth(req, res, next) {
    try {
        const authHeader = req.headers.authorization;
        
        if (!authHeader || !authHeader.startsWith('Bearer ')) {
            return res.status(401).json({ error: 'No authorization token provided' });
        }
        
        const token = authHeader.replace('Bearer ', '');
        const user = await verifyAuthToken(token);
        
        // Attach user to request
        req.user = user;
        req.walletAddress = user.wallet?.address;
        
        next();
    } catch (error) {
        console.error('Auth error:', error);
        res.status(401).json({ error: 'Invalid or expired token' });
    }
}
```

### **Step 7: Create Chef Routes**

Create `src/routes/chef.routes.js`:

```javascript
import express from 'express';
import { chefNFT } from '../services/blockchain.service.js';
import { sponsorTransaction } from '../services/privy.service.js';
import { requireAuth } from '../middleware/auth.js';

const router = express.Router();

// POST /api/chef/mint - Mint Chef NFT (gas sponsored)
router.post('/mint', requireAuth, async (req, res) => {
    try {
        const { playerName } = req.body;
        const playerAddress = req.walletAddress;
        
        // Validate name
        if (!playerName || playerName.length < 3 || playerName.length > 20) {
            return res.status(400).json({ error: 'Invalid name (3-20 characters)' });
        }
        
        // Check if player already has chef
        const hasChef = await chefNFT.playerHasChef(playerAddress);
        if (hasChef) {
            return res.status(400).json({ error: 'Already has chef NFT' });
        }
        
        // Prepare transaction
        const tx = await chefNFT.mintChef.populateTransaction(playerName);
        
        // Sponsor transaction via Privy
        const sponsoredTx = await sponsorTransaction({
            from: playerAddress,
            to: process.env.CHEFNFT_ADDRESS,
            data: tx.data,
            value: '0x0'
        });
        
        // Wait for confirmation
        const receipt = await sponsoredTx.wait();
        
        // Get token ID from event
        const mintEvent = receipt.logs.find(log => 
            log.topics[0] === chefNFT.interface.getEvent('ChefMinted').topicHash
        );
        const tokenId = parseInt(mintEvent.topics[2], 16);
        
        res.json({
            success: true,
            txHash: receipt.hash,
            tokenId
        });
    } catch (error) {
        console.error('Mint chef error:', error);
        res.status(500).json({ error: error.message });
    }
});

// GET /api/chef/profile - Get player profile
router.get('/profile', requireAuth, async (req, res) => {
    try {
        const playerAddress = req.walletAddress;
        
        // Get chef info
        const hasChef = await chefNFT.playerHasChef(playerAddress);
        if (!hasChef) {
            return res.status(404).json({ error: 'No chef NFT found' });
        }
        
        const tokenId = await chefNFT.getChefTokenId(playerAddress);
        const name = await chefNFT.getChefName(tokenId);
        const energy = await chefNFT.getPlayerEnergy(playerAddress);
        
        // Get global stats from GameCore
        const globalStats = await gameCore.getPlayerGlobalStats(playerAddress);
        
        res.json({
            player: {
                address: playerAddress,
                name,
                tokenId: Number(tokenId),
                energy: Number(energy),
                maxEnergy: 300
            },
            globalStats: {
                totalSessionsPlayed: Number(globalStats.totalSessionsPlayed),
                totalMoves: Number(globalStats.totalMoves),
                totalRecipesSpawned: Number(globalStats.totalRecipesSpawned),
                totalRecipesDelivered: Number(globalStats.totalRecipesDelivered),
                totalRecipesDiscarded: Number(globalStats.totalRecipesDiscarded),
                totalWellCooked: Number(globalStats.totalWellCooked),
                totalOverCooked: Number(globalStats.totalOverCooked)
            }
        });
    } catch (error) {
        console.error('Get profile error:', error);
        res.status(500).json({ error: error.message });
    }
});

export default router;
```

### **Step 8: Create Main Server File**

Create `src/server.js`:

```javascript
import express from 'express';
import cors from 'cors';
import helmet from 'helmet';
import dotenv from 'dotenv';

// Import routes
import chefRoutes from './routes/chef.routes.js';
import sessionRoutes from './routes/session.routes.js';
import gameplayRoutes from './routes/gameplay.routes.js';
import leaderboardRoutes from './routes/leaderboard.routes.js';

dotenv.config();

const app = express();
const PORT = process.env.PORT || 3000;

// Middleware
app.use(helmet());
app.use(cors({
    origin: process.env.NODE_ENV === 'production' 
        ? 'https://sushibonks.com' 
        : '*'
}));
app.use(express.json());

// Health check
app.get('/health', (req, res) => {
    res.json({ status: 'ok', timestamp: new Date().toISOString() });
});

// Routes
app.use('/api/chef', chefRoutes);
app.use('/api/session', sessionRoutes);
app.use('/api/gameplay', gameplayRoutes);
app.use('/api/leaderboard', leaderboardRoutes);

// Error handler
app.use((err, req, res, next) => {
    console.error(err.stack);
    res.status(500).json({ error: 'Something went wrong!' });
});

// Start server
app.listen(PORT, () => {
    console.log(`🚀 Server running on port ${PORT}`);
    console.log(`📡 Environment: ${process.env.NODE_ENV}`);
    console.log(`⛓️  Network: Base Sepolia`);
    console.log(`🔐 Privy App ID: ${process.env.PRIVY_APP_ID}`);
});
```

### **Step 9: Copy Contract ABIs**

```bash
# Copy ABIs from blockchain/artifacts to backend
mkdir -p backend/src/abis
cp blockchain/artifacts/contracts/ChefNFT.sol/ChefNFT.json backend/src/abis/
cp blockchain/artifacts/contracts/GameCore.sol/GameCore.json backend/src/abis/
cp blockchain/artifacts/contracts/Leaderboard.sol/Leaderboard.json backend/src/abis/
```

### **Step 10: Add Package.json Scripts**

Update `package.json`:

```json
{
  "name": "sushi-bonks-backend",
  "type": "module",
  "scripts": {
    "start": "node src/server.js",
    "dev": "nodemon src/server.js",
    "test": "node --test"
  }
}
```

---

## 5. Testing

### **Test 1: Privy Authentication**

```bash
# Start backend
cd backend
npm run dev

# In Unity, test login flow
# Check Unity console for:
# ✅ Privy initialized successfully
# ✅ OTP sent to email
# ✅ Login successful!
# ✅ Wallet: 0x...
```

### **Test 2: API Authentication**

```bash
# Get auth token from Unity PlayerPrefs
# Test API call with curl
curl -X GET http://localhost:3000/api/chef/profile \
  -H "Authorization: Bearer YOUR_PRIVY_TOKEN"

# Should return 200 OK or 404 (if no chef minted yet)
```

### **Test 3: Mint Chef NFT**

```bash
# From Unity, call MintChef API
# Check backend logs for:
# - Privy token verification ✓
# - Gas sponsorship request ✓
# - Transaction confirmed ✓

# Verify on Basescan:
# https://sepolia.basescan.org/tx/YOUR_TX_HASH
```

### **Test 4: Start Game Session**

```javascript
// In Unity
async void TestSession() {
    var response = await GameAPIClient.Instance.StartSession();
    Debug.Log($"Session ID: {response.sessionId}");
    
    // Make some moves
    await GameAPIClient.Instance.RecordMove(response.sessionId);
    await GameAPIClient.Instance.RecordMove(response.sessionId);
    
    // End session
    var endResponse = await GameAPIClient.Instance.EndSession(response.sessionId);
    Debug.Log($"Final Score: {endResponse.finalScore}");
}
```

---

## 6. Troubleshooting

### **Unity Errors**

#### "Privy namespace not found"
```csharp
// Add using directive at top of file
using Privy;
```

#### "PrivyManager.Instance is null"
```csharp
// Make sure PrivyAuthManager is in scene and initialized
// Check that PrivyManager.Initialize() was called
```

#### "Authentication failed"
```
// Check:
// 1. App ID and Client ID are correct
// 2. Internet connection
// 3. Email/code are valid
```

### **Backend Errors**

#### "Invalid authentication token"
```javascript
// Check:
// 1. Token is sent in Authorization header
// 2. Token format: "Bearer YOUR_TOKEN"
// 3. PRIVY_APP_SECRET is correct
```

#### "Gas sponsorship failed"
```javascript
// Check:
// 1. Gas sponsorship is enabled in Privy dashboard
// 2. Contract addresses are whitelisted
// 3. Sponsorship wallet has funds
// 4. User hasn't hit spend limits
```

#### "Contract interaction failed"
```javascript
// Check:
// 1. Contract addresses are correct
// 2. ABIs are up-to-date
// 3. Backend signer has permissions
// 4. Network is Base Sepolia
```

---

## 7. Production Checklist

### **Privy Dashboard**
- [ ] Remove localhost from allowed origins
- [ ] Add production domain to allowed origins
- [ ] Set proper spend limits
- [ ] Fund sponsorship wallet adequately
- [ ] Enable monitoring/alerts

### **Unity Build**
- [ ] Update API base URL to production
- [ ] Update Privy App ID/Client ID
- [ ] Test on all target platforms
- [ ] Implement proper error handling
- [ ] Add loading states everywhere

### **Backend**
- [ ] Deploy to GCP Cloud Run
- [ ] Set environment variables in GCP
- [ ] Enable Cloud Logging
- [ ] Set up monitoring/alerts
- [ ] Configure auto-scaling
- [ ] Test all endpoints

### **Security**
- [ ] Rotate backend signer key
- [ ] Store secrets in GCP Secret Manager
- [ ] Enable rate limiting
- [ ] Add request validation
- [ ] Implement CORS properly
- [ ] Set up firewall rules

---

**Next Steps:**
1. Complete Unity integration
2. Test authentication flow
3. Deploy backend to GCP
4. Test gas sponsorship
5. Launch! 🚀

For additional help, see:
- [Privy Discord](https://discord.gg/privy)
- [Privy Documentation](https://docs.privy.io)
- Project `ARCHITECTURE.md`

