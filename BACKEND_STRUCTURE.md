# Backend Project Structure

Complete file structure for the Node.js/Express backend with Privy integration.

---

## 📁 Directory Structure

```
backend/
├── src/
│   ├── config/
│   │   ├── firebase.js              # Firebase/Firestore initialization
│   │   └── constants.js             # Game constants, scoring config
│   │
│   ├── services/
│   │   ├── privy.service.js         # Privy SDK integration
│   │   ├── blockchain.service.js    # Ethers.js contract interactions
│   │   ├── cache.service.js         # Firestore caching layer
│   │   └── scoring.service.js       # Score computation (mirrors onchain)
│   │
│   ├── middleware/
│   │   ├── auth.js                  # Privy auth verification
│   │   ├── rateLimit.js             # Rate limiting
│   │   ├── validation.js            # Request validation
│   │   └── errorHandler.js          # Global error handler
│   │
│   ├── routes/
│   │   ├── auth.routes.js           # Authentication endpoints
│   │   ├── chef.routes.js           # Chef NFT endpoints
│   │   ├── session.routes.js        # Session management
│   │   ├── gameplay.routes.js       # Gameplay actions
│   │   └── leaderboard.routes.js    # Leaderboard queries
│   │
│   ├── abis/
│   │   ├── ChefNFT.json             # ChefNFT ABI
│   │   ├── GameCore.json            # GameCore ABI
│   │   └── Leaderboard.json         # Leaderboard ABI
│   │
│   ├── utils/
│   │   ├── logger.js                # Winston logging
│   │   └── helpers.js               # Utility functions
│   │
│   └── server.js                    # Main Express server
│
├── test/
│   ├── auth.test.js                 # Auth tests
│   ├── chef.test.js                 # Chef NFT tests
│   ├── session.test.js              # Session tests
│   └── gameplay.test.js             # Gameplay tests
│
├── .env.example                     # Environment template
├── .env                             # Environment variables (gitignored)
├── .gitignore
├── package.json
├── package-lock.json
└── README.md
```

---

## 📄 Complete File Contents

### **package.json**

```json
{
  "name": "sushi-bonks-backend",
  "version": "1.0.0",
  "description": "Sushi Bonks game backend with Privy integration",
  "type": "module",
  "main": "src/server.js",
  "scripts": {
    "start": "node src/server.js",
    "dev": "nodemon src/server.js",
    "test": "node --test test/*.test.js",
    "lint": "eslint src/",
    "deploy": "gcloud run deploy"
  },
  "keywords": ["blockchain", "game", "privy", "base"],
  "author": "Sushi Bonks Team",
  "license": "MIT",
  "dependencies": {
    "@privy-io/server-auth": "^1.9.0",
    "cors": "^2.8.5",
    "dotenv": "^16.3.1",
    "ethers": "^6.10.0",
    "express": "^4.18.2",
    "firebase-admin": "^12.0.0",
    "helmet": "^7.1.0",
    "winston": "^3.11.0"
  },
  "devDependencies": {
    "nodemon": "^3.0.2",
    "eslint": "^8.56.0"
  },
  "engines": {
    "node": ">=20.0.0",
    "npm": ">=9.0.0"
  }
}
```

### **src/config/firebase.js**

```javascript
import admin from 'firebase-admin';

const serviceAccount = JSON.parse(process.env.FIREBASE_CREDENTIALS || '{}');

admin.initializeApp({
    credential: admin.credential.cert(serviceAccount),
    projectId: process.env.FIREBASE_PROJECT_ID
});

export const db = admin.firestore();
export const auth = admin.auth();

export default admin;
```

### **src/config/constants.js**

```javascript
// Game configuration (must match smart contract values)
export const GAME_CONFIG = {
    DURATION: 1000, // seconds
    MOVE_COST: 1, // energy per move
    MAX_ENERGY: 300,
    ENERGY_REFILL_AMOUNT: 300,
    ENERGY_REFILL_COST_USDC: '100000', // 0.1 USDC (6 decimals)
};

// Scoring configuration (must match GameCore.sol)
export const SCORING_CONFIG = {
    deliveryBonus: 100,
    cookBonus: 10,
    discardPenalty: 50,
    overcookPenalty: 20,
    movePenalty: 1,
};

// Batching configuration
export const BATCH_CONFIG = {
    maxBatchSize: 10, // actions
    maxBatchInterval: 5000, // ms
};

// Rate limiting
export const RATE_LIMITS = {
    loginAttempts: 5, // per 15 minutes
    apiCalls: 100, // per minute
    gameActions: 20, // per second
};

export default {
    GAME_CONFIG,
    SCORING_CONFIG,
    BATCH_CONFIG,
    RATE_LIMITS,
};
```

### **src/services/cache.service.js**

```javascript
import { db } from '../config/firebase.js';

// Cache player energy
export async function getCachedEnergy(address) {
    const doc = await db.collection('players').doc(address).get();
    
    if (doc.exists) {
        const data = doc.data();
        const age = Date.now() - data.timestamp;
        
        // Cache valid for 60 seconds
        if (age < 60000) {
            return data.energy;
        }
    }
    
    return null;
}

export async function setCachedEnergy(address, energy) {
    await db.collection('players').doc(address).set({
        energy,
        timestamp: Date.now()
    }, { merge: true });
}

// Cache session state
export async function getCachedSession(sessionId) {
    const doc = await db.collection('sessions').doc(sessionId.toString()).get();
    return doc.exists ? doc.data() : null;
}

export async function setCachedSession(sessionId, data) {
    await db.collection('sessions').doc(sessionId.toString()).set({
        ...data,
        updatedAt: Date.now()
    }, { merge: true });
}

// Cache leaderboard
export async function getCachedLeaderboard(season, limit) {
    const cacheKey = `leaderboard_${season}_${limit}`;
    const doc = await db.collection('cache').doc(cacheKey).get();
    
    if (doc.exists) {
        const data = doc.data();
        const age = Date.now() - data.timestamp;
        
        // Cache valid for 5 minutes
        if (age < 300000) {
            return data.rankings;
        }
    }
    
    return null;
}

export async function setCachedLeaderboard(season, limit, rankings) {
    const cacheKey = `leaderboard_${season}_${limit}`;
    await db.collection('cache').doc(cacheKey).set({
        rankings,
        timestamp: Date.now()
    });
}

export default {
    getCachedEnergy,
    setCachedEnergy,
    getCachedSession,
    setCachedSession,
    getCachedLeaderboard,
    setCachedLeaderboard,
};
```

### **src/middleware/validation.js**

```javascript
export function validateMintChef(req, res, next) {
    const { playerName } = req.body;
    
    if (!playerName || typeof playerName !== 'string') {
        return res.status(400).json({ error: 'playerName is required' });
    }
    
    if (playerName.length < 3 || playerName.length > 20) {
        return res.status(400).json({ error: 'Name must be 3-20 characters' });
    }
    
    // Sanitize name (alphanumeric + spaces)
    if (!/^[a-zA-Z0-9\s]+$/.test(playerName)) {
        return res.status(400).json({ error: 'Name can only contain letters, numbers, and spaces' });
    }
    
    next();
}

export function validateSessionId(req, res, next) {
    const sessionId = req.body.sessionId || req.query.sessionId;
    
    if (!sessionId) {
        return res.status(400).json({ error: 'sessionId is required' });
    }
    
    if (isNaN(sessionId) || sessionId < 1) {
        return res.status(400).json({ error: 'Invalid sessionId' });
    }
    
    req.sessionId = parseInt(sessionId);
    next();
}

export default {
    validateMintChef,
    validateSessionId,
};
```

### **src/middleware/rateLimit.js**

```javascript
import { db } from '../config/firebase.js';
import { RATE_LIMITS } from '../config/constants.js';

const rateLimitCache = new Map();

export async function rateLimitByIP(req, res, next) {
    const ip = req.ip || req.connection.remoteAddress;
    const now = Date.now();
    const windowMs = 60000; // 1 minute
    
    const key = `ratelimit:${ip}`;
    const record = rateLimitCache.get(key) || { count: 0, resetTime: now + windowMs };
    
    if (now > record.resetTime) {
        // Reset window
        record.count = 1;
        record.resetTime = now + windowMs;
    } else {
        record.count++;
    }
    
    rateLimitCache.set(key, record);
    
    if (record.count > RATE_LIMITS.apiCalls) {
        return res.status(429).json({ error: 'Too many requests, please try again later' });
    }
    
    next();
}

export async function rateLimitGameActions(req, res, next) {
    const address = req.walletAddress;
    const now = Date.now();
    const windowMs = 1000; // 1 second
    
    const key = `gameaction:${address}`;
    const record = rateLimitCache.get(key) || { count: 0, resetTime: now + windowMs };
    
    if (now > record.resetTime) {
        record.count = 1;
        record.resetTime = now + windowMs;
    } else {
        record.count++;
    }
    
    rateLimitCache.set(key, record);
    
    if (record.count > RATE_LIMITS.gameActions) {
        return res.status(429).json({ error: 'Too many game actions, slow down!' });
    }
    
    next();
}

export default {
    rateLimitByIP,
    rateLimitGameActions,
};
```

### **src/routes/session.routes.js**

```javascript
import express from 'express';
import { gameCore } from '../services/blockchain.service.js';
import { requireAuth } from '../middleware/auth.js';
import { rateLimitGameActions } from '../middleware/rateLimit.js';
import { setCachedSession } from '../services/cache.service.js';

const router = express.Router();

// POST /api/session/start
router.post('/start', requireAuth, rateLimitGameActions, async (req, res) => {
    try {
        const playerAddress = req.walletAddress;
        
        // Check if player has chef
        const hasChef = await chefNFT.playerHasChef(playerAddress);
        if (!hasChef) {
            return res.status(400).json({ error: 'No chef NFT found' });
        }
        
        // Check energy
        const energy = await chefNFT.getPlayerEnergy(playerAddress);
        if (energy < 5) {
            return res.status(400).json({ error: 'Insufficient energy (minimum 5)' });
        }
        
        // Start session on blockchain (gas sponsored)
        const tx = await gameCore.startGameSession(playerAddress);
        const receipt = await tx.wait();
        
        // Parse SessionStarted event
        const event = receipt.logs.find(log => 
            log.topics[0] === gameCore.interface.getEvent('SessionStarted').topicHash
        );
        const sessionId = parseInt(event.topics[1], 16);
        
        // Cache session
        await setCachedSession(sessionId, {
            sessionId,
            player: playerAddress,
            startTime: Date.now(),
            isActive: true,
            moveCount: 0
        });
        
        res.json({
            success: true,
            sessionId,
            txHash: receipt.hash,
            startTime: Date.now()
        });
    } catch (error) {
        console.error('Start session error:', error);
        res.status(500).json({ error: error.message });
    }
});

// POST /api/session/end
router.post('/end', requireAuth, async (req, res) => {
    try {
        const { sessionId } = req.body;
        const playerAddress = req.walletAddress;
        
        // Verify session ownership
        const session = await gameCore.getSession(sessionId);
        if (session.player.toLowerCase() !== playerAddress.toLowerCase()) {
            return res.status(403).json({ error: 'Not your session' });
        }
        
        if (!session.isActive) {
            return res.status(400).json({ error: 'Session already ended' });
        }
        
        // End session (gas sponsored)
        const tx = await gameCore.endGameSession(sessionId, 'TIME_UP');
        const receipt = await tx.wait();
        
        // Get final score from event
        const event = receipt.logs.find(log =>
            log.topics[0] === gameCore.interface.getEvent('ScoreFinalized').topicHash
        );
        const finalScore = parseInt(event.data.slice(0, 66), 16);
        
        res.json({
            success: true,
            sessionId,
            finalScore,
            txHash: receipt.hash
        });
    } catch (error) {
        console.error('End session error:', error);
        res.status(500).json({ error: error.message });
    }
});

// GET /api/session/current
router.get('/current', requireAuth, async (req, res) => {
    try {
        const playerAddress = req.walletAddress;
        const sessionId = await gameCore.getCurrentSessionId(playerAddress);
        
        if (sessionId === 0) {
            return res.json({ hasActiveSession: false });
        }
        
        const session = await gameCore.getSession(sessionId);
        
        res.json({
            hasActiveSession: true,
            sessionId: Number(sessionId),
            moveCount: Number(session.moveCount),
            recipesDelivered: Number(session.recipesDelivered),
            recipesDiscarded: Number(session.recipesDiscarded)
        });
    } catch (error) {
        console.error('Get current session error:', error);
        res.status(500).json({ error: error.message });
    }
});

export default router;
```

### **src/routes/gameplay.routes.js**

```javascript
import express from 'express';
import { gameCore } from '../services/blockchain.service.js';
import { requireAuth } from '../middleware/auth.js';
import { validateSessionId } from '../middleware/validation.js';
import { rateLimitGameActions } from '../middleware/rateLimit.js';

const router = express.Router();

// Action queue for batching
const actionQueues = new Map();

// POST /api/gameplay/move
router.post('/move', requireAuth, validateSessionId, rateLimitGameActions, async (req, res) => {
    try {
        const { sessionId } = req;
        const playerAddress = req.walletAddress;
        
        // Verify session
        const session = await gameCore.getSession(sessionId);
        if (session.player.toLowerCase() !== playerAddress.toLowerCase()) {
            return res.status(403).json({ error: 'Not your session' });
        }
        
        if (!session.isActive) {
            return res.status(400).json({ error: 'Session not active' });
        }
        
        // Queue action for batching
        if (!actionQueues.has(sessionId)) {
            actionQueues.set(sessionId, []);
        }
        
        actionQueues.get(sessionId).push({ type: 'move', timestamp: Date.now() });
        
        // Process queue if >= 10 actions or 5 seconds old
        await processQueueIfNeeded(sessionId);
        
        // Return immediately (UX optimization)
        res.json({ success: true });
    } catch (error) {
        console.error('Record move error:', error);
        res.status(500).json({ error: error.message });
    }
});

// POST /api/gameplay/recipe-delivered
router.post('/recipe-delivered', requireAuth, validateSessionId, async (req, res) => {
    try {
        const { sessionId } = req;
        
        // Record immediately (important game event)
        const tx = await gameCore.recordRecipeDelivered(sessionId);
        await tx.wait();
        
        res.json({ success: true });
    } catch (error) {
        console.error('Record recipe delivered error:', error);
        res.status(500).json({ error: error.message });
    }
});

// POST /api/gameplay/recipe-discarded
router.post('/recipe-discarded', requireAuth, validateSessionId, async (req, res) => {
    try {
        const { sessionId } = req;
        
        const tx = await gameCore.recordRecipeDiscarded(sessionId);
        await tx.wait();
        
        res.json({ success: true });
    } catch (error) {
        console.error('Record recipe discarded error:', error);
        res.status(500).json({ error: error.message });
    }
});

// POST /api/gameplay/well-cooked
router.post('/well-cooked', requireAuth, validateSessionId, async (req, res) => {
    try {
        const { sessionId } = req;
        
        const tx = await gameCore.recordWellCooked(sessionId);
        await tx.wait();
        
        res.json({ success: true });
    } catch (error) {
        console.error('Record well-cooked error:', error);
        res.status(500).json({ error: error.message });
    }
});

// POST /api/gameplay/over-cooked
router.post('/over-cooked', requireAuth, validateSessionId, async (req, res) => {
    try {
        const { sessionId } = req;
        
        const tx = await gameCore.recordOverCooked(sessionId);
        await tx.wait();
        
        res.json({ success: true });
    } catch (error) {
        console.error('Record overcooked error:', error);
        res.status(500).json({ error: error.message });
    }
});

// Helper: Process action queue
async function processQueueIfNeeded(sessionId) {
    const queue = actionQueues.get(sessionId);
    if (!queue || queue.length === 0) return;
    
    const oldestAction = queue[0];
    const age = Date.now() - oldestAction.timestamp;
    
    // Flush if >= 10 actions or oldest is >= 5 seconds old
    if (queue.length >= 10 || age >= 5000) {
        await flushQueue(sessionId);
    }
}

async function flushQueue(sessionId) {
    const queue = actionQueues.get(sessionId);
    if (!queue || queue.length === 0) return;
    
    const moves = queue.filter(a => a.type === 'move');
    
    if (moves.length > 0) {
        try {
            // Submit batched moves
            for (let i = 0; i < moves.length; i++) {
                await gameCore.recordMove(sessionId);
            }
            
            console.log(`✅ Flushed ${moves.length} moves for session ${sessionId}`);
        } catch (error) {
            console.error('Flush queue error:', error);
        }
    }
    
    // Clear queue
    actionQueues.delete(sessionId);
}

// Periodic queue flush (every 5 seconds)
setInterval(() => {
    for (const [sessionId] of actionQueues) {
        flushQueue(sessionId);
    }
}, 5000);

export default router;
```

### **src/routes/leaderboard.routes.js**

```javascript
import express from 'express';
import { leaderboard, gameCore } from '../services/blockchain.service.js';
import { getCachedLeaderboard, setCachedLeaderboard } from '../services/cache.service.js';

const router = express.Router();

// GET /api/leaderboard/top
router.get('/top', async (req, res) => {
    try {
        const limit = parseInt(req.query.limit) || 100;
        const season = parseInt(req.query.season) || 1;
        
        // Check cache first
        const cached = await getCachedLeaderboard(season, limit);
        if (cached) {
            return res.json({ rankings: cached, cached: true });
        }
        
        // Fetch from blockchain
        const [players, scores] = await leaderboard.getTopPlayers(limit);
        
        const rankings = players.map((player, index) => ({
            rank: index + 1,
            player,
            score: Number(scores[index])
        }));
        
        // Cache result
        await setCachedLeaderboard(season, limit, rankings);
        
        res.json({ rankings, cached: false });
    } catch (error) {
        console.error('Get leaderboard error:', error);
        res.status(500).json({ error: error.message });
    }
});

// GET /api/leaderboard/player-rank
router.get('/player-rank', async (req, res) => {
    try {
        const playerAddress = req.query.address;
        const season = parseInt(req.query.season) || 1;
        
        if (!playerAddress) {
            return res.status(400).json({ error: 'address is required' });
        }
        
        const rank = await leaderboard.getPlayerRank(playerAddress);
        const score = await leaderboard.getPlayerHighScore(playerAddress);
        
        res.json({
            rank: Number(rank),
            score: Number(score),
            season
        });
    } catch (error) {
        console.error('Get player rank error:', error);
        res.status(500).json({ error: error.message });
    }
});

export default router;
```

### **.env.example**

```bash
# Privy Configuration
PRIVY_APP_ID=clxxx...
PRIVY_APP_SECRET=secret_xxx...
PRIVY_CLIENT_ID=client_xxx...

# Blockchain
BASE_SEPOLIA_RPC_URL=https://sepolia.base.org
BACKEND_PRIVATE_KEY=0x...

# Contract Addresses
CHEFNFT_ADDRESS=0x...
GAMECORE_ADDRESS=0x...
LEADERBOARD_ADDRESS=0x...

# Firebase
FIREBASE_PROJECT_ID=your-project-id
FIREBASE_CREDENTIALS={"type":"service_account",...}

# Server
PORT=3000
NODE_ENV=development

# CORS
ALLOWED_ORIGINS=http://localhost:3000,https://sushibonks.com
```

### **.gitignore**

```
node_modules/
.env
*.log
.DS_Store
dist/
build/
coverage/
.vscode/
.idea/
```

---

## 🚀 Quick Start

```bash
# 1. Clone and setup
cd backend
npm install

# 2. Copy environment file
cp .env.example .env

# 3. Update .env with your values
nano .env

# 4. Copy contract ABIs
mkdir -p src/abis
cp ../blockchain/artifacts/contracts/ChefNFT.sol/ChefNFT.json src/abis/
cp ../blockchain/artifacts/contracts/GameCore.sol/GameCore.json src/abis/
cp ../blockchain/artifacts/contracts/Leaderboard.sol/Leaderboard.json src/abis/

# 5. Start development server
npm run dev

# Server running on http://localhost:3000
```

---

## 📝 API Testing with curl

```bash
# Health check
curl http://localhost:3000/health

# Mint chef (requires Privy token)
curl -X POST http://localhost:3000/api/chef/mint \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer YOUR_PRIVY_TOKEN" \
  -d '{"playerName":"TestChef"}'

# Get profile
curl http://localhost:3000/api/chef/profile \
  -H "Authorization: Bearer YOUR_PRIVY_TOKEN"

# Start session
curl -X POST http://localhost:3000/api/session/start \
  -H "Authorization: Bearer YOUR_PRIVY_TOKEN"

# Get leaderboard
curl http://localhost:3000/api/leaderboard/top?limit=10
```

---

## 🔧 Development Tips

### **Hot Reload with Nodemon**
Nodemon automatically restarts the server when files change:
```bash
npm run dev
```

### **Debugging**
Add breakpoints in VS Code or use:
```javascript
console.log('Debug:', variable);
```

### **Testing Blockchain Calls**
Use Hardhat console:
```bash
cd blockchain
npx hardhat console --network baseSepolia

> const ChefNFT = await ethers.getContractFactory("ChefNFT");
> const chef = await ChefNFT.attach("0x...");
> await chef.totalChefs();
```

---

**Next: Deploy to GCP Cloud Run** (see deployment guide)

