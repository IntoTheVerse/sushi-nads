# Monad Testnet Setup Instructions

## Changes Required for Monad Testnet

### 1. Backend `.env` File Updates

Update these environment variables in `backend/.env`:

```env
# Set TARGET_NETWORK to monad
TARGET_NETWORK=monad

# Update Monad contract addresses (already deployed)
MONAD_CHEFNFT_ADDRESS=0xF9DC1860df4F3b17725964E2CF9bDE4aA75Ce83a
MONAD_GAMECORE_ADDRESS=0xbBeC5987BBC89e99bD36a675b4c2F097b8eB2624
MONAD_LEADERBOARD_ADDRESS=0x9d6F6bC7AEF75Dc85ca00AC87fC2533fA606Ecf7

# Monad RPC (already correct)
MONAD_RPC_URL=https://testnet-rpc.monad.xyz
```

### 2. Backend Code Changes (✅ Already Done)

The following files have been updated to support dynamic chain IDs:

- ✅ `backend/src/services/blockchain.service.js` - Added chain ID to NETWORKS config
- ✅ `backend/src/services/privy.service.js` - Uses dynamic chain ID from activeNetwork
- ✅ `backend/src/services/sponsorship.service.js` - Uses dynamic chain ID from activeNetwork

### 3. Unity Changes

**✅ NO CODE CHANGES NEEDED** - Unity doesn't need any code updates because:
- Unity only calls the backend REST API (via `GameAPIClient.cs`)
- The backend handles network switching based on `TARGET_NETWORK`
- All blockchain interactions happen server-side
- Unity only uses Privy for:
  - User authentication (email OTP, OAuth)
  - Getting wallet addresses (which are chain-agnostic)
  - **NO direct blockchain transactions** from Unity code

**Important Notes:**
- Privy wallet addresses are the same across all EVM chains
- Unity just passes the wallet address to the backend API
- The backend uses that address regardless of which network it's running on
- No chain ID configuration needed in Unity

### 4. Privy Configuration

**Privy Dashboard Configuration (Optional for Gas Sponsorship):**

Since your backend pays for all transactions in hackathon mode, Privy gas sponsorship configuration is **optional**. However, if you want to enable gas sponsorship for Monad:

1. Go to [Privy Dashboard](https://dashboard.privy.io)
2. Navigate to **Wallets → Gas Sponsorship**
3. Check if Monad Testnet (Chain ID: 10143) is available in the supported chains list
4. If available, enable it for gas sponsorship
5. Ensure your authorization key is configured for Monad transactions

**Important Notes:**
- **Privy may not yet support Monad Testnet** - This is common for newer networks
- If gas sponsorship isn't available for Monad, **the backend will still work perfectly** using direct contract calls where the backend pays gas
- Your current hackathon setup (backend pays gas) doesn't require Privy gas sponsorship
- Privy wallets created on Base Sepolia will work fine - wallet addresses are chain-agnostic
- Unity doesn't need to know which chain Privy wallets are configured for since all blockchain calls go through the backend

**Privy Unity SDK Configuration:**
- ✅ No changes needed in Unity code
- ✅ Privy initialization (`PrivyManager.Initialize`) doesn't require chain configuration
- ✅ Chain configuration is managed at the Privy Dashboard level, not in Unity
- ✅ The same Privy App ID and Client ID work for both Base and Monad

### 5. Testing Checklist

After updating `.env`:

1. ✅ Restart backend server: `npm run dev`
2. ✅ Verify network connection in logs: Should show "Monad Testnet"
3. ✅ Test minting: Call `/api/chef/mint` and verify on Monad explorer
4. ✅ Test session start: Call `/api/session/start` and verify transaction
5. ✅ Test gameplay: Record moves and verify on-chain

### 6. Switching Back to Base Sepolia

To switch back to Base Sepolia:

```env
TARGET_NETWORK=base
```

No other changes needed - the backend will automatically use Base Sepolia contracts and chain ID (84532).

### 7. Chain IDs Reference

- **Base Sepolia**: Chain ID `84532`
- **Monad Testnet**: Chain ID `10143`

Both are configured in `blockchain.service.js` and used automatically based on `TARGET_NETWORK`.
