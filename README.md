# Shutdown My PC

> Power management from your browser — a sleek dark-themed UI for Windows shutdown, restart, hibernate, sleep, and logoff operations.

![Screenshot](screenshots/screenshots.png)

## ✨ Features

- **5 Power Actions** — Shut Down, Restart, Hibernate, Sleep, Log Off
- **Custom Timer** — Set a countdown from 0 to 600 seconds (10 minutes)
- **Force Close Apps** — Optionally force running applications to close
- **Cancel Scheduled Operation** — Abort any pending shutdown at the click of a button
- **Dark Theme** — Modern dark UI built with [Ant Design 5](https://ant.design/) and custom CSS
- **Bun-powered** — Built on [Bun](https://bun.sh), a fast all-in-one JavaScript runtime

> **Note:** The power management API (`shutdown.exe`) is **Windows-only**. The server will gracefully return an error on other platforms.

## 🚀 Getting Started

### Prerequisites

- [Bun](https://bun.sh) v1.x installed
- **Windows** (for power API functionality; the UI will still render on other platforms)

### Install

```bash
bun install
```

### Development

Start the dev server with hot module replacement (HMR):

```bash
bun dev
```

The server will start on `http://localhost:3021` by default.  
Override the port via the `PORT` environment variable or pass it as a CLI argument:

```bash
PORT=8080 bun dev
# or
bun dev 8080
```

### Production Build

Build the frontend assets and compile a standalone executable:

```bash
bun run build          # builds both frontend and executable
```

Or step by step:

```bash
bun run build:frontend # outputs to out/dist/
bun run build:exe      # compiles to out/shutdown-my-pc.exe
```

Start the production server:

```bash
bun start
```

In production mode, the server will automatically open the browser.

## 🖥️ API Reference

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

## 🧩 Tech Stack

| Layer    | Technology |
|----------|------------|
| Runtime  | [Bun](https://bun.sh) |
| Frontend | React 19 + TypeScript |
| UI       | Ant Design 5 + @ant-design/icons |
| Styling  | Custom CSS with CSS custom properties |
| Bundling | Bun's built-in bundler + HMR |

## 📁 Project Structure

```
shutdown-my-pc/
├── screenshots/
│   └── screenshots.png
├── src/
│   ├── server/
│   │   └── utils.ts        # Server utilities (port, shutdown, static files)
│   ├── App.tsx              # Main React component
│   ├── frontend.tsx         # React entry point
│   ├── index.css            # Global styles
│   ├── index.html           # HTML template
│   └── index.ts             # Server entry point + API routes
├── build.js                 # Build script
├── package.json
└── tsconfig.json
```

## 📄 License

MIT

