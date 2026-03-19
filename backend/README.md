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

### 4. Run the Server

Choose **one** of these (don't run both at once):

| Method | Use case |
|--------|----------|
| `npm run dev` | Active development – auto-restarts on code changes |
| `pm2 start ecosystem.config.cjs` | Background process – keeps running after closing terminal |

**Development (with auto-reload):**
```bash
npm run dev
```

**Production / background (PM2):**
```bash
pm2 start ecosystem.config.cjs
pm2 save
pm2 startup   # Follow the command it prints to enable auto-start on reboot
```

---

## 🔄 PM2 Commands

| Command | Description |
|---------|-------------|
| `pm2 list` | List all processes |
| `pm2 logs sushi-bonks-api` | View logs (live) |
| `pm2 restart sushi-bonks-api` | Restart after code changes |
| `pm2 stop sushi-bonks-api` | Stop the app |
| `pm2 delete sushi-bonks-api` | Remove from PM2 |
| `pm2 monit` | Real-time monitoring dashboard |

**Auto-start on reboot:** Run `pm2 startup` and execute the command it prints (includes your Node path).

---

## 🏥 Health Check

```bash
curl http://localhost:8081/health
```

Expected response:
```json
{"status":"ok","timestamp":"...","service":"sushi-bonks-api","version":"1.0.0"}
```

Server runs on `http://localhost:8081` – set this as the API base URL in Unity.

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

### With Privy Token
```bash
# Get profile
curl http://localhost:8081/api/chef/profile \
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
├── nginx/                # Nginx config for production (reverse proxy)
├── scripts/              # CLI tools (manageSession, setup-server)
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

### PM2 + Nginx (recommended – dedicated server)

Deploy on your own cloud server. See **[DEPLOYMENT.md](./DEPLOYMENT.md)** for full instructions.

```bash
cd backend
npm install
cp env.example.txt .env
# Edit .env with your values
pm2 start ecosystem.config.cjs
pm2 save && pm2 startup
```

**Nginx reverse proxy** (production server only – not needed for local dev):

`nginx/sushi-bonks.conf.example` configures Nginx to listen on port 80 and forward requests to your Node app on port 8081.

**How to use it:**

1. Copy the file on the server:
   ```bash
   sudo cp backend/nginx/sushi-bonks.conf.example /etc/nginx/conf.d/sushi-bonks.conf
   ```

2. Edit the config:
   ```bash
   sudo nano /etc/nginx/conf.d/sushi-bonks.conf
   ```
   Replace `your_domain.com` with your domain or server IP (e.g. `api.sushibonks.com` or `123.45.67.89`)

3. Check and reload Nginx:
   ```bash
   sudo nginx -t
   sudo systemctl reload nginx
   ```

4. Optional – HTTPS:
   ```bash
   sudo certbot --nginx -d your_domain.com
   ```

**Summary:** Local dev: ignore this file. Production server: copy it, set `server_name`, then reload Nginx.

Backend URL: `https://your-domain.com` (or `http://your-server-ip`)

### GCP Cloud Run (alternative)

```bash
gcloud builds submit --tag gcr.io/PROJECT_ID/sushi-bonks-api
gcloud run deploy sushi-bonks-api \
  --image gcr.io/PROJECT_ID/sushi-bonks-api \
  --platform managed \
  --region us-central1 \
  --allow-unauthenticated
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

