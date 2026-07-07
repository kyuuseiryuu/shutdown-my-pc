import { serve } from "bun";
import index from "./index.html";
import { isWindows, PORT, runShutdownCommand, serverResolve, STATIC_DIR } from "./server/utils";

const server = serve({
  routes: {
    // Serve index.html for all unmatched routes.
    "/*": process.env.NODE_ENV === 'production' ? serverResolve : index,

    "/api/power": async (req) => {
      if (!isWindows()) {
        return Response.json(
          { ok: false, error: "Power API is only supported on Windows" },
          { status: 400 },
        );
      }

      const url = new URL(req.url);
      const rawAction = url.searchParams.get("action") ?? "shutdown";
      const action = rawAction.toLowerCase();
      const force = url.searchParams.get("force") !== "false";
      const timeout = Math.max(0, Math.min(600, parseInt(url.searchParams.get("timeout") ?? "30", 10) || 30));

      const ACTIONS: Record<string, { flag: string; label: string; noTimeout: boolean }> = {
        shutdown:  { flag: "-s", label: "Shut down", noTimeout: false },
        restart:   { flag: "-r", label: "Restart", noTimeout: false },
        hibernate: { flag: "-h", label: "Hibernate", noTimeout: false },
        sleep:     { flag: "-hybrid", label: "Sleep", noTimeout: false },
        logout:    { flag: "-l", label: "Log off", noTimeout: true },
      };

      const def = ACTIONS[action];
      if (!def) {
        const valid = Object.keys(ACTIONS).join(", ");
        return Response.json(
          { ok: false, error: `Unknown action "${action}". Valid: ${valid}` },
          { status: 400 },
        );
      }

      const args = [def.flag];
      if (force && !def.noTimeout) args.push("-f");
      if (!def.noTimeout) args.push("-t", String(timeout));

      const result = runShutdownCommand(args);
      const label = def.label;
      const desc = def.noTimeout ? `${label}...` : `${label} in ${timeout} seconds`;

      if (result.ok) {
        console.log(`✅ ${desc}`);
      } else {
        console.error(`❌ ${label} failed:`, result.stderr);
      }

      return Response.json(
        {
          ok: result.ok,
          action,
          message: result.ok ? desc : `Failed to ${action}`,
          details: result.ok ? undefined : result.stderr,
        },
        { status: result.ok ? 200 : 500 },
      );
    },

    // API: Cancel scheduled power action
    "/api/cancel": async () => {
      if (!isWindows()) {
        return Response.json(
          { ok: false, error: "Cancel is only supported on Windows" },
          { status: 400 },
        );
      }

      const result = runShutdownCommand(["-a"]);

      if (result.ok) {
        console.log("✅ Cancelled");
      } else {
        console.error("❌ Cancel failed:", result.stderr);
      }

      return Response.json(
        {
          ok: result.ok,
          message: result.ok
            ? "Scheduled operation has been cancelled"
            : "No pending operation to cancel, or cancellation failed",
          details: result.ok ? undefined : result.stderr,
        },
        { status: result.ok ? 200 : 500 },
      );
    },
  },

  development: process.env.NODE_ENV !== "production" && {
    // Enable browser hot reloading in development
    hmr: true,

    // Echo console logs from the browser to the server
    console: true,
  },
});

// console.log(`🚀 Server running at ${server.url}`);
// ─── Startup ──────────────────────────────────────────────────────────

const url = `http://localhost:${PORT}/`;
console.log(`\n  🚀 Shutdown My PC — Server running at ${url}`);
console.log(`  📁 Serving static files from: ${STATIC_DIR}`);
console.log(`  🔌 Port: ${PORT}  |  PID: ${process.pid}`);
console.log(`  ℹ️  Press Ctrl+C to stop\n`);

// Open browser automatically in production mode
if (process.env.NODE_ENV === "production") {
  setTimeout(() => {
    const openCmd =
      process.platform === "win32"
        ? ["cmd", "/c", "start", url]
        : process.platform === "darwin"
          ? ["open", url]
          : ["xdg-open", url];
    try {
      const proc = Bun.spawn(openCmd);
      proc.unref?.();
    } catch {
      // Browser open is best-effort
    }
  }, 300);
}

// Graceful shutdown
process.on("SIGINT", () => {
  console.log("\n  👋 Shutting down...");
  server.stop?.();
  process.exit(0);
});

process.on("SIGTERM", () => {
  server.stop?.();
  process.exit(0);
});
