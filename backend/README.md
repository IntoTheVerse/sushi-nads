# Sushi Bonks Backend API

Node.js/Express backend with Privy integration and Firebase for Sushi Bonks game.

---

## 🚀 Quick Start

### 1. Install Dependencies
```bash
npm install
```

### 2. Copy Contract ABIs
```bash
mkdir -p src/abis
cp ../blockchain/artifacts/contracts/ChefNFT.sol/ChefNFT.json src/abis/
cp ../blockchain/artifacts/contracts/GameCore.sol/GameCore.json src/abis/
cp ../blockchain/artifacts/contracts/Leaderboard.sol/Leaderboard.json src/abis/
```

### 3. Configure Environment
```bash
cp env.example.txt .env
nano .env  # Fill in your values
```

### 4. Run Development Server
```bash
npm run dev
```

Server will start on `http://localhost:3000`

---

## 🔐 Environment Variables

See `env.example.txt` for all required variables:

- **Privy**: App ID, App Secret, Client ID
- **Blockchain**: RPC URL, Backend private key, Contract addresses
- **Firebase**: Project ID, Service account credentials
- **Server**: Port, environment, CORS origins

---

## 📡 API Endpoints

### Authentication
All endpoints (except `/health` and `/api/leaderboard/*`) require Privy auth token:
```
Authorization: Bearer <privy-token>
```

### Chef NFT
```
POST   /api/chef/mint              # Mint chef NFT
GET    /api/chef/profile           # Get player profile
POST   /api/chef/refill-energy     # Refill energy (TODO)
PUT    /api/chef/update-name       # Update chef name
```

### Session
```
POST   /api/session/start          # Start game session
POST   /api/session/end            # End game session
GET    /api/session/current        # Get current session
GET    /api/session/history        # Get session history
```

### Gameplay
```
POST   /api/gameplay/move              # Record move (batched)
POST   /api/gameplay/recipe-spawned    # Record recipe spawn
POST   /api/gameplay/recipe-delivered  # Record delivery
POST   /api/gameplay/recipe-discarded  # Record discard
POST   /api/gameplay/well-cooked       # Record perfect cook
POST   /api/gameplay/over-cooked       # Record overcook
GET    /api/gameplay/session-stats     # Get session stats
```

### Leaderboard
```
GET    /api/leaderboard/top           # Get top players
GET    /api/leaderboard/player-rank   # Get player rank
GET    /api/leaderboard/global-stats  # Get global stats
```

---

## 🧪 Testing

### Health Check
```bash
curl http://localhost:3000/health
```

### With Privy Token
```bash
# Get profile
curl http://localhost:3000/api/chef/profile \
  -H "Authorization: Bearer YOUR_PRIVY_TOKEN"
```

---

## 📊 Features

### ✅ Implemented
- Privy authentication verification
- Gas-sponsored transactions (for gameplay)
- Transaction batching (moves queued)
- Firestore caching (energy, sessions, leaderboard)
- Rate limiting (by IP and player)
- Error handling
- CORS configuration

### 🔄 TODO
- Energy refill with USDC (requires Privy signature)
- Webhook listeners for transaction events
- Advanced monitoring
- Load testing

---

## 🏗️ Project Structure

```
backend/
├── src/
│   ├── config/           # Configuration files
│   ├── services/         # Business logic
│   ├── middleware/       # Express middleware
│   ├── routes/           # API routes
│   ├── abis/             # Contract ABIs
│   └── server.js         # Main entry point
└── test/                 # Tests
```

---

## 🔒 Security

- ✅ Privy token verification on all protected routes
- ✅ Rate limiting (100 req/min per IP, 20 actions/sec per player)
- ✅ Input validation
- ✅ Helmet.js security headers
- ✅ CORS configuration
- ✅ Backend signer key in .env (gitignored)

**Production TODO:**
- Move secrets to GCP Secret Manager
- Enable HTTPS only
- Add API key authentication for admin routes
- Implement request signing

---

## 📦 Deployment

### GCP Cloud Run
```bash
# Build
gcloud builds submit --tag gcr.io/PROJECT_ID/sushi-bonks-api

# Deploy
gcloud run deploy sushi-bonks-api \
  --image gcr.io/PROJECT_ID/sushi-bonks-api \
  --platform managed \
  --region us-central1 \
  --allow-unauthenticated
```

### Set Environment Variables
```bash
gcloud run services update sushi-bonks-api \
  --set-env-vars "PRIVY_APP_ID=xxx,..."
```

---

## 📚 Documentation

- [Privy Docs](https://docs.privy.io)
- [Ethers.js Docs](https://docs.ethers.org)
- [Express Docs](https://expressjs.com)
- [Firebase Docs](https://firebase.google.com/docs)

---

## 🐛 Troubleshooting

### "Cannot find module 'X'"
```bash
npm install
```

### "Invalid auth token"
- Check PRIVY_APP_SECRET is correct
- Verify token is sent with "Bearer " prefix

### "Transaction reverted"
- Check contract addresses in .env
- Verify backend signer is authorized
- Check player has sufficient energy

### "Firebase error"
- Verify FIREBASE_CREDENTIALS JSON is valid
- Check Firebase project ID

---

**Ready to serve! 🍣**

