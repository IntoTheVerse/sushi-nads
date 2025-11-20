# Sushi Bonks - Deployment Information

**Network:** Base Sepolia  
**Deployed:** November 19, 2025  
**Deployer:** 0xaAf16AD8a1258A98ed77A5129dc6A8813924Ad3C  
**✅ All contracts verified on Basescan**

---

## 📋 Contract Addresses

| Contract | Address | Basescan |
|----------|---------|----------|
| **ChefNFT** | `0xC3dDBBA92CE071D7B872D31f3870bC4672c3D05E` | [View](https://sepolia.basescan.org/address/0xC3dDBBA92CE071D7B872D31f3870bC4672c3D05E) |
| **GameCore** | `0x17C43c95226250c116E904dea0a7507a74ADa99a` | [View](https://sepolia.basescan.org/address/0x17C43c95226250c116E904dea0a7507a74ADa99a) |
| **Leaderboard** | `0x41ba1B1bd3A81955D476Cb909DD4eA056caaE45f` | [View](https://sepolia.basescan.org/address/0x41ba1B1bd3A81955D476Cb909DD4eA056caaE45f) |

### Monad Testnet (Base-Compatible Deployment)

**Network:** Monad Testnet  
**Deployed:** November 19, 2025  
**✅ All contracts verified on MonVision (Sourcify)**  

| Contract | Address | Explorer |
|----------|---------|----------|
| **ChefNFT** | `0xF9DC1860df4F3b17725964E2CF9bDE4aA75Ce83a` | [MonVision](https://testnet.monadexplorer.com/address/0xF9DC1860df4F3b17725964E2CF9bDE4aA75Ce83a) |
| **GameCore** | `0xbBeC5987BBC89e99bD36a675b4c2F097b8eB2624` | [MonVision](https://testnet.monadexplorer.com/address/0xbBeC5987BBC89e99bD36a675b4c2F097b8eB2624) |
| **Leaderboard** | `0x9d6F6bC7AEF75Dc85ca00AC87fC2533fA606Ecf7` | [MonVision](https://testnet.monadexplorer.com/address/0x9d6F6bC7AEF75Dc85ca00AC87fC2533fA606Ecf7) |

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
CHEFNFT_ADDRESS=0xC3dDBBA92CE071D7B872D31f3870bC4672c3D05E
GAMECORE_ADDRESS=0x17C43c95226250c116E904dea0a7507a74ADa99a
LEADERBOARD_ADDRESS=0x41ba1B1bd3A81955D476Cb909DD4eA056caaE45f

BACKEND_PRIVATE_KEY=<private_key_backend_signer> //already saved on dev wallet 
BACKEND_SIGNER_ADDRESS=0x8cE6f62bFf4ACeaDd4C49116214ba13BF446d527

BASE_SEPOLIA_RPC_URL=https://sepolia.base.org
```

### For Unity Client
```csharp
// Add these to your GameConfig
public const string CHEFNFT_ADDRESS = "0xC3dDBBA92CE071D7B872D31f3870bC4672c3D05E";
public const string GAMECORE_ADDRESS = "0x17C43c95226250c116E904dea0a7507a74ADa99a";
public const string LEADERBOARD_ADDRESS = "0x41ba1B1bd3A81955D476Cb909DD4eA056caaE45f";
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
  "0xC3dDBBA92CE071D7B872D31f3870bC4672c3D05E"
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
  "0x17C43c95226250c116E904dea0a7507a74ADa99a"
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
- https://sepolia.basescan.org/address/0xC3dDBBA92CE071D7B872D31f3870bC4672c3D05E
- https://sepolia.basescan.org/address/0x17C43c95226250c116E904dea0a7507a74ADa99a
- https://sepolia.basescan.org/address/0x41ba1B1bd3A81955D476Cb909DD4eA056caaE45f

