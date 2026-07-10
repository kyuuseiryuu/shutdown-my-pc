# 💻 Shutdown My PC

> Power management from your browser — a sleek dark-themed UI for Windows shutdown, restart, hibernate, sleep, and logoff operations.

🌐 **English** · [中文](README.zh-CN.md) · [日本語](README.ja-JP.md)

![Screenshot](screenshots/screenshots.png)

## 🎯 Motivation

**Wake-on-LAN (WOL)** makes it easy to remotely power on a PC, but **remotely powering off** is still a hassle — you typically have to RDP in, open a terminal or the Start menu, and manually click through the shutdown process. This is inconvenient, slow, and ties you to a full desktop session just to run a single command.

**Shutdown My PC** solves this by providing a clean web interface that triggers the native `shutdown.exe` command. It is especially useful for:

- **Headless desktops / media servers** — woken via WOL but with no easy way to shut down.
- **Home lab machines** — power off remotely without RDP.
- **Gaming PCs used for remote streaming** — shut them down after a session without logging in.

Set it as a **Windows startup program**, and the web UI will be available automatically whenever the machine is on. No RDP, no remote desktop — just open a browser tab.

> 🔒 **Security Note:** This tool exposes power management commands over HTTP. **Do not** expose it directly to the public internet. Use a **VPN** (Tailscale, WireGuard, OpenVPN) to access it remotely, or place a **reverse proxy** (NGINX, Caddy) in front with proper **basic authentication** or **OAuth2** middleware.

## ✨ Features

- **5 Power Actions** — Shut Down, Restart, Hibernate, Sleep, Log Off
- **Custom Timer** — Set a countdown from 0 to 600 seconds (10 minutes)
- **Force Close Apps** — Optionally force running applications to close
- **Cancel Scheduled Operation** — Abort any pending shutdown at the click of a button
- **Dark Theme** — Modern dark UI built with [Ant Design 5](https://ant.design/) and custom CSS
- **Fully Portable** — Single ~1 MB `.exe` with embedded frontend and HTTP server
- **No Runtime Required** — Built-in C# `HttpListener`, no Bun/Node.js needed
- **Port Conflict Handling** — If port 3021 is in use, a dialog prompts for a new port
- **Single Instance** — Only one instance runs at a time

> **Note:** The power management API (`shutdown.exe`) is **Windows-only**.

## 🚀 Getting Started

### Download

Download the latest `ShutdownMyPC.exe` from [Releases](https://github.com/kyuuseiryuu/shutdown-my-pc/releases) — no installation needed.

### Build from Source

#### Prerequisites

- [Bun](https://bun.sh) v1.x (for building frontend only)
- **Windows** (with .NET Framework 4.5+, pre-installed on modern Windows)
- C# compiler (`csc.exe`, included with .NET Framework)

#### Build

```bash
bun install
bun run build          # builds single out/ShutdownMyPC.exe
```

The output is a single native Windows executable with all frontend files embedded.

### 📌 Set as Windows Startup Program

1. Press **Win + R**, type `shell:startup`, and press Enter to open the **Startup** folder.
2. Create a shortcut to `ShutdownMyPC.exe`.
3. The tray icon and server will start automatically on every boot.

Access the UI at `http://<your-pc-ip>:3021/` from any device on your local network.

## 🔒 Security (Important)

This tool is designed for **local network use**. The API endpoints accept requests without authentication, so anyone who can reach the port can shut down your PC.

| Access Method | Recommendation |
|---------------|----------------|
| **Same LAN** | ✅ Safe — no additional config needed |
| **Remote / Internet** | ❌ **Never expose directly** — always use one of the methods below |

### Option A: VPN (Recommended)

Connect to your home network via a VPN (e.g., **Tailscale**, **WireGuard**, **OpenVPN**) and access the UI using the machine's local IP. No open ports needed.

### Option B: Reverse Proxy with Authentication

Use **NGINX** (or Caddy, HAProxy) as a reverse proxy with basic authentication:

```nginx
server {
    listen 443 ssl;
    server_name shutdown.example.com;

    ssl_certificate     /path/to/cert.pem;
    ssl_certificate_key /path/to/key.pem;

    location / {
        auth_basic           "Restricted";
        auth_basic_user_file /etc/nginx/.htpasswd;

        proxy_pass http://127.0.0.1:3021;
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
    }
}
```

Generate an `.htpasswd` file:

```bash
echo "user:$(openssl passwd -apr1)" > /etc/nginx/.htpasswd
```

## 📖 API Reference

### `GET /api/power`

Trigger a power action.

**Query Parameters:**

| Parameter | Type    | Default      | Description |
|-----------|---------|--------------|-------------|
| `action`  | string  | `shutdown`   | One of: `shutdown`, `restart`, `hibernate`, `sleep`, `logout` |
| `timeout` | number  | `30`         | Delay in seconds (0–600). Ignored for `logout`. |
| `force`   | boolean | `true`       | Whether to force-close running applications. Ignored for `logout`. |

**Response:**

```json
{
  "ok": true,
  "action": "shutdown",
  "message": "Shut down in 30 seconds"
}
```

Error response:

```json
{
  "ok": false,
  "error": "Unknown action \"reboot\". Valid: shutdown, restart, hibernate, sleep, logout"
}
```

---

### `GET /api/cancel`

Cancel any currently scheduled power operation (equivalent to `shutdown -a`).

**Response:**

```json
{
  "ok": true,
  "message": "Scheduled operation has been cancelled"
}
```

## 🧱 Tech Stack

| Layer    | Technology |
|----------|------------|
| HTTP Server | C# `HttpListener` (.NET Framework) |
| Frontend | React 19 + TypeScript |
| UI       | Ant Design 5 + @ant-design/icons |
| Styling  | Custom CSS with CSS custom properties |
| Tray     | C# `NotifyIcon` (System.Windows.Forms) |
| Bundler  | Bun (for frontend build only) |

## 📁 Project Structure

```
shutdown-my-pc/
├── screenshots/
│   └── screenshots.png
├── src/
│   ├── App.tsx              # Main React component
│   ├── frontend.tsx         # React entry point
│   ├── index.css            # Global styles
│   ├── index.html           # HTML template
│   └── index.ts             # Server entry point (dev mode only)
├── tray/
│   ├── Program.cs           # Entry point + port conflict detection
│   ├── TrayApp.cs           # System tray wrapper (NotifyIcon)
│   ├── HttpServer.cs        # HTTP server (HttpListener)
│   ├── PowerManager.cs      # shutdown.exe wrapper
│   ├── StaticFiles.cs       # Embedded file storage
│   ├── EmbeddedFiles.cs     # Auto-generated by build.js
│   └── tray-icon.ico        # Custom tray icon
├── build.js                 # Build script (generates EmbeddedFiles.cs, compiles)
├── package.json
└── tsconfig.json
```

## 📄 License

MIT
