# Backend Setup Guide

## 🚀 Quick Setup (3 options)

### **Option 1: Full Setup (Recommended for production)**

**Prerequisites:**
1. Privy account → Get credentials from [dashboard.privy.io](https://dashboard.privy.io)
2. Firebase project → Create at [console.firebase.google.com](https://console.firebase.google.com)

**Steps:**
```bash
# 1. Run setup script
chmod +x setup-backend-env.sh
./setup-backend-env.sh

# 2. Start server
npm run dev
```

---

### **Option 2: Minimal Setup (For testing blockchain only)**

Create `.env` file manually:

```bash
# Create .env
cat > .env << 'EOF'
# Privy (get from dashboard.privy.io)
PRIVY_APP_ID=your_app_id
PRIVY_APP_SECRET=your_app_secret
PRIVY_CLIENT_ID=your_client_id

# Blockchain (already configured)
BASE_SEPOLIA_RPC_URL=https://sepolia.base.org
BACKEND_PRIVATE_KEY=<PRIVATE_KEY_BACKEND_SIGNER>

# Deployed contracts
CHEFNFT_ADDRESS=0x49889375450342FF44a56891034078Ad982Ef95E
GAMECORE_ADDRESS=0x9e6C89a53E270d87AE1f50b3fe00988Db2323931
LEADERBOARD_ADDRESS=0xB38343DA4Ba50dDD7D6F6d7E7cE6Dc08253C5dA7

# Firebase (optional - leave empty to skip caching)
FIREBASE_PROJECT_ID=
FIREBASE_CREDENTIALS=

# Server
PORT=3000
NODE_ENV=development
ALLOWED_ORIGINS=*
EOF

chmod 600 .env
```

Then:
```bash
npm run dev
```

---

### **Option 3: Skip Firebase (Blockchain + Privy only)**

If you don't have Firebase yet, modify the code to skip Firebase initialization:

1. Comment out Firebase in `src/server.js`:
```javascript
// try {
//     initializeFirebase();
// } catch (error) {
//     console.warn('⚠️  Firebase skipped');
// }
```

2. Comment out cache imports in route files

3. Create minimal .env with just Privy + Blockchain

---

## 🔑 Getting Privy Credentials

### 1. Go to [dashboard.privy.io](https://dashboard.privy.io)

### 2. Create App
- Click "Create App"
- Name: "Sushi Bonks"
- Select: Embedded Wallets

### 3. Get App ID
- Go to Settings → Basic
- Copy **App ID** (starts with `cl...`)

### 4. Get App Secret
- Go to Settings → API Keys
- Create new server-side key
- Copy **App Secret** (starts with `secret_...`)

### 5. Create App Client (for Unity)
- Go to Settings → App Clients
- Create App Client
- Type: Native Mobile App
- Copy **Client ID** (starts with `client_...`)

### 6. Enable Gas Sponsorship
- Go to Wallets → Gas Sponsorship
- Enable for Base Sepolia
- Add contract addresses:
  - `0x49889375450342FF44a56891034078Ad982Ef95E` (ChefNFT)
  - `0x9e6C89a53E270d87AE1f50b3fe00988Db2323931` (GameCore)
  - `0xB38343DA4Ba50dDD7D6F6d7E7cE6Dc08253C5dA7` (Leaderboard)
- Fund sponsorship wallet with Base Sepolia ETH

---

## 🔥 Getting Firebase Credentials

### 1. Go to [console.firebase.google.com](https://console.firebase.google.com)

### 2. Create Project
- Click "Add project"
- Name: "sushi-bonks"
- Disable Google Analytics (optional)

### 3. Enable Firestore
- Build → Firestore Database
- Create database
- Start in **production mode**
- Location: us-central

### 4. Get Service Account
- Project Settings (⚙️) → Service Accounts
- Click "Generate new private key"
- Download JSON file

### 5. Format for .env
The JSON will look like:
```json
{
  "type": "service_account",
  "project_id": "sushi-bonks-xxx",
  "private_key_id": "...",
  "private_key": "...",
  ...
}
```

**Compress it to one line** for .env:
```bash
FIREBASE_CREDENTIALS={"type":"service_account","project_id":"sushi-bonks-xxx",...}
```

---

## ✅ Verification Checklist

Before running the server, verify:

- [ ] `.env` file exists in `backend/` directory
- [ ] `.env` has `chmod 600` permissions
- [ ] PRIVY_APP_ID is set
- [ ] PRIVY_APP_SECRET is set
- [ ] BACKEND_PRIVATE_KEY is set
- [ ] Contract addresses match deployment
- [ ] ABIs copied to `src/abis/`
- [ ] `node_modules/` exists (ran `npm install`)

---

## 🧪 Testing

### Start Server
```bash
npm run dev
```

### Test Health Endpoint
```bash
curl http://localhost:3000/health
```

Expected response:
```json
{
  "status": "ok",
  "timestamp": "2025-10-23T...",
  "service": "sushi-bonks-api",
  "version": "1.0.0"
}
```

### Test Blockchain Connection
Check server logs for:
```
✅ Network connection verified
   Chain ID: 84532
   Block number: 12345678
```

---

## 🐛 Troubleshooting

### "Cannot find module '@privy-io/server-auth'"
```bash
npm install
```

### "Firebase initialization failed"
Either:
1. Fix FIREBASE_CREDENTIALS JSON
2. Or skip Firebase (comment out in server.js)

### "Invalid private key"
Check BACKEND_PRIVATE_KEY format (no `0x` prefix)

### "Contract not deployed"
Verify contract addresses in `.env` match deployment

---

**Once `.env` is configured, run:** `npm run dev` 🚀

