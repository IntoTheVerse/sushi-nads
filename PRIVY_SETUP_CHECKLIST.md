# Privy Dashboard Setup - Step-by-Step Checklist

**Goal:** Configure Privy for Sushi Bonks with embedded wallets and gas sponsorship

---

## 📋 **Step 1: Create Privy Account** (2 mins)

1. Go to **https://dashboard.privy.io**
2. Click **"Sign Up"** or **"Get Started"**
3. Sign up with:
   - Email + password, OR
   - Google account
4. Verify email if prompted
5. ✅ **You're in the dashboard!**

---

## 🎮 **Step 2: Create Your App** (2 mins)

1. Click **"Create App"** or **"New App"**
2. Fill in:
   ```
   App Name: Sushi Bonks
   Description: Cooking game with onchain scoring
   App Type: Game
   ```
3. Click **"Create"**
4. ✅ **App created!**

You'll see:
```
App ID: clxxxxxxxxxxxxxxxxxxxx
```

**📝 Copy this App ID** - you'll need it!

---

## ⚙️ **Step 3: Configure Authentication Methods** (3 mins)

1. Go to **Settings** → **Login Methods** (left sidebar)
2. Enable these methods:
   ```
   ✅ Email (OTP code)
   ✅ Google OAuth (optional but recommended)
   ✅ Discord OAuth (optional)
   ```
3. Configure Email settings:
   ```
   Sender Name: Sushi Bonks
   From Email: noreply@sushibonks.com (or use default)
   ```
4. For OAuth (if enabling):
   - Google: Use Privy's default (no config needed)
   - Discord: Use Privy's default

5. Click **"Save"**
6. ✅ **Auth methods configured!**

---

## 💼 **Step 4: Configure Embedded Wallets** (2 mins)

1. Go to **Settings** → **Wallets** (left sidebar)
2. Embedded Wallet Settings:
   ```
   Wallet Creation: ✅ Automatic (create on sign up)
   Supported Chains: ✅ Ethereum
   Default Chain: Base Sepolia (84532)
   ```
3. Security Settings:
   ```
   Require MFA for: High-risk actions only
   Session Duration: 7 days
   ```
4. Click **"Save"**
5. ✅ **Wallets configured!**

---

## 📱 **Step 5: Create App Client for Unity** (3 mins)

Unity requires an **App Client** (per [Privy docs](https://docs.privy.io/basics/unity/setup#prerequisites))

1. Go to **Settings** → **API Keys & Clients**
2. Scroll to **"App Clients"** section
3. Click **"Create App Client"**
4. Fill in:
   ```
   Name: Unity Client
   Type: Native Mobile App
   Redirect URIs:
     - sushibonks://callback
     - http://localhost:3000
   Platform: iOS, Android, WebGL
   ```
5. Click **"Create"**
6. ✅ **You'll get a Client ID**

**📝 Copy these values:**
```
App ID: clxxxxxxxxxxxxxxxxxxxx
Client ID: client_xxxxxxxxxxxxxxxxxxxx
```

---

## 🔑 **Step 6: Get API Secret (Backend)** (2 mins)

1. Stay in **Settings** → **API Keys & Clients**
2. Under **"Server API Keys"**, click **"Create Server Key"**
3. Fill in:
   ```
   Name: Backend Server
   Environment: Production
   Permissions: Full Access
   ```
4. Click **"Create"**
5. ✅ **Copy the App Secret immediately!**

**📝 Copy this value (shown only once!):**
```
App Secret: secret_xxxxxxxxxxxxxxxxxxxx
```

⚠️ **CRITICAL:** Save this secret now - Privy won't show it again!

---

## 💰 **Step 7: Enable Gas Sponsorship** (5 mins)

This is crucial for FREE gameplay!

1. Go to **Wallets** → **Gas Sponsorship** (left sidebar)
2. Click **"Enable Gas Sponsorship"**
3. Select Network:
   ```
   ✅ Base Sepolia (84532)
   ```
4. Configure Spend Limits:
   ```
   Daily Limit: $50 USD
   Per Transaction: $0.50 USD
   Per User (daily): $5 USD
   ```
5. Click **"Save Limits"**

---

## 🎯 **Step 8: Whitelist Your Contracts** (3 mins)

Still in **Gas Sponsorship** settings:

1. Scroll to **"Sponsored Contracts"** or **"Allowlist"**
2. Click **"Add Contract"**
3. Add each contract:

**Contract 1: ChefNFT**
```
Name: ChefNFT
Address: 0x49889375450342FF44a56891034078Ad982Ef95E
Chain: Base Sepolia (84532)
```

**Contract 2: GameCore**
```
Name: GameCore
Address: 0x9e6C89a53E270d87AE1f50b3fe00988Db2323931
Chain: Base Sepolia (84532)
```

**Contract 3: Leaderboard**
```
Name: Leaderboard
Address: 0xB38343DA4Ba50dDD7D6F6d7E7cE6Dc08253C5dA7
Chain: Base Sepolia (84532)
```

4. Click **"Add"** for each
5. ✅ **All contracts whitelisted!**

---

## 💸 **Step 9: Fund Gas Sponsorship Wallet** (5 mins)

Privy creates a deposit address for gas sponsorship:

1. In **Gas Sponsorship** settings, find **"Deposit Address"**
2. You'll see something like:
   ```
   Deposit Address: 0xABCD...1234
   Network: Base Sepolia
   Current Balance: 0 ETH
   ```

3. **Send Base Sepolia ETH to this address:**
   - Minimum: **0.05 ETH** (for testing)
   - Recommended: **0.1-0.2 ETH** (for hackathon)
   
4. **How to get Base Sepolia ETH:**
   - Option A: [Coinbase Base Faucet](https://www.coinbase.com/faucets/base-ethereum-goerli-faucet)
   - Option B: Bridge from Ethereum Sepolia to Base Sepolia
   - Option C: Ask in Base Discord

5. Wait 1-2 mins for confirmation
6. Refresh page to see balance updated
7. ✅ **Sponsorship wallet funded!**

---

## 🔒 **Step 10: Configure CORS & Security** (2 mins)

1. Go to **Settings** → **Security**
2. Add **Allowed Origins**:
   ```
   http://localhost:3000
   http://localhost:8080
   https://sushibonks.com
   chrome-extension://*
   ```
3. Session Settings:
   ```
   Session Duration: 7 days
   Require re-authentication for: Never (for game flow)
   ```
4. Click **"Save"**
5. ✅ **Security configured!**

---

## ✅ **Verification Checklist**

Before proceeding, verify you have:

- [ ] App ID (starts with `cl...`)
- [ ] Client ID (starts with `client_...`)
- [ ] App Secret (starts with `secret_...`)
- [ ] Gas sponsorship enabled for Base Sepolia
- [ ] All 3 contracts whitelisted
- [ ] Sponsorship wallet funded (>0.05 ETH)
- [ ] Allowed origins configured

---

## 📝 **Save Your Credentials**

Create a temporary note with:

```
PRIVY_APP_ID=clxxxxxxxxxxxxxxxxxxxx
PRIVY_CLIENT_ID=client_xxxxxxxxxxxxxxxxxxxx
PRIVY_APP_SECRET=secret_xxxxxxxxxxxxxxxxxxxx

Gas Sponsorship Deposit Address: 0xABCD...1234
Current Balance: 0.XX ETH
```

---

## 🧪 **Step 11: Test Gas Sponsorship** (Optional)

Once funded, you can test:

1. Go to **Wallets** → **Gas Sponsorship** → **Activity**
2. You should see:
   ```
   Status: Active ✅
   Remaining Budget: $XX.XX
   Transactions Today: 0
   ```

---

## 🎯 **Next: Configure Backend**

Once you have all 3 credentials, run:

```bash
cd backend
./setup-backend-env.sh
```

It will prompt for:
- Privy App ID
- Privy App Secret  
- Privy Client ID
- Firebase Project ID
- Firebase Credentials JSON

Then test:
```bash
npm run dev
```

---

## 🐛 **Troubleshooting**

### Can't find Gas Sponsorship
- Make sure you're on a paid Privy plan (or trial)
- Contact Privy support if not available

### Sponsorship not working
- Verify contracts are on correct network (Base Sepolia = 84532)
- Check contract addresses are exactly correct
- Ensure wallet has funds

### App Client not available
- Privy Unity SDK requires App Client
- If you don't see this option, contact Privy support

---

**Ready?** Let me know when you've completed these steps and have your credentials! 🚀

Or if you need help with any specific step, just ask!


