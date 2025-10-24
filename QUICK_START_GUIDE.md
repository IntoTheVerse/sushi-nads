# 🚀 Quick Start Guide - Sushi Bonks

**Complete integration ready for testing!**

---

## ✅ **What's Been Completed:**

### **1. Smart Contracts** ✅
- ChefNFT, GameCore, Leaderboard deployed to Base Sepolia
- All contracts verified on Basescan
- Gas sponsorship compatible (hybrid auth)

### **2. Backend API** ✅
- Node.js/Express server configured
- Privy authentication integrated
- Session signers for gas sponsorship
- All endpoints functional

### **3. Unity Scripts** ✅
- `GameAPIClient.cs` - HTTP client
- `ChefManager.cs` - Minting logic
- `PrivyTokenHelper.cs` - Auth token extraction
- `PrivyAndroidLogin.cs` - Updated with token saving

---

## 📋 **Pre-Flight Checklist:**

### **Backend (Already Running):**
- [x] Backend running on `localhost:3000`
- [x] Contracts deployed and linked
- [x] Privy credentials configured
- [x] Gas sponsorship enabled

Test:
```bash
curl http://localhost:3000/health
# Should return: {"status":"ok"}
```

### **Unity Setup (DO NOW):**

#### **Step 1: Update Privy Credentials** (2 mins)
1. Open Unity project
2. Find `PrivyAndroidLogin` script (in scene or prefab)
3. Update Inspector fields:
   ```
   App ID:    cmh0lyy8p00y7i80c9o3eg6ue
   Client ID: client-WY6SH3G7kxffDVYNsAhwRXPqxQ6R5xYgwDVPrRspWHrmd
   ```

#### **Step 2: Create GameAPIClient** (2 mins)
1. Hierarchy → Create Empty GameObject
2. Rename: `GameAPIClient`
3. Add Component: `GameAPIClient.cs`
4. Inspector → Base URL: `http://localhost:3000`

#### **Step 3: Setup Minting Scene** (10 mins)
See `UNITY_MINTING_INTEGRATION.md` for full UI setup

**Quick Version:**
1. Create minting panel/scene
2. Add: Input field (name), Button (mint), 3x Text fields (status, wallet, chef info)
3. Add `ChefManager.cs` component
4. Assign UI references in Inspector

---

## 🧪 **Test Flow:**

### **Test 1: Login & Token Extraction** (5 mins)

1. **Play** the scene with `PrivyAndroidLogin`
2. **Login** with email OTP or Google/Twitter
3. **Check console** for:
   ```
   === Privy User Object Debug ===
   User ID: did:privy:xxx
   Available properties on User:
     ...
   ✅ Found potential token: [PropertyName]
   ✅ Privy token saved: eyJhbGciOi...
   ```

4. **If no token found:**
   - Check console output
   - Identify which property has the JWT token
   - Update `PrivyTokenHelper.cs` line ~30-50
   - Uncomment the correct property access

### **Test 2: Mint Chef NFT** (10 mins)

**Requirements:**
- Backend running
- Logged into Privy
- Token saved successfully

**Steps:**
1. **Open minting scene**
2. **Enter name:** e.g., "TestChef123"
3. **Click "Mint Chef"**
4. **Watch console:**
   ```
   🎨 Minting chef: TestChef123
   ✅ API Response (/api/chef/mint): {...}
   ✅ Chef NFT minted successfully!
      Name: TestChef123
      Token ID: 1
      Tx Hash: 0x...
      Gas Sponsored: true
   ```
5. **Check backend logs:**
   ```
   🎨 Minting chef for 0xABC... with name: TestChef123
   ✅ Session signer added to wallet xxx
   ✅ Sponsored tx sent: 0x...
   ```

6. **Verify on Basescan:**
   - Go to: `https://sepolia.basescan.org/tx/[YOUR_TX_HASH]`
   - Check: Transaction from USER's wallet (not backend)
   - Check: Gas paid by Privy sponsorship contract

### **Test 3: Profile Loading** (2 mins)

After minting:
```
Expected UI Updates:
- Chef Token ID: "Chef #1: TestChef123"
- Energy: "⚡ 300/300"
- Status: Green success message
```

---

## 🐛 **Troubleshooting:**

### **❌ No Token Found**
**Symptoms:** Console shows "Could not find auth token"

**Fix:**
1. Check `PrivyTokenHelper.cs` console output
2. Find property with JWT format (starts with `eyJ`)
3. Uncomment correct line in `GetAuthToken()` method
4. Example:
   ```csharp
   // If console shows: "IdToken: eyJhbGciOi..."
   // Uncomment:
   if (!string.IsNullOrEmpty(user.IdToken))
       return user.IdToken;
   ```

### **❌ 401 Unauthorized**
**Symptoms:** API returns "Invalid authentication token"

**Fix:**
- Verify Privy App ID matches in Unity and backend
- Check backend `.env` has correct `PRIVY_APP_SECRET`
- Ensure token is being sent in Authorization header

**Debug:**
```csharp
// In GameAPIClient.SendRequestAsync, add:
Debug.Log($"Sending request with token: {token.Substring(0, 20)}...");
```

### **❌ Backend Not Responding**
**Symptoms:** Connection timeout

**Fix:**
```bash
# Check backend is running:
curl http://localhost:3000/health

# If not running:
cd backend
node src/server.js

# Check logs:
tail -f server.log
```

### **❌ Minting Times Out**
**Symptoms:** "Transaction sent" but no confirmation

**Fix:**
- Increase wait time in `ChefManager.cs` from 3s to 10s
- Check Base Sepolia RPC is working
- Verify transaction on Basescan manually

---

## 📊 **Success Criteria:**

✅ **Login works** - User authenticated, wallet created  
✅ **Token saved** - Console shows token saved message  
✅ **API connected** - Backend receives authenticated requests  
✅ **Chef minted** - NFT created, gas sponsored by Privy  
✅ **Profile loads** - UI shows chef name, token ID, energy  

---

## 🎯 **Next Modules:**

Once minting works:

1. **Session Management** (3-4 hrs)
   - Start session button
   - Energy validation
   - Session timer

2. **Gameplay Integration** (4-6 hrs)
   - Move recording (batched)
   - Recipe tracking
   - Cooking state management

3. **Leaderboard** (2-3 hrs)
   - Display top players
   - Player rank
   - Season info

4. **Player Profile** (2 hrs)
   - Global stats display
   - Energy management
   - Refill UI

---

## 🚨 **IMPORTANT:**

### **For Token Extraction:**
After first login test, you MUST update `PrivyTokenHelper.cs` with the correct property name for your Privy SDK version. The console will tell you which property contains the token.

### **For Production:**
Update `GameAPIClient` base URL to your deployed backend:
```csharp
private string baseUrl = "https://api.sushibonks.com";
```

---

**Ready to test? Start with Test 1 (Login)!** 🎮

Questions or issues? Check the console output - it will guide you! 🔍

