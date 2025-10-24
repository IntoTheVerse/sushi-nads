# Sushi Bonks Smart Contracts

Smart contracts for the Sushi Bonks cooking game on Base Sepolia testnet.

## 📋 Contracts

### 1. ChefNFT.sol
- ERC721 soulbound NFT (non-transferable)
- One chef per wallet
- Energy management system (refill with USDC)
- Player name storage

### 2. GameCore.sol
- Main game logic contract
- Session management (start/end)
- Move tracking with energy consumption
- Recipe tracking (spawned, delivered, discarded)
- Cooking quality tracking (well-cooked, overcooked)
- Onchain score computation with bonuses/penalties
- Global player statistics

### 3. Leaderboard.sol
- Season-based leaderboard
- Efficient ranking system
- High score tracking per player
- Query functions for top players

## 🚀 Quick Start

### Installation

```bash
cd blockchain
npm install
```

### Configuration

1. Copy `env.example.txt` to `.env`:
```bash
cp env.example.txt .env
```

2. Fill in your `.env` values:
```
PRIVATE_KEY=your_private_key_here
BASE_SEPOLIA_RPC_URL=https://sepolia.base.org
BASESCAN_API_KEY=your_basescan_api_key_here
USDC_ADDRESS=0x036CbD53842c5426634e7929541eC2318f3dCF7e
BACKEND_SIGNER_ADDRESS=your_backend_wallet_address
```

### Compile Contracts

```bash
npm run compile
```

### Run Tests

```bash
npm test
```

### Deploy to Base Sepolia

```bash
npm run deploy:sepolia
```

### Verify Contracts

After deployment, verify on Basescan:

```bash
npx hardhat verify --network baseSepolia <CONTRACT_ADDRESS> <CONSTRUCTOR_ARGS>
```

(See deployment output for exact commands)

## 📊 Contract Architecture

```
┌─────────────┐
│  ChefNFT    │
│  (ERC721)   │
└──────┬──────┘
       │
       │ authorized to consume energy
       ↓
┌─────────────┐      ┌─────────────┐
│  GameCore   │─────→│ Leaderboard │
│             │      │             │
└─────────────┘      └─────────────┘
       │
       └─ Called by backend signer
```

## 🎮 Game Flow

1. **Mint Chef NFT** (free, pay gas only)
   ```solidity
   chefNFT.mintChef("PlayerName")
   ```

2. **Refill Energy** (0.1 USDC = 300 energy)
   ```solidity
   usdc.approve(chefNFTAddress, refillCost)
   chefNFT.refillEnergy()
   ```

3. **Start Game Session** (backend)
   ```solidity
   gameCore.startGameSession(playerAddress)
   ```

4. **Gameplay Actions** (backend)
   ```solidity
   gameCore.recordMove(sessionId)
   gameCore.recordRecipeDelivered(sessionId)
   gameCore.recordWellCooked(sessionId)
   // etc.
   ```

5. **End Session & Compute Score** (backend)
   ```solidity
   gameCore.endGameSession(sessionId, "TIME_UP")
   // Score automatically computed and sent to leaderboard
   ```

## 💯 Scoring Formula

```
score = (recipes_delivered × 100)
      + (well_cooked × 10)
      - (recipes_discarded × 50)
      - (over_cooked × 20)
      - (move_count × 1)
      + time_bonus
      + combo_bonus
      × efficiency_multiplier
```

### Bonuses:
- **Time Bonus**: 2 points per second remaining
- **Combo Bonus**: 
  - 3-4 streak: +50
  - 5-6 streak: +150
  - 7-9 streak: +300
  - 10+ streak: +500
- **Efficiency Multiplier**:
  - 75-89% accuracy: 1.1x
  - 90%+ accuracy: 1.2x

## 📝 Contract Addresses (Base Sepolia)

After deployment, addresses will be saved to `deployments/` directory.

Example:
```json
{
  "contracts": {
    "ChefNFT": "0x...",
    "GameCore": "0x...",
    "Leaderboard": "0x..."
  }
}
```

## 🧪 Testing

The test suite covers:
- ✅ Chef NFT minting and energy system
- ✅ Game session lifecycle
- ✅ All gameplay actions
- ✅ Score computation logic
- ✅ Leaderboard rankings
- ✅ Season management
- ✅ Access control
- ✅ Edge cases

Run with gas reporting:
```bash
REPORT_GAS=true npm test
```

## 🔒 Security Features

- **Soulbound NFTs**: Chefs cannot be transferred
- **Energy checks**: Prevents overuse of game resources
- **Backend authorization**: Only backend can trigger gameplay
- **Non-negative scores**: Score floor at 0
- **Reentrancy guards**: On critical functions
- **Access control**: Owner and backend roles

## 📚 Additional Resources

- [Hardhat Documentation](https://hardhat.org/docs)
- [OpenZeppelin Contracts](https://docs.openzeppelin.com/contracts/)
- [Base Documentation](https://docs.base.org/)
- [Ethers.js v6](https://docs.ethers.org/v6/)

## 🐛 Troubleshooting

### "Insufficient funds" error
Make sure your wallet has Base Sepolia ETH. Get testnet ETH from:
- [Base Sepolia Faucet](https://www.coinbase.com/faucets/base-ethereum-goerli-faucet)

### "Nonce too high" error
Reset your account nonce in MetaMask: Settings → Advanced → Reset Account

### Test failures
Make sure you've compiled first:
```bash
npm run compile
npm test
```

## 📄 License

MIT

