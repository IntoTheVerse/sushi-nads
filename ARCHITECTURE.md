# Sushi Bonks - System Architecture

## 🏗️ Overview

Sushi Bonks uses a **hybrid architecture** optimizing Privy's embedded wallet capabilities with a lightweight backend for gas sponsorship and transaction batching.

---

## 📊 Architecture Diagram

```
┌─────────────────────────────────────────────────────────────┐
│                     UNITY CLIENT                            │
│  ┌────────────────────────────────────────────────────┐    │
│  │  Privy Unity SDK                                   │    │
│  │  - Email/OAuth Authentication                      │    │
│  │  - Embedded Wallet Creation                        │    │
│  │  - Wallet Address Display                          │    │
│  │  - Sign Messages (EIP-191/712) for special cases  │    │
│  └────────────────────────────────────────────────────┘    │
│                          │                                  │
│  ┌────────────────────────────────────────────────────┐    │
│  │  Game Logic (C#)                                   │    │
│  │  - Player movement, cooking, delivery              │    │
│  │  - Local game state management                     │    │
│  │  - UI rendering                                    │    │
│  └────────────────────────────────────────────────────┘    │
│                          │                                  │
│                          │ HTTP REST API                    │
└──────────────────────────┼──────────────────────────────────┘
                           │
                           ↓
┌─────────────────────────────────────────────────────────────┐
│         NODE.JS BACKEND (PM2 + Nginx / Dedicated Server)   │
│  ┌────────────────────────────────────────────────────┐    │
│  │  Express API Server                                │    │
│  │  - RESTful endpoints                               │    │
│  │  - Privy auth token verification                   │    │
│  │  - Request validation                              │    │
│  └────────────────────────────────────────────────────┘    │
│                          │                                  │
│  ┌────────────────────────────────────────────────────┐    │
│  │  Privy Server SDK (@privy-io/server-auth)         │    │
│  │  - Verify authentication tokens                    │    │
│  │  - Gas sponsorship (sponsor transactions)          │    │
│  │  - User wallet management                          │    │
│  └────────────────────────────────────────────────────┘    │
│                          │                                  │
│  ┌────────────────────────────────────────────────────┐    │
│  │  Blockchain Service Layer (ethers.js)             │    │
│  │  - Backend signer wallet                           │    │
│  │  - Contract interaction (ChefNFT, GameCore, etc)  │    │
│  │  - Transaction batching                            │    │
│  │  - RPC provider (Base Sepolia)                     │    │
│  └────────────────────────────────────────────────────┘    │
│                          │                                  │
│  ┌────────────────────────────────────────────────────┐    │
│  │  Firebase/Firestore                                │    │
│  │  - Session state caching                           │    │
│  │  - Energy level caching                            │    │
│  │  - Real-time leaderboard                           │    │
│  │  - Player profile data                             │    │
│  └────────────────────────────────────────────────────┘    │
│                          │                                  │
│                          │ Web3 RPC                         │
└──────────────────────────┼──────────────────────────────────┘
                           │
                           ↓
┌─────────────────────────────────────────────────────────────┐
│              SMART CONTRACTS (BASE SEPOLIA)                 │
│  ┌────────────────────────────────────────────────────┐    │
│  │  ChefNFT.sol                                       │    │
│  │  - ERC721 soulbound NFT                            │    │
│  │  - Energy management                               │    │
│  │  - Player names                                    │    │
│  └────────────────────────────────────────────────────┘    │
│  ┌────────────────────────────────────────────────────┐    │
│  │  GameCore.sol                                      │    │
│  │  - Session management                              │    │
│  │  - Move/recipe/cooking tracking                    │    │
│  │  - Onchain score computation                       │    │
│  │  - Global stats accumulation                       │    │
│  └────────────────────────────────────────────────────┘    │
│  ┌────────────────────────────────────────────────────┐    │
│  │  Leaderboard.sol                                   │    │
│  │  - Season-based rankings                           │    │
│  │  - High score tracking                             │    │
│  │  - Rank queries                                    │    │
│  └────────────────────────────────────────────────────┘    │
└─────────────────────────────────────────────────────────────┘
```

---

## 🔄 Data Flow Examples

### **1. Player Login & Wallet Creation**

```
Unity Client                Backend               Smart Contracts
     │                         │                         │
     │ 1. Email OTP Login      │                         │
     ├────────────────────────→│                         │
     │    (Privy Unity SDK)    │                         │
     │                         │                         │
     │ 2. Create Wallet        │                         │
     ├────────────────────────→│                         │
     │                         │                         │
     │ 3. Return Auth Token    │                         │
     │←────────────────────────┤                         │
     │    + Wallet Address     │                         │
     │                         │                         │
     │ 4. POST /api/chef/mint  │                         │
     ├────────────────────────→│                         │
     │    { name, authToken }  │                         │
     │                         │ 5. Verify Token         │
     │                         │    (Privy SDK)          │
     │                         │                         │
     │                         │ 6. ChefNFT.mintChef()   │
     │                         ├────────────────────────→│
     │                         │    (backend signer)     │
     │                         │    (gas sponsored)      │
     │                         │                         │
     │                         │ 7. Tx confirmed         │
     │                         │←────────────────────────┤
     │                         │                         │
     │ 8. Success Response     │                         │
     │←────────────────────────┤                         │
     │    { tokenId, txHash }  │                         │
```

### **2. Game Session Gameplay**

```
Unity Client                Backend               Smart Contracts
     │                         │                         │
     │ POST /api/session/start │                         │
     ├────────────────────────→│                         │
     │                         │ startGameSession()      │
     │                         ├────────────────────────→│
     │                         │                         │
     │ sessionId: 123          │                         │
     │←────────────────────────┤                         │
     │                         │                         │
     │ POST /api/gameplay/move │                         │
     ├────────────────────────→│                         │
     │    (x10 moves)          │ (batched)               │
     │                         ├─ Queue actions          │
     │ 200 OK (instant)        │                         │
     │←────────────────────────┤                         │
     │                         │                         │
     │ Player continues        │ Every 5s or 10 actions: │
     │ playing smoothly        │ recordMove() x10        │
     │                         ├────────────────────────→│
     │                         │ (single batched tx)     │
     │                         │                         │
     │ POST /api/gameplay/     │                         │
     │      recipe-delivered   │                         │
     ├────────────────────────→│ recordRecipeDelivered() │
     │                         ├────────────────────────→│
     │                         │                         │
     │ POST /api/session/end   │                         │
     ├────────────────────────→│ endGameSession()        │
     │                         ├────────────────────────→│
     │                         │ (compute score onchain) │
     │                         │                         │
     │ finalScore: 12450       │                         │
     │←────────────────────────┤                         │
```

### **3. Energy Refill (Player-Paid Transaction)**

```
Unity Client                Backend               Smart Contracts
     │                         │                         │
     │ POST /api/chef/         │                         │
     │      refill-energy      │                         │
     ├────────────────────────→│                         │
     │                         │ 1. Verify player has    │
     │                         │    USDC approval        │
     │                         │                         │
     │                         │ 2. ChefNFT.refillEnergy()│
     │                         ├────────────────────────→│
     │                         │    (backend relays)     │
     │                         │    (player pays gas)    │
     │                         │                         │
     │ Show "pending" toast    │                         │
     │←────────────────────────┤                         │
     │                         │                         │
     │                         │ 3. Wait for confirmation│
     │                         │←────────────────────────┤
     │                         │                         │
     │ Energy +300             │                         │
     │←────────────────────────┤                         │
```

---

## 🔐 Security Architecture

### **Authentication Flow**

```
1. Unity Client
   └─→ Privy Unity SDK handles email/OAuth
   └─→ Creates embedded wallet (keys stored by Privy)
   └─→ Returns JWT auth token

2. All API Requests
   ├─→ Include Privy auth token in header
   │   Authorization: Bearer <privy-token>
   │
   └─→ Backend verifies with Privy SDK
       const user = await privy.verifyAuthToken(token);
       if (!user) throw Unauthorized;
```

### **Private Key Management**

| Key Type | Storage Location | Purpose |
|----------|------------------|---------|
| **Backend Signer** | .env / Secret Manager | Signs all gameplay transactions |
| **Player Wallet** | Privy (encrypted) | Embedded wallet for identity |
| **Owner Wallet** | Hardware wallet | Contract admin operations |

### **Access Control**

```solidity
// Smart Contracts enforce backend-only access
modifier onlyBackend() {
    if (msg.sender != backendSigner) revert UnauthorizedCaller();
    _;
}

// Backend verifies player auth
async function verifyRequest(req) {
    const user = await privy.verifyAuthToken(req.headers.authorization);
    const sessionId = req.body.sessionId;
    const session = await gameCore.getSession(sessionId);
    
    if (session.player !== user.wallet.address) {
        throw Forbidden("Not your session");
    }
}
```

---

## ⚡ Gas Optimization Strategies

### **1. Transaction Batching**

```javascript
// Backend queues actions and batches them
class ActionQueue {
    constructor() {
        this.queue = [];
        this.batchSize = 10;
        this.batchInterval = 5000; // 5 seconds
    }
    
    async add(action) {
        this.queue.push(action);
        
        if (this.queue.length >= this.batchSize) {
            await this.flush();
        }
    }
    
    async flush() {
        const actions = this.queue.splice(0);
        
        // Submit single transaction with multiple recordMove() calls
        const tx = await gameCore.batchRecordMoves(
            sessionId, 
            actions.length
        );
        
        await tx.wait();
    }
}
```

### **2. Privy Gas Sponsorship**

```javascript
// Backend uses Privy to sponsor transactions
import { PrivyClient } from '@privy-io/server-auth';

const privy = new PrivyClient(appId, appSecret);

async function submitSponsoredTransaction(calldata) {
    // Privy pays the gas
    const sponsoredTx = await privy.relayTransaction({
        chain: 'base-sepolia',
        transaction: {
            from: backendSignerAddress,
            to: gameCoreAddress,
            data: calldata,
            value: '0x0'
        }
    });
    
    return sponsoredTx;
}
```

### **3. State Caching (Firestore)**

```javascript
// Cache frequently read data to avoid RPC calls
async function getPlayerEnergy(address) {
    // Try cache first
    const cached = await firestore
        .collection('players')
        .doc(address)
        .get();
    
    if (cached.exists && Date.now() - cached.data().timestamp < 60000) {
        return cached.data().energy;
    }
    
    // Fallback to blockchain
    const energy = await chefNFT.getPlayerEnergy(address);
    
    // Update cache
    await firestore.collection('players').doc(address).set({
        energy,
        timestamp: Date.now()
    });
    
    return energy;
}
```

---

## 🎮 Unity Client Responsibilities

### **What Unity DOES:**

✅ **Authentication**
```csharp
using Privy;

public class PrivyAuthManager : MonoBehaviour {
    async void Login(string email) {
        // Send OTP
        await PrivyManager.Instance.Email.SendCode(email);
        
        // Verify code
        var user = await PrivyManager.Instance.Email.LoginWithCode(email, code);
        
        // Store auth token for API calls
        PlayerPrefs.SetString("privyAuthToken", user.Token);
    }
}
```

✅ **Display Wallet Info**
```csharp
public class WalletDisplay : MonoBehaviour {
    void ShowWallet() {
        var user = PrivyManager.Instance.User;
        var wallet = user.EmbeddedWallets[0];
        
        walletAddressText.text = wallet.Address;
        chefTokenIdText.text = $"Chef #{user.ChefTokenId}";
    }
}
```

✅ **HTTP API Calls**
```csharp
public class GameAPI : MonoBehaviour {
    private string baseUrl = "https://api.sushibonks.com";
    
    async Task<int> StartSession() {
        var response = await httpClient.PostAsync(
            $"{baseUrl}/api/session/start",
            GetAuthHeaders()
        );
        
        var data = await response.Content.ReadAsAsync<SessionResponse>();
        return data.sessionId;
    }
    
    async Task RecordMove(int sessionId) {
        await httpClient.PostAsync(
            $"{baseUrl}/api/gameplay/move",
            new { sessionId },
            GetAuthHeaders()
        );
    }
    
    private Dictionary<string, string> GetAuthHeaders() {
        return new Dictionary<string, string> {
            { "Authorization", $"Bearer {PlayerPrefs.GetString("privyAuthToken")}" }
        };
    }
}
```

### **What Unity DOES NOT:**

❌ Direct blockchain calls  
❌ Gas management  
❌ Private key storage  
❌ Contract ABIs  
❌ RPC provider management  
❌ Transaction signing (except special cases)

---

## 🖥️ Backend Responsibilities

### **What Backend DOES:**

✅ **Privy Auth Verification**
```javascript
import { PrivyClient } from '@privy-io/server-auth';

const privy = new PrivyClient(process.env.PRIVY_APP_ID, process.env.PRIVY_APP_SECRET);

async function verifyAuth(req, res, next) {
    const authToken = req.headers.authorization?.replace('Bearer ', '');
    
    try {
        const user = await privy.verifyAuthToken(authToken);
        req.user = user;
        next();
    } catch (error) {
        res.status(401).json({ error: 'Unauthorized' });
    }
}
```

✅ **Gas Sponsorship**
```javascript
async function sponsorTransaction(tx) {
    const sponsoredTx = await privy.relayTransaction({
        chain: 'base-sepolia',
        transaction: tx
    });
    
    return sponsoredTx;
}
```

✅ **Contract Interactions**
```javascript
import { ethers } from 'ethers';

const provider = new ethers.JsonRpcProvider(process.env.BASE_SEPOLIA_RPC);
const signer = new ethers.Wallet(process.env.BACKEND_PRIVATE_KEY, provider);

const gameCore = new ethers.Contract(gameCoreAddress, gameCoreABI, signer);

async function startSession(playerAddress) {
    const tx = await gameCore.startGameSession(playerAddress);
    await tx.wait();
    return tx.hash;
}
```

✅ **Transaction Batching**
✅ **State Caching**
✅ **Data Aggregation**

---

## 📡 API Endpoint Summary

### **Authentication**
```
POST /api/auth/verify     - Verify Privy token
GET  /api/auth/status     - Check auth status
```

### **Chef NFT**
```
POST /api/chef/mint            - Mint chef NFT (sponsored)
GET  /api/chef/profile         - Get player profile
POST /api/chef/refill-energy   - Refill energy (player pays)
PUT  /api/chef/update-name     - Update name (sponsored)
```

### **Game Session**
```
POST /api/session/start        - Start session (sponsored)
POST /api/session/end          - End session (sponsored)
GET  /api/session/current      - Get current session
GET  /api/session/history      - Get session history
```

### **Gameplay Actions**
```
POST /api/gameplay/move              - Record move (batched)
POST /api/gameplay/recipe-spawned    - Record spawn (batched)
POST /api/gameplay/recipe-delivered  - Record delivery (batched)
POST /api/gameplay/recipe-discarded  - Record discard (batched)
POST /api/gameplay/well-cooked       - Record well-cooked (batched)
POST /api/gameplay/over-cooked       - Record overcooked (batched)
GET  /api/gameplay/session-stats     - Get session stats
```

### **Leaderboard**
```
GET /api/leaderboard/top          - Get top N players
GET /api/leaderboard/player-rank  - Get player rank
GET /api/leaderboard/global-stats - Get global stats
```

---

## 🚀 Deployment Architecture

### **Unity Client**
```
Platform: WebGL, iOS, Android
Hosting: Vercel / Netlify (WebGL build)
App Stores: iOS App Store, Google Play
```

### **Backend**
```
Platform: Node.js 20 LTS
Hosting: PM2 + Nginx (dedicated server) or Cloud Run
Database: Firebase/Firestore
Secrets: .env (self-hosted) or Secret Manager (cloud)
Monitoring: PM2 logs, Nginx access logs
```

### **Smart Contracts**
```
Network: Base Sepolia (testnet)
Future: Base Mainnet
RPC: Base Sepolia public RPC
Verification: Basescan
```

---

## 📊 Performance Metrics

### **Target Performance**

| Metric | Target | Measurement |
|--------|--------|-------------|
| API Response Time | < 200ms | 95th percentile |
| Transaction Confirmation | < 5s | Average |
| Leaderboard Load | < 500ms | 95th percentile |
| Session Start | < 2s | Average |
| Game Move Latency | < 50ms | 95th percentile |

### **Cost Estimates**

| Item | Cost | Frequency |
|------|------|-----------|
| Gas per session | $0 (sponsored) | Per game |
| Backend compute | ~$20/mo | Continuous |
| Firestore reads | ~$5/mo | Per query |
| Energy refill | Player pays | Optional |
| **Total per 1000 players** | **~$25/mo** | **Estimate** |

---

## 🔧 Environment Configuration

### **Unity Client**
```
PRIVY_APP_ID=your_app_id
API_BASE_URL=https://api.sushibonks.com
```

### **Backend**
```
PRIVY_APP_ID=your_app_id
PRIVY_APP_SECRET=your_app_secret
PRIVY_CLIENT_ID=your_client_id

BASE_SEPOLIA_RPC_URL=https://sepolia.base.org
BACKEND_PRIVATE_KEY=0x...

CHEFNFT_ADDRESS=0x...
GAMECORE_ADDRESS=0x...
LEADERBOARD_ADDRESS=0x...

FIREBASE_PROJECT_ID=your_project
FIREBASE_CREDENTIALS={}
```

---

## 📚 Technology Stack Summary

### **Frontend (Unity)**
- Unity 2022+ LTS
- Privy Unity SDK
- C# / .NET
- UnityWebRequest (HTTP client)

### **Backend (Node.js)**
- Node.js 20 LTS
- Express.js
- Privy Server SDK (`@privy-io/server-auth`)
- ethers.js v6
- Firebase Admin SDK

### **Blockchain**
- Solidity 0.8.20
- Hardhat
- OpenZeppelin Contracts
- Base Sepolia

### **Infrastructure**
- PM2 + Nginx (dedicated server) or Cloud Run
- Firebase/Firestore
- .env / Secret Manager
- Base RPC

---

**This architecture provides the optimal balance of:**
- ✅ Free gameplay (gas sponsored)
- ✅ Instant UX (no blockchain waits)
- ✅ Secure (keys on server)
- ✅ Scalable (serverless backend)
- ✅ Simple Unity code (HTTP only)

Next: See `PRIVY_INTEGRATION_GUIDE.md` for implementation details.

