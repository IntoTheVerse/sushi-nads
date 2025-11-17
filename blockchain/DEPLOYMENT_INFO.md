# Sushi Bonks - Deployment Information

**Network:** Base Sepolia  
**Deployed:** October 23, 2025  
**Deployer:** 0xaAf16AD8a1258A98ed77A5129dc6A8813924Ad3C

---

## 📋 Contract Addresses

| Contract | Address | Basescan |
|----------|---------|----------|
| **ChefNFT** | `0x4ef5835f754fb83364ec8bB7CC5e3976087a46ad` | [View](https://sepolia.basescan.org/address/0x4ef5835f754fb83364ec8bB7CC5e3976087a46ad) |
| **GameCore** | `0x04E9f92c8e4Ce69c3f73eac9bF18F3c38d905012` | [View](https://sepolia.basescan.org/address/0x04E9f92c8e4Ce69c3f73eac9bF18F3c38d905012) |
| **Leaderboard** | `0xD13bb48824451EAEDdB8a4cb000066F65aA7eB44` | [View](https://sepolia.basescan.org/address/0xD13bb48824451EAEDdB8a4cb000066F65aA7eB44) |

### Monad Testnet (Base-Compatible Deployment)

**Network:** Monad Testnet  
**Deployed:** November 17, 2025  
**Contracts verified on MonVision (Sourcify)**  

| Contract | Address | Explorer |
|----------|---------|----------|
| **ChefNFT** | `0x8ab8054b8b94A8a42719A1D62e08107115660B6A` | [MonVision](https://testnet.monadexplorer.com/address/0x8ab8054b8b94A8a42719A1D62e08107115660B6A) |
| **GameCore** | `0xe70b0619BfC8491f87920bb161554048C1Bac9b5` | [MonVision](https://testnet.monadexplorer.com/address/0xe70b0619BfC8491f87920bb161554048C1Bac9b5) |
| **Leaderboard** | `0xFCB5CD51AE29379790658b7A9804Bf4d1c01Ab9D` | [MonVision](https://testnet.monadexplorer.com/address/0xFCB5CD51AE29379790658b7A9804Bf4d1c01Ab9D) |

---

## 🔐 Configuration

### Backend Signer
```
Address: 0x8cE6f62bFf4ACeaDd4C49116214ba13BF446d527
```
⚠️ **IMPORTANT:** Save this private key securely! You'll need it for the backend server.

### External Contracts
```
USDC (Base Sepolia): 0x036CbD53842c5426634e7929541eC2318f3dCF7e
```

### Game Settings
```
Energy Refill Cost: 0.1 USDC
Energy Refill Amount: 300
Move Cost: 1 energy
Game Duration: 1000 seconds
```

---

## ✅ Verification Status

All contracts deployed and connected:
- ✅ ChefNFT knows about GameCore
- ✅ GameCore knows about ChefNFT
- ✅ GameCore knows about Leaderboard
- ✅ Leaderboard knows about GameCore
- ✅ Backend signer authorized in GameCore

---

## 🔗 Contract Interactions

### For Backend Server (.env)
```bash
CHEFNFT_ADDRESS=0x4ef5835f754fb83364ec8bB7CC5e3976087a46ad
GAMECORE_ADDRESS=0x04E9f92c8e4Ce69c3f73eac9bF18F3c38d905012
LEADERBOARD_ADDRESS=0xD13bb48824451EAEDdB8a4cb000066F65aA7eB44

BACKEND_PRIVATE_KEY=<private_key_backend_signer> //already saved on dev wallet 
BACKEND_SIGNER_ADDRESS=0x8cE6f62bFf4ACeaDd4C49116214ba13BF446d527

BASE_SEPOLIA_RPC_URL=https://sepolia.base.org
```

### For Unity Client
```csharp
// Add these to your GameConfig
public const string CHEFNFT_ADDRESS = "0x4ef5835f754fb83364ec8bB7CC5e3976087a46ad";
public const string GAMECORE_ADDRESS = "0x04E9f92c8e4Ce69c3f73eac9bF18F3c38d905012";
public const string LEADERBOARD_ADDRESS = "0xD13bb48824451EAEDdB8a4cb000066F65aA7eB44";
```

---

## 📊 Contract ABIs

ABIs are available in:
```
blockchain/artifacts/contracts/ChefNFT.sol/ChefNFT.json
blockchain/artifacts/contracts/GameCore.sol/GameCore.json
blockchain/artifacts/contracts/Leaderboard.sol/Leaderboard.json
```

Copy these to your backend (keep Base and Monad ABIs isolated):
```bash
mkdir -p backend/src/abis/base backend/src/abis/monad

# Base Sepolia ABIs
cp blockchain/artifacts/contracts/ChefNFT.sol/ChefNFT.json backend/src/abis/base/ChefNFT.json
cp blockchain/artifacts/contracts/GameCore.sol/GameCore.json backend/src/abis/base/GameCore.json
cp blockchain/artifacts/contracts/Leaderboard.sol/Leaderboard.json backend/src/abis/base/Leaderboard.json

# Monad Testnet ABIs (after compiling with Monad settings)
cp blockchain/artifacts/contracts/ChefNFT.sol/ChefNFT.json backend/src/abis/monad/ChefNFT.json
cp blockchain/artifacts/contracts/GameCore.sol/GameCore.json backend/src/abis/monad/GameCore.json
cp blockchain/artifacts/contracts/Leaderboard.sol/Leaderboard.json backend/src/abis/monad/Leaderboard.json
```

---

## 🧪 Testing

### Test Chef NFT Minting
```javascript
const chefNFT = await ethers.getContractAt(
  "ChefNFT",
  "0x4ef5835f754fb83364ec8bB7CC5e3976087a46ad"
);

// Mint a chef
await chefNFT.mintChef("TestChef");

// Check balance
const hasChef = await chefNFT.playerHasChef(yourAddress);
console.log("Has chef:", hasChef);
```

### Test Session Start
```javascript
const gameCore = await ethers.getContractAt(
  "GameCore",
  "0x04E9f92c8e4Ce69c3f73eac9bF18F3c38d905012"
);

// Only backend signer can call this
const tx = await gameCore.startGameSession(playerAddress);
await tx.wait();
```

---

## 📝 Next Steps

1. **Set up backend server**
   - Use addresses above in backend `.env`
   - Copy ABIs to `backend/src/abis/`
   - Initialize Privy SDK
   - Start server

2. **Integrate with Unity**
   - Add contract addresses to Unity config
   - Implement Privy authentication
   - Connect to backend API

3. **Test the flow**
   - Login with Privy
   - Mint chef NFT
   - Refill energy
   - Start game session
   - Play game

4. **Privy Gas Sponsorship**
   - Add deployed addresses to Privy dashboard
   - Enable gas sponsorship for these contracts
   - Fund sponsorship wallet

---

## 🔒 Security Notes

- ✅ Backend signer key stored securely (will be in backend .env)
- ✅ Deployer key stored securely
- ✅ Contracts are on testnet (Base Sepolia)
- ⚠️ Before mainnet: audit contracts
- ⚠️ Before mainnet: rotate all keys
- ⚠️ Backend signer should use GCP Secret Manager in production

---

**Deployment Successful!** 🎉

View contracts on Basescan:
- https://sepolia.basescan.org/address/0x4ef5835f754fb83364ec8bB7CC5e3976087a46ad
- https://sepolia.basescan.org/address/0x04E9f92c8e4Ce69c3f73eac9bF18F3c38d905012
- https://sepolia.basescan.org/address/0xD13bb48824451EAEDdB8a4cb000066F65aA7eB44

