# Sushi Bonks Backend – PM2 + Nginx Deployment

Deploy the backend on your dedicated cloud server using PM2 (process manager) and Nginx (reverse proxy). This setup avoids expensive managed cloud services and gives you full control.

**Reference:** [Setup Node.js with PM2 and Nginx](https://medium.com/today-i-learned-chai/setup-node-js-application-with-pm2-and-nginx-72840f44ea73)

---

## Prerequisites

- Dedicated cloud server (Ubuntu 20.04+ / Debian 11+)
- SSH access
- Domain name (optional; can use server IP)

---

## Quick Deployment

### 1. Install dependencies on server

```bash
# Node.js 20
curl -fsSL https://deb.nodesource.com/setup_20.x | sudo -E bash -
sudo apt-get install -y nodejs build-essential

# Nginx
sudo apt-get install -y nginx

# PM2 (process manager)
sudo npm install -g pm2
```

Or run the setup script:

```bash
cd backend
bash scripts/setup-server.sh
```

### 2. Deploy the app

```bash
cd /path/to/sushi-nads/backend

# Install dependencies
npm install

# Copy and configure environment
cp env.example.txt .env
nano .env   # Fill in Privy, Firebase, blockchain config, etc.

# Create logs directory
mkdir -p logs

# Start with PM2
pm2 start ecosystem.config.cjs

# Persist across reboots
pm2 save
pm2 startup   # Follow the command it prints
```

### 3. Configure Nginx

```bash
# Copy config
sudo cp nginx/sushi-bonks.conf.example /etc/nginx/conf.d/sushi-bonks.conf

# Edit: replace your_domain.com with your domain or server IP
sudo nano /etc/nginx/conf.d/sushi-bonks.conf

# Verify and reload
sudo nginx -t
sudo systemctl reload nginx
```

### 4. Enable HTTPS (recommended)

```bash
sudo apt install certbot python3-certbot-nginx
sudo certbot --nginx -d your_domain.com
```

---

## File overview

| File | Purpose |
|------|---------|
| `ecosystem.config.cjs` | PM2 process config (name, script, env, logs) |
| `nginx/sushi-bonks.conf.example` | Nginx reverse proxy config |
| `scripts/setup-server.sh` | One-time server setup script |

---

## PM2 commands

```bash
pm2 list              # List processes
pm2 logs sushi-bonks-api   # View logs
pm2 restart sushi-bonks-api
pm2 stop sushi-bonks-api
pm2 delete sushi-bonks-api
pm2 monit              # Real-time monitoring
```

---

## Environment variables

Place all variables in `.env` in the backend directory. See `env.example.txt` for the full list.

| Variable | Required | Notes |
|----------|----------|-------|
| `PRIVY_APP_ID` | ✅ | From Privy Dashboard |
| `PRIVY_APP_SECRET` | ✅ | From Privy Dashboard |
| `PRIVY_CLIENT_ID` | ✅ | From Privy Dashboard |
| `PRIVY_BACKEND_AUTH_KEY_ID` | ✅ | For gas sponsorship |
| `PRIVY_BACKEND_AUTH_PRIVATE_KEY` | ✅ | For gas sponsorship |
| `BACKEND_PRIVATE_KEY` | ✅ | Backend signer (no `0x` prefix) |
| `CHEFNFT_ADDRESS` | ✅ | On-chain contract |
| `GAMECORE_ADDRESS` | ✅ | On-chain contract |
| `LEADERBOARD_ADDRESS` | ✅ | On-chain contract |
| `FIREBASE_PROJECT_ID` | ✅ | Firebase project ID |
| `FIREBASE_CREDENTIALS` | ✅ | Service account JSON (required on self-hosted) |
| `ALLOWED_ORIGINS` | ✅ | Comma-separated origins (Unity, itch.io) |
| `PORT` | Optional | Default 3000 (must match Nginx proxy_pass) |

### Firebase on self-hosted

On a dedicated server, you must provide `FIREBASE_CREDENTIALS` (service account JSON). Application Default Credentials are not available outside GCP.

---

## CORS for Unity WebGL

Set `ALLOWED_ORIGINS` in `.env`:

```
ALLOWED_ORIGINS=https://your-game.itch.io,https://game.yourdomain.com
```

---

## Verify deployment

```bash
# Health check (replace with your domain or IP)
curl http://your_domain.com/health

# Expected response
{"status":"ok","timestamp":"...","service":"sushi-bonks-api","version":"1.0.0"}
```

---

## Update Unity to use hosted backend

Set the API base URL in your Unity config to:

```
https://your_domain.com
```

(or `http://your_server_ip` if not using HTTPS)

---

## Troubleshooting

| Issue | Possible fix |
|-------|--------------|
| 502 Bad Gateway | App not running: `pm2 list`, `pm2 logs sushi-bonks-api` |
| Port mismatch | Ensure Nginx `proxy_pass` port matches `PORT` in .env |
| CORS errors | Add Unity/itch.io origin to `ALLOWED_ORIGINS` |
| Firebase auth fails | Verify `FIREBASE_CREDENTIALS` JSON is valid |
| Privy auth fails | Verify `PRIVY_APP_SECRET` and token format |
| PM2 not found after reboot | Run `pm2 startup` and `pm2 save` |

---

## Deployment workflow (updates)

```bash
cd /path/to/sushi-nads
git pull
cd backend
npm install
pm2 restart sushi-bonks-api
```

---

## Security notes

- Keep `.env` out of version control (already in `.gitignore`)
- Use HTTPS in production (certbot)
- Restrict firewall: `ufw allow 80`, `ufw allow 443`, `ufw enable`
- Consider fail2ban for brute-force protection
