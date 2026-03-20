# WebGL Build & Deployment Guide

How to build and publish the SushiNads WebGL build.

---

## 1. Pre-build checklist

### Unity settings

1. **API environment** – For production, set `GameAPIClient` (in MainMenuScene or a persistent prefab) to:
   - **Api Environment:** `Hosted`
   - **Hosted Base Url:** `https://api-sushinads.cryptara.world` (or your backend URL)

2. **WebGL template** (optional) – In **Edit > Project Settings > Player > WebGL**:
   - **Resolution and Presentation > WebGL Template:** `Default` (or `unity-webview-2020` if you need embedded webview)

3. **Scenes in build** – In **File > Build Settings**, ensure these scenes are enabled:
   - `Assets/Scenes/SplashScreen.unity`
   - `Assets/Scenes/MainMenuScene.unity`
   - `Assets/Scenes/GameScene.unity`

### Other Settings (Edit > Project Settings > Player > Other Settings)

With **WebGL** selected in the platform dropdown:

| Setting | Production | Development |
|---------|------------|-------------|
| **Scripting Backend** | IL2CPP (WebGL default) | IL2CPP |
| **API Compatibility Level** | .NET Standard 2.1 (or match your project) | Same |
| **Managed Stripping Level** | Medium or High (smaller build) | Low or Minimal |
| **Strip Engine Code** | Enabled | Optional |

### Publishing Settings (Edit > Project Settings > Player > Publishing Settings)

With **WebGL** selected:

| Setting | Production | Notes |
|---------|------------|-------|
| **Compression Format** | Brotli | Smaller downloads. Use Gzip if your host doesn't support Brotli. |
| **Decompression Fallback** | Enabled | Lets older browsers fall back to uncompressed if Brotli fails. |
| **Data Caching** | Enabled | Caches build data in IndexedDB for faster reloads. |
| **Exception Support** | None or Explicitly Thrown | Full = larger build; None = smallest. Use Explicitly Thrown if you need try/catch. |
| **Debug Symbols** | Disabled | Enable only for development builds. |
| **Memory Size** | Initial: 32 MB, Max: 512–2048 MB | Increase max if the game runs out of memory. |
| **Code Optimization** | Size | Use Size for production; Speed for development. |

**Host compatibility:** itch.io and many CDNs support Brotli. If you see load failures, switch to **Gzip** and enable **Decompression Fallback**.

---

## 2. Build in Unity

### Option A: Menu (recommended)

1. **Assets > Build WebGL** – Production build
2. **Assets > Build WebGL (Development)** – Development build (with debug symbols)

### Option B: Build Settings

1. **File > Build Settings**
2. Select **WebGL**
3. Click **Build** or **Build And Run**
4. Choose output folder (e.g. `WebGLBuild`)

Output is written to `WebGLBuild/` (ignored by git).

---

## 3. Deploy the build

### Option A: Same server as backend (Nginx)

If your backend is on a VPS with Nginx, serve the WebGL build from the same server:

1. Copy the `WebGLBuild` folder to the server, e.g. `/var/www/sushinads-game/`
2. Add a new Nginx server block (or extend the existing one):

```nginx
# WebGL game (static files)
server {
    listen 80;
    server_name game.yourdomain.com;  # or sushinads.yourdomain.com

    root /var/www/sushinads-game;
    index index.html;

    location / {
        try_files $uri $uri/ /index.html;
    }

    # Cache static assets
    location ~* \.(js|data|unityweb|mem)$ {
        expires 1y;
        add_header Cache-Control "public, immutable";
    }
}
```

3. Enable HTTPS with certbot:

```bash
sudo certbot --nginx -d game.yourdomain.com
```

### Option B: Vercel / Netlify

1. Create a new project and connect your repo (or upload the `WebGLBuild` folder).
2. Set the **publish directory** to `WebGLBuild` (or the folder containing `index.html`).
3. Deploy.

### Option C: itch.io

1. Create a new project on [itch.io](https://itch.io).
2. Upload the contents of `WebGLBuild` as a zip or via the web uploader.
3. Set **Kind of project** to `HTML`.
4. Enable **This file will be played in the browser**.

---

## 4. Backend CORS

Add your WebGL game URL to `ALLOWED_ORIGINS` in `backend/.env`:

```
ALLOWED_ORIGINS=https://game.yourdomain.com,https://your-game.itch.io,https://sushinads.cryptara.world,http://localhost:8080
```

Include `http://localhost:8080` for local testing. Restart the backend after changing `.env`.

---

## 5. Privy Web client

In the Privy dashboard (Web tab), add your WebGL origin to **Allowed origins**:

- `https://game.yourdomain.com`
- `https://your-game.itch.io`
- `http://localhost:8080` (for local testing and Unity Editor Play mode)

**Important:** Configure this before testing. The Privy SDK sends an Origin header for WebGL and Editor; the value must match an allowed origin exactly (including scheme and port).

---

## 6. Local testing

1. Build WebGL to `WebGLBuild`.
2. Start the backend locally (e.g. `pm2 start ecosystem.config.cjs` in `backend/`).
3. Serve the WebGL folder:

```bash
cd WebGLBuild
npx serve -l 8080
```

4. Open **`http://localhost:8080`** in a browser (not your LAN IP).
5. Ensure:
   - Backend `.env` has `ALLOWED_ORIGINS` including `http://localhost:8080`
   - Privy **Allowed origins** includes `http://localhost:8080`
   - Restart backend after changing `.env` (`pm2 restart sushi-bonks-api`)

**Runtime override:** When `Auto Use Local Backend When On Localhost` is enabled (default), the game served from `http://localhost:8080` uses the local backend (`http://localhost:8081`). Disable it if you want a Hosted build to always use the hosted API when testing locally (the hosted backend must allow `http://localhost:8080` in CORS).

**Accessing via LAN IP (e.g. `http://192.168.0.103:8080`):** The browser sends that as the Origin. Add it to Privy **Allowed origins** and `ALLOWED_ORIGINS`, or use `localhost` instead.

---

## Troubleshooting

| Issue | Fix |
|-------|-----|
| CORS errors | Add the game URL to `ALLOWED_ORIGINS` and restart the backend. For local testing: add `http://localhost:8080` to the **deployed** backend’s env (e.g. on api-sushinads.cryptara.world). |
| Privy login fails | Add the game URL (or `http://localhost:8080` for local) to Privy **Web > Allowed origins** |
| `invalid_native_app_id` | Fixed in project: Privy SDK skips native app ID on WebGL/Editor. Rebuild if using an old build. |
| `missing_origin` | Fixed in project: Origin header is sent for WebGL/Editor. Ensure origin is in Privy allowed list. |
| `invalid_origin` / Privy 403 | The URL you use (e.g. `http://192.168.0.103:8080`) must be in Privy **Allowed origins**. Use `http://localhost:8080` or add your LAN IP. |
| Privy iframe blocked (CSP) | Same as above: add your access URL to Privy allowed origins. |
| Blank screen | Check browser console; ensure API base URL is correct |
| Build fails | Switch to WebGL in Build Settings first; check for compile errors |
