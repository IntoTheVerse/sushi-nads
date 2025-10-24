# Unity Chef Minting Integration Guide

**Status:** ✅ Scripts Created  
**Next:** Setup Scene + Test

---

## 📁 **New Scripts Created:**

### **1. GameAPIClient.cs** (`Assets/Scripts/API/GameAPIClient.cs`)
- HTTP client for backend communication
- Handles all API requests (chef, session, gameplay, leaderboard)
- Automatically adds Privy auth token to requests

### **2. ChefManager.cs** (`Assets/Scripts/ChefManager.cs`)
- Manages chef NFT minting
- Loads player profile
- Updates UI with chef info and stats

### **3. Updated: PrivyAndroidLogin.cs**
- Now saves Privy auth token after login
- Token used for authenticated backend requests

---

## 🎨 **Setup Minting Scene:**

### **Step 1: Create GameAPIClient GameObject** (5 mins)

1. In Unity Hierarchy: **Right-click → Create Empty**
2. Rename: `GameAPIClient`
3. Add Component: `GameAPIClient.cs`
4. Inspector → Set Base URL:
   ```
   Development: http://localhost:3000
   Production:  https://api.sushibonks.com
   ```
5. ✅ Mark as **DontDestroyOnLoad** (already in script)

### **Step 2: Setup Chef Minting UI** (10 mins)

Create a new scene or panel for chef minting:

**UI Elements Needed:**
```
ChefMintingPanel (Canvas Panel)
├── WalletAddressText (TextMeshPro)
│   └── Text: "Wallet: 0x..."
│
├── ChefNameInput (TMP_InputField)
│   ├── Placeholder: "Enter Chef Name (3-20 chars)"
│   └── Character Limit: 20
│
├── MintChefButton (Button)
│   └── Text: "Mint Chef NFT"
│
├── StatusText (TextMeshPro)
│   └── Text: "🎨 Create your chef to start playing!"
│
├── ChefTokenIdText (TextMeshPro)
│   └── Text: "Chef #42: MasterChef"
│
└── EnergyText (TextMeshPro)
    └── Text: "⚡ 300/300"
```

### **Step 3: Create ChefManager GameObject** (5 mins)

1. Hierarchy: **Create Empty → Rename: ChefManager**
2. Add Component: `ChefManager.cs`
3. **Drag UI elements** to Inspector slots:
   - Chef Name Input → `chefNameInput`
   - Mint Chef Button → `mintChefButton`
   - Status Text → `statusText`
   - Wallet Address Text → `walletAddressText`
   - Chef Token ID Text → `chefTokenIdText`
   - Energy Text → `energyText`

---

## 🔧 **Important: Get Privy Auth Token**

**TODO:** Update `SaveAuthToken()` in `PrivyAndroidLogin.cs`

The current implementation needs you to extract the actual auth token from Privy SDK:

```csharp
private void SaveAuthToken()
{
    try
    {
        var user = PrivyManager.Instance.User;
        if (user != null)
        {
            // ⚠️ TODO: Get actual token from Privy SDK
            // Check these possible locations:
            
            // Option 1: Check PrivyUser class
            string token = user.AccessToken;  // or user.Token, user.AuthToken, etc.
            
            // Option 2: Check PrivyManager
            string token = PrivyManager.Instance.GetAccessToken();
            
            // Option 3: Look for Session property
            string token = PrivyManager.Instance.Session?.AccessToken;
            
            if (!string.IsNullOrEmpty(token))
            {
                PlayerPrefs.SetString("privyAuthToken", token);
                PlayerPrefs.Save();
                Debug.Log("✅ Privy auth token saved");
            }
        }
    }
    catch (Exception e)
    {
        Debug.LogWarning($"Failed to save auth token: {e.Message}");
    }
}
```

**To Find the Token:**
1. Open `PrivyUser` class definition (your Privy Unity SDK)
2. Look for properties like: `AccessToken`, `Token`, `AuthToken`, `IdToken`
3. Or check `PrivyManager.Instance` for token getter methods
4. Update line 138 in `PrivyAndroidLogin.cs` with correct property

---

## 🎮 **Flow Diagram:**

```
1. User Opens Game
   ↓
2. PrivyAndroidLogin Scene
   → User logs in (Email/Google/Twitter)
   → Wallet created automatically
   → Auth token saved ✅
   ↓
3. Chef Minting Scene
   → ChefManager checks for existing chef
   → If no chef:
     - Show input field
     - User enters name
     - Click "Mint Chef"
   ↓
4. Minting Process
   → ChefManager calls GameAPIClient.MintChef()
   → GameAPIClient sends POST to backend with token
   → Backend verifies token with Privy
   → Backend sends sponsored transaction ✅
   → Transaction minted from USER's wallet
   → Gas paid by Privy (FREE!) ✅
   ↓
5. Success!
   → Chef NFT minted
   → Display: "Chef #42: MasterChef"
   → Show energy: "⚡ 300/300"
   → Enable "Play" button
```

---

## 🧪 **Testing Checklist:**

### **Pre-Flight:**
- [ ] Backend running on `localhost:3000` or deployed
- [ ] Privy gas sponsorship enabled in dashboard
- [ ] Contracts deployed and verified on Base Sepolia

### **In Unity:**
- [ ] GameAPIClient created with correct Base URL
- [ ] ChefManager UI references assigned
- [ ] Privy App ID and Client ID set in PrivyAndroidLogin

### **Test Flow:**
1. [ ] **Login:** Email OTP or Google/Twitter
2. [ ] **Check Console:** "✅ Privy auth token saved"
3. [ ] **Open Minting Scene:** Should show wallet address
4. [ ] **Enter Name:** e.g., "TestChef123"
5. [ ] **Click Mint:** Should show "Minting..."
6. [ ] **Wait ~5 seconds:** For transaction confirmation
7. [ ] **Success:** Should show "✅ Chef minted! You are TestChef123 (#1)"
8. [ ] **Check Backend Logs:** Should see "✅ Sponsored tx sent"
9. [ ] **Verify on Basescan:** Transaction should be from user's wallet

---

## 🐛 **Common Issues & Fixes:**

### **Issue: "No auth token found"**
**Fix:** Check `SaveAuthToken()` is getting the actual Privy token
- Debug print `user` object to see available properties
- Consult Privy Unity SDK docs for token access

### **Issue: "401 Unauthorized" from backend**
**Fix:** Backend can't verify Privy token
- Check Privy App ID matches in Unity and backend .env
- Verify `PRIVY_APP_SECRET` is correct in backend
- Test token verification with curl

### **Issue: "Minting failed"**
**Fix:** Check backend logs for specific error
- Energy might be insufficient
- Player might already have chef
- Contract might not be whitelisted for gas sponsorship

### **Issue: "Transaction times out"**
**Fix:** 
- Increase wait time from 3s to 5-10s
- Base Sepolia can be slow during high traffic
- Check RPC URL is working

---

## 📱 **Build Settings:**

For Android/iOS build:

1. **Update Base URL** in GameAPIClient:
   ```csharp
   private string baseUrl = "https://api.sushibonks.com";
   ```

2. **Test Deep Linking:**
   - Redirect URI: `sushibonks://`
   - Must match Privy dashboard config

3. **Enable Internet Permission** (Android):
   - Player Settings → Android → Internet Access: Require

---

## 🚀 **Next Steps After Minting:**

Once minting works:
1. ✅ Create Session Start UI
2. ✅ Integrate Gameplay (moves, recipes)
3. ✅ Add Leaderboard Screen
4. ✅ Profile Screen (global stats)

**Want to proceed with Session Start integration?** Let me know! 🎮

