# 🍣 Sushi Base

<p align="center">
  <img width="400" alt="Sushi Bonks Logo" src="https://github.com/user-attachments/assets/8ea45e28-644d-4593-8938-0efdc9121a29" />
</p>

<h3 align="center">🎮 Overcooked Meets Blockchain 🔗</h3>

<p align="center">
  <strong>The first fully on-chain cooking game on Base sepolia network</strong><br/>
  Where every recipe, every move, and every burnt ingredient lives forever on the blockchain.
</p>

---

## 🎬 Demo Video

> 📹 **[Watch Demo Video →](https://youtu.be/WEU-MKhzGz4)** *(Coming Soon)*

---

## 🎯 What is Sushi Base?

**Sushi Base** is a fast-paced, blockchain-native cooking game inspired by Overcooked. Players cook sushi recipes under time pressure, but with a twist: **every action is recorded on-chain**.

Unlike traditional games where your progress lives on centralized servers, in Sushi Base:
- 🎨 Your **Chef is an NFT** (soulbound to your wallet)
- ⚡ **Energy system** powered by smart contracts
- 🎮 **Every gameplay event** recorded on Base blockchain
- 🏆 **Permanent leaderboards** secured by blockchain
- 💎 **True ownership** of your gaming identity

**Play it live:** *(Coming Soon)*

---

## ✨ Key Features

### 🍱 **Fully On-Chain Gameplay**
Every game action is a blockchain event:
- Recipe deliveries
- Perfectly cooked ingredients
- Burnt ingredients
- Recipes discarded
- Player movements

### 🎨 **Chef NFT System**
- Mint your unique Chef NFT to start playing
- Soulbound to your wallet
- Tracks lifetime stats on-chain
- Energy system for game sessions

### ⚡ **Energy Mechanics**
- Each Chef starts with 300 energy
- Sessions cost 5 energy to start
- Refill with 0.1 USDC (or free in hackathon mode!)
- All energy changes recorded on-chain

### 🎮 **Session-Based Gameplay**
- 10-minute timed cooking sessions
- Start session → Play → End session (all on-chain)
- Score computed on blockchain using proven formula
- No server-side scoring - trustless and transparent

### 🏆 **On-Chain Leaderboard**
- Rankings stored permanently on Base
- Seasonal competitions
- Global and personal stats
- Verifiable on Basescan

### 🔐 **Privy Authentication**
- Gasless onboarding via embedded wallets
- Social login support
- Seamless Web2 to Web3 experience

---

## 🎮 How to Play

### 1️⃣ **Mint Your Chef**
Create your unique Chef NFT - your on-chain gaming identity.

```
Login → Enter Chef Name → Mint (Gas Sponsored!) → Start Playing
```

### 2️⃣ **Refill Energy**
Get energy to play game sessions.

```
Cost: 0.1 USDC → +300 Energy
(Free refills in hackathon mode!)
```

### 3️⃣ **Start a Session**
Begin your 10-minute cooking challenge.

```
Cost: 5 Energy per session
All events recorded on-chain
```

### 4️⃣ **Cook & Deliver**
Make sushi recipes under time pressure!

- 🔥 Cook ingredients perfectly (+10 points each)
- 🍱 Deliver correct recipes (+100 points each)
- 💀 Avoid burning food (-20 points penalty)
- 🗑️ Don't waste plates (-50 points penalty)
- ⏱️ Finish fast for time bonus

### 5️⃣ **View Your Score**
Session ends → Score computed on-chain → View on Basescan!

```
Final Score = (Deliveries × 100) + (Cooked × 10) + Time Bonus + Combo
             - (Burned × 20) - (Discarded × 50) - (Moves × 1)
```

---

## 🧮 Scoring System

### **Positive Points**
| Action | Points | Details |
|--------|--------|---------|
| Recipe Delivered | **+100** | Successfully deliver correct recipe |
| Well-Cooked Ingredient | **+10** | Cook to perfect "Fried" state |
| Time Bonus | **+2/sec** | Points for time remaining |
| Combo Streak | **+50 to +500** | Consecutive perfect deliveries |

### **Negative Points**
| Action | Points | Details |
|--------|--------|---------|
| Recipe Discarded | **-50** | Throw plate in trash |
| Overcooked Ingredient | **-20** | Burn ingredient on stove |
| Movement | **-1** | Each grid tile movement |


### Overview of session 6 gameplay
```
📊 Fetching Session 6 data from blockchain...

═══════════════════════════════════════════════
           SESSION 6 DETAILS           
═══════════════════════════════════════════════

🎮 Session Info:
   Session ID: 6
   Player: 0x1C7e0309b37e622431f18c6E4A8137beDac93564
   Active: ❌ No
   Score Finalized: ✅ Yes

⏱️  Timing:
   Start Time: 10/25/2025, 2:43:34 AM
   End Time: 10/25/2025, 2:54:14 AM
   Duration: 640 seconds (10.7 mins)

📈 Gameplay Stats:
   Moves: 143
   Recipes Spawned: 0
   Recipes Delivered: 1 ✅
   Recipes Discarded: 0 🗑️
   Well Cooked: 8 🔥
   Over Cooked: 2 💀

🎯 Combo & Score:
   Current Streak: 1
   Max Combo: 1
   Final Score: 860

📊 Performance:
   Delivery Accuracy: 100.0%
   Efficiency Bonus: ×1.2 (90%+ accuracy) 🌟
   Combo Bonus: None

═══════════════════════════════════════════════

📋 JSON Export:
{
  "sessionId": 6,
  "player": "0x1C7e0309b37e622431f18c6E4A8137beDac93564",
  "isActive": false,
  "startTime": 1761340414,
  "endTime": 1761341054,
  "duration": 640,
  "moveCount": 143,
  "recipesSpawned": 0,
  "recipesDelivered": 1,
  "recipesDiscarded": 0,
  "wellCookedCount": 8,
  "overCookedCount": 2,
  "consecutivePerfectDeliveries": 1,
  "maxComboAchieved": 1,
  "finalScore": 860,
  "scoreFinalized": true,
  "efficiency": 100,
  "comboBonus": 0
}

```

### **Multipliers**
- **90%+ Efficiency:** Score × 1.2
- **75%+ Efficiency:** Score × 1.1
- **Combo Bonuses:**
  - 10+ streak: +500
  - 7-9 streak: +300
  - 5-6 streak: +150
  - 3-4 streak: +50

---

## 🏗️ Technical Architecture

### **Smart Contracts (Base Sepolia)**

#### **ChefNFT.sol**
- ERC-721 NFT for player identity
- Energy management system
- Soulbound tokens (non-transferable)
- Persistent player stats

#### **GameCore.sol**
- Session management (start/end)
- Gameplay event recording
- On-chain score computation
- Combo and streak tracking

#### **Leaderboard.sol**
- Seasonal ranking system
- Global and player stats
- Permanent on-chain storage

### **Tech Stack**

**Blockchain:**
- ⛓️ Base Sepolia Testnet
- 🔐 Smart Contracts: Solidity ^0.8.20
- 🔗 Ethers.js v6

**Game Engine:**
- 🎮 Unity 2022.3 LTS
- 🔌 Solana Unity SDK (adapted for EVM)
- 📱 iOS/Android Support

**Backend:**
- 🖥️ Node.js + Express
- 🔑 Privy Authentication
- ⛽ Gas Sponsorship (Hackathon Mode)
- 📊 Transaction Queue System

**Authentication:**
- 🔐 Privy Embedded Wallets
- 🌐 Social Login Support
- ⚡ Gasless Onboarding

---

## 🎯 Fully On-Chain Systems

### **1. Chef NFT Minting**
```solidity
function mintChef(address player, string memory name) external
```
- Creates soulbound Chef NFT
- Initializes energy (300)
- Records creation on-chain
- Gas sponsored via Privy

### **2. Session Management**
```solidity
function startGameSession(address player) external returns (uint256 sessionId)
function endGameSession(uint256 sessionId, string memory reason) external
```
- Start/end tracked on-chain
- Energy consumed on start
- Score computed on end
- All verifiable on Basescan

### **3. Gameplay Event Recording**
```solidity
function recordRecipeDelivered(uint256 sessionId) external
function recordWellCooked(uint256 sessionId) external
function recordOverCooked(uint256 sessionId) external
function recordRecipeDiscarded(uint256 sessionId) external
function recordMove(uint256 sessionId) external
```
- Every action becomes a transaction
- Sequential processing via queue
- Prevents nonce conflicts
- Transparent gameplay

### **4. Score Computation**
```solidity
function _computeAndFinalizeScore(uint256 sessionId) internal
```
- Runs entirely on-chain
- Uses session event data
- Applies bonuses/penalties
- Immutable and verifiable

### **5. Leaderboard Updates**
```solidity
function updateScore(address player, uint256 score, uint256 sessionId) external
```
- Automatic ranking updates
- Seasonal competitions
- Global stats tracking
- Permanent storage

---

## 🚀 Getting Started

### **Prerequisites**
- Unity 2022.3 LTS or later
- Node.js 18+
- Git

### **Installation**

1. **Clone the repository**
```bash
git clone https://github.com/yourusername/sushi-bonks.git
cd sushi-bonks
```

2. **Install backend dependencies**
```bash
cd backend
npm install
cp .env.example .env
# Edit .env with your keys
npm start
```

3. **Open Unity project**
```bash
# Open the project folder in Unity Hub
# Select Unity 2022.3 LTS
```

4. **Configure Unity**
- Open GameScene
- Ensure GameplayEventTracker exists in hierarchy
- Set GameAPIClient baseUrl to `http://localhost:3000`

5. **Play!**
- Press Play in Unity Editor
- Login with Privy
- Mint your Chef
- Start cooking!

---

## 📊 Game Flow Diagram

```
┌─────────────────────────────────────────────────────────────┐
│                     PLAYER ONBOARDING                        │
├─────────────────────────────────────────────────────────────┤
│  Login (Privy) → Mint Chef NFT → Refill Energy → Ready!     │
└─────────────────────────────────────────────────────────────┘
                              ↓
┌─────────────────────────────────────────────────────────────┐
│                      GAME SESSION                            │
├─────────────────────────────────────────────────────────────┤
│  Start Session (-5 Energy) → 10 Min Timer Begins             │
│  ├─ Cook Ingredients → Record on-chain                       │
│  ├─ Deliver Recipes → Record on-chain                        │
│  ├─ Move Around → Record on-chain (batched)                  │
│  └─ Session Ends → Compute Score on-chain                    │
└─────────────────────────────────────────────────────────────┘
                              ↓
┌─────────────────────────────────────────────────────────────┐
│                    POST-GAME RESULTS                         │
├─────────────────────────────────────────────────────────────┤
│  Score Popup → Basescan Link → Leaderboard Update           │
└─────────────────────────────────────────────────────────────┘
```

---

## 🎨 What Makes It Special?

### **True Blockchain Gaming**
Most "blockchain games" only use blockchain for tokens. Sushi Base puts **gameplay itself on-chain**:

❌ **Typical Blockchain Game:**
- Game runs on server
- Blockchain only for rewards
- Score can be manipulated
- No transparency

✅ **Sushi Base:**
- Every action on-chain
- Score computed in smart contract
- Fully transparent
- Trustless verification

### **No Custody, No Trust**
- ✅ Your Chef is your key
- ✅ No server holds your data
- ✅ Scores are mathematically proven
- ✅ Leaderboard can't be hacked

### **Gas Optimization**
- 🎯 Move batching (10 moves per tx)
- 🎯 Transaction queue prevents nonce conflicts
- 🎯 Gas sponsorship for onboarding
- 🎯 Efficient smart contract design

---

## 📱 Contract Addresses (Base Sepolia)

| Contract | Address | Explorer |
|----------|---------|----------|
| **ChefNFT** | `0x4ef5835f754fb83364ec8bB7CC5e3976087a46ad` | [View →](https://sepolia.basescan.org/address/0x4ef5835f754fb83364ec8bB7CC5e3976087a46ad) |
| **GameCore** | `0x04E9f92c8e4Ce69c3f73eac9bF18F3c38d905012` | [View →](https://sepolia.basescan.org/address/0x04E9f92c8e4Ce69c3f73eac9bF18F3c38d905012) |
| **Leaderboard** | `0xD13bb48824451EAEDdB8a4cb000066F65aA7eB44` | [View →](https://sepolia.basescan.org/address/0xD13bb48824451EAEDdB8a4cb000066F65aA7eB44) |

### 📱 Contract Addresses (Monad Testnet)

All Monad deployments are verified via Sourcify and visible on MonVision.

| Contract | Address | Explorer |
|----------|---------|----------|
| **ChefNFT** | `0x8ab8054b8b94A8a42719A1D62e08107115660B6A` | [MonVision](https://testnet.monadexplorer.com/address/0x8ab8054b8b94A8a42719A1D62e08107115660B6A) |
| **GameCore** | `0xe70b0619BfC8491f87920bb161554048C1Bac9b5` | [MonVision](https://testnet.monadexplorer.com/address/0xe70b0619BfC8491f87920bb161554048C1Bac9b5) |
| **Leaderboard** | `0xFCB5CD51AE29379790658b7A9804Bf4d1c01Ab9D` | [MonVision](https://testnet.monadexplorer.com/address/0xFCB5CD51AE29379790658b7A9804Bf4d1c01Ab9D) |

---

## 🛠️ Development

### **Project Structure**
```
sushi-bonks/
├── Assets/              # Unity game assets
│   ├── Scenes/         # Game scenes
│   ├── Scripts/        # C# game logic
│   └── Prefabs/        # UI components
├── backend/            # Node.js backend
│   ├── src/
│   │   ├── routes/     # API endpoints
│   │   ├── services/   # Blockchain services
│   │   └── middleware/ # Auth & validation
│   └── scripts/        # Utility scripts
├── blockchain/         # Smart contracts
│   ├── contracts/      # Solidity files
│   ├── scripts/        # Deployment scripts
│   └── test/           # Contract tests
└── README.md           # You are here!
```

### **Key Technologies**

**Unity Scripts:**
- `SessionManager.cs` - Session lifecycle
- `GameplayEventTracker.cs` - Event recording
- `ChefManager.cs` - NFT & energy management
- `DeliveryManager.cs` - Recipe validation
- `KitchenGameManager.cs` - Game state & timer

**Backend Services:**
- `blockchain.service.js` - Contract interactions
- `transactionQueue.service.js` - Nonce management
- `privy.service.js` - Authentication & gas sponsorship

**Smart Contracts:**
- `ChefNFT.sol` - Player identity & energy
- `GameCore.sol` - Game logic & scoring
- `Leaderboard.sol` - Rankings & stats

---

## 🎯 Roadmap

### **Phase 1: Hackathon MVP** ✅
- [x] Chef NFT minting
- [x] Energy system
- [x] Session management
- [x] On-chain event recording
- [x] Score computation
- [x] Leaderboard integration
- [x] Privy authentication
- [x] Gas sponsorship

### **Phase 2: Mainnet Launch** 🚀
- [ ] Deploy to Base Mainnet
- [ ] Token rewards ($SUSHI)
- [ ] Multiplayer sessions
- [ ] Chef NFT marketplace
- [ ] Advanced recipes
- [ ] Tournament system

### **Phase 3: Expansion** 🌟
- [ ] Mobile app (iOS/Android)
- [ ] Cross-chain support
- [ ] DAO governance
- [ ] Community recipes
- [ ] Seasonal events
- [ ] NFT customization

---

## 💰 Business Model & GTM Strategy

### **Revenue Model**

```
┌─────────────────────────────────────────────────────────────────┐
│                      REVENUE STREAMS                             │
├─────────────────────────────────────────────────────────────────┤
│                                                                  │
│  PRIMARY: Energy Refills                                         │
│  ├─ 0.1 USDC → +300 Energy (1 session = 5 energy)              │
│  ├─ $10 → 100 refills → 6,000 sessions                         │
│  └─ Paid on Base (low fees, instant)                            │
│                                                                  │
│  FUTURE: Microtransactions & Upgrades                           │
│  ├─ 🎁 Chef Skin Packs (NFT cosmetics)                         │
│  ├─ ⚡ Power-up Boosters (2x speed, auto-cook)                 │
│  ├─ 🏆 Season Battle Passes                                     │
│  ├─ 🎯 Recipe Expansion Packs                                   │
│  └─ 💎 Premium Chef Upgrades                                    │
│                                                                  │
└─────────────────────────────────────────────────────────────────┘
```

### **Go-To-Market Strategy**

```
┌─────────────────────────────────────────────────────────────────┐
│                    COMMUNITY-DRIVEN GROWTH                       │
├─────────────────────────────────────────────────────────────────┤
│                                                                  │
│  PHASE 1: NFT Community Factions 🎭                             │
│  ┌──────────────────────────────────────────────────────┐      │
│  │  Ape Faction       vs  Punks Faction  vs  Degen Faction     │
│  │  (BAYC Chefs)         (CryptoPunk Chefs)  (Milady Chefs)    │
│  └──────────────────────────────────────────────────────┘      │
│  │                                                               │
│  ├─ Each NFT community has unique Chef skins                    │
│  ├─ Faction-based leaderboards & rivalries                      │
│  ├─ Community tournaments with memecoin rewards                 │
│  └─ Holders get free Chef mints from their faction              │
│                                                                  │
│  PHASE 2: Memecoin Rewards 🪙                                   │
│  ┌──────────────────────────────────────────────────────┐      │
│  │  Top Players Earn:                                            │
│  │  ├─ $DEGEN rewards for high scores                           │
│  │  ├─ $BONK airdrops for streaks                               │
│  │  ├─ Partner memecoin prizes                                  │
│  │  └─ Exclusive NFT rewards                                    │
│  └──────────────────────────────────────────────────────┘      │
│                                                                  │
│  PHASE 3: Competitive Seasons 🏆                                │
│  ├─ Monthly tournaments (entry fee in USDC)                     │
│  ├─ Prize pools in ETH + memecoins                              │
│  ├─ Season-exclusive power-ups & skins                          │
│  └─ Faction wars with community prizes                          │
│                                                                  │
└─────────────────────────────────────────────────────────────────┘
```

### **Economics Breakdown**

```
┌─────────────────────────────────────────────────────────────────┐
│                    PLAYER ECONOMICS                              │
├─────────────────────────────────────────────────────────────────┤
│                                                                  │
│  Energy System:                                                  │
│  ├─ Free Chef Mint (300 starting energy)                        │
│  ├─ 5 energy per session (10 min gameplay)                      │
│  ├─ 0.1 USDC refill = 300 energy = 60 sessions                 │
│  └─ Cost per session: ~$0.0017 USD                              │
│                                                                  │
│  Value Proposition:                                              │
│  ├─ $10 investment = 100 refills = 6,000 sessions              │
│  ├─ 6,000 sessions × 10 min = 1,000 hours of gameplay          │
│  └─ Cost: $0.01 per hour (cheaper than any F2P game!)          │
│                                                                  │
│  Monetization Funnel:                                            │
│  ┌────────────────────────────────────────────────────┐         │
│  │  100 Players                                                  │
│  │    ├─ 80 Free Players (use starting energy)                  │
│  │    ├─ 15 Casual Refills ($1-10/month)                        │
│  │    └─ 5 Power Players ($50+/month with power-ups)            │
│  └────────────────────────────────────────────────────┘         │
│                                                                  │
└─────────────────────────────────────────────────────────────────┘
```

### **Growth Loop**

```
                  New Player Onboarding
                         ↓
              ┌─────────────────────┐
              │  Free Chef Mint     │
              │  (300 Energy)       │
              └─────────────────────┘
                         ↓
              ┌─────────────────────┐
              │  Play 60 Sessions   │
              │  (Learn the game)   │
              └─────────────────────┘
                         ↓
                    CONVERSION
                         ↓
              ┌─────────────────────┐
              │  First Refill       │
              │  ($0.10 USDC)       │
              └─────────────────────┘
                         ↓
              ┌─────────────────────┐
              │  Join Faction       │
              │  (NFT Community)    │
              └─────────────────────┘
                         ↓
              ┌─────────────────────┐
              │  Compete in         │
              │  Tournaments        │
              └─────────────────────┘
                         ↓
              ┌─────────────────────┐
              │  Win Memecoin       │
              │  Rewards            │
              └─────────────────────┘
                         ↓
              ┌─────────────────────┐
              │  Invite Friends     │
              │  to Faction         │
              └─────────────────────┘
                         ↓
                  (Loop repeats)
```

### **Competitive Edge**

**Why Players Will Pay:**
- ✅ **Fair Economics:** $0.01/hour cheaper than any mobile game
- ✅ **Community:** NFT faction rivalries create engagement
- ✅ **Rewards:** Earn memecoins while playing
- ✅ **Ownership:** Your Chef & progress on-chain forever
- ✅ **Low Friction:** Base L2 = minimal gas fees

**Why Communities Will Participate:**
- 🎭 **Faction Pride:** Represent your NFT community
- 🏆 **Community Tournaments:** Win prizes for your faction
- 📈 **Floor Price Impact:** Active utility drives NFT demand
- 🤝 **Partnerships:** Memecoin integrations bring liquidity
- 🎮 **Engagement:** Keep holders active in bear markets

---

## 🤝 Contributing

We welcome contributions! Whether it's:
- 🐛 Bug reports
- 💡 Feature suggestions
- 🔧 Code improvements
- 📚 Documentation

Check out [UI_SPECIFICATIONS.md](./UI_SPECIFICATIONS.md) for design guidelines.

---

## 📄 License

This project is licensed under the MIT License.

---

## 🙏 Acknowledgments

- **Base** - For the L2 infrastructure
- **Privy** - For seamless authentication
- **Unity** - For the game engine
- **Overcooked** - For the inspiration
- **Code Monkey** - For Unity tutorials

---

## 📞 Contact & Links

- **Website:** [IntoTheVerse](https://intotheverse.xyz/)
- **Twitter:** [Sushi Bonks](https://x.com/sushibonks_)
- **Discord:** *(Coming Soon)*
- **GitHub:** [github.com/lopeselio/sushi-base](github.com/lopeselio/sushi-base)

---

<p align="center">
  <strong>Built with ❤️ for the Base ecosystem</strong><br/>
  🍣 Cook. Compete. Conquer On-Chain. 🔗
</p>

---

## 🔥 Why Sushi Bonks?

> "Traditional games lock your progress on servers you don't control. Sushi Bonks puts your gaming identity, achievements, and history permanently on the blockchain. Your Chef, your scores, your legacy - all provably yours, forever."

**This isn't just a game with crypto tacked on. This is blockchain gaming done right.**

Start cooking! 🍱
