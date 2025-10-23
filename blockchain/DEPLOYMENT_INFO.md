# Sushi Bonks - Deployment Information

**Network:** Base Sepolia  
**Deployed:** October 23, 2025  
**Deployer:** 0xaAf16AD8a1258A98ed77A5129dc6A8813924Ad3C

---

## 📋 Contract Addresses

| Contract | Address | Basescan |
|----------|---------|----------|
| **ChefNFT** | `0x49889375450342FF44a56891034078Ad982Ef95E` | [View](https://sepolia.basescan.org/address/0x49889375450342FF44a56891034078Ad982Ef95E) |
| **GameCore** | `0x9e6C89a53E270d87AE1f50b3fe00988Db2323931` | [View](https://sepolia.basescan.org/address/0x9e6C89a53E270d87AE1f50b3fe00988Db2323931) |
| **Leaderboard** | `0xB38343DA4Ba50dDD7D6F6d7E7cE6Dc08253C5dA7` | [View](https://sepolia.basescan.org/address/0xB38343DA4Ba50dDD7D6F6d7E7cE6Dc08253C5dA7) |

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
CHEFNFT_ADDRESS=0x49889375450342FF44a56891034078Ad982Ef95E
GAMECORE_ADDRESS=0x9e6C89a53E270d87AE1f50b3fe00988Db2323931
LEADERBOARD_ADDRESS=0xB38343DA4Ba50dDD7D6F6d7E7cE6Dc08253C5dA7

BACKEND_PRIVATE_KEY=<private_key_backend_signer> //already saved on dev wallet 
BACKEND_SIGNER_ADDRESS=0x8cE6f62bFf4ACeaDd4C49116214ba13BF446d527

BASE_SEPOLIA_RPC_URL=https://sepolia.base.org
```

### For Unity Client
```csharp
// Add these to your GameConfig
public const string CHEFNFT_ADDRESS = "0x49889375450342FF44a56891034078Ad982Ef95E";
public const string GAMECORE_ADDRESS = "0x9e6C89a53E270d87AE1f50b3fe00988Db2323931";
public const string LEADERBOARD_ADDRESS = "0xB38343DA4Ba50dDD7D6F6d7E7cE6Dc08253C5dA7";
```

---

## 📊 Contract ABIs

ABIs are available in:
```
blockchain/artifacts/contracts/ChefNFT.sol/ChefNFT.json
blockchain/artifacts/contracts/GameCore.sol/GameCore.json
blockchain/artifacts/contracts/Leaderboard.sol/Leaderboard.json
```

Copy these to your backend:
```bash
mkdir -p backend/src/abis
cp blockchain/artifacts/contracts/ChefNFT.sol/ChefNFT.json backend/src/abis/
cp blockchain/artifacts/contracts/GameCore.sol/GameCore.json backend/src/abis/
cp blockchain/artifacts/contracts/Leaderboard.sol/Leaderboard.json backend/src/abis/
```

---

## 🧪 Testing

### Test Chef NFT Minting
```javascript
const chefNFT = await ethers.getContractAt(
  "ChefNFT",
  "0x49889375450342FF44a56891034078Ad982Ef95E"
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
  "0x9e6C89a53E270d87AE1f50b3fe00988Db2323931"
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
- https://sepolia.basescan.org/address/0x49889375450342FF44a56891034078Ad982Ef95E
- https://sepolia.basescan.org/address/0x9e6C89a53E270d87AE1f50b3fe00988Db2323931
- https://sepolia.basescan.org/address/0xB38343DA4Ba50dDD7D6F6d7E7cE6Dc08253C5dA7

