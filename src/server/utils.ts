import { file } from "bun";
import { join } from "path";
import { existsSync } from "fs";

export const PORT = resolvePort();

/** Determine the base directory for static assets. */

export function getStaticDir(): string {
  const IS_DEV = process.env.NODE_ENV !== "production";

  // In development, serve from src/ so Bun can use HMR
  if (IS_DEV) {
    // import.meta.dir is the directory of the current file (src/)
    const metaDir = import.meta.dir ?? ".";
    if (existsSync(metaDir)) return metaDir;
    const cwdSrc = join(process.cwd(), "src");
    if (existsSync(cwdSrc)) return cwdSrc;
    return ".";
  }

  // In production, serve from dist/
  const candidates = [
    join(import.meta.dir ?? ".", "..", "dist"),
    join(import.meta.dir ?? ".", "dist"),
    join(process.cwd(), "dist"),
  ];
  for (const dir of candidates) {
    if (existsSync(dir)) return dir;
  }
  return join(process.cwd(), "dist");
}

export const STATIC_DIR = getStaticDir();

export const MIME_TYPES: Record<string, string> = {
  html: "text/html; charset=utf-8",
  css: "text/css; charset=utf-8",
  js: "application/javascript; charset=utf-8",
  svg: "image/svg+xml",
  png: "image/png",
  ico: "image/x-icon",
  json: "application/json",
};

export function resolvePort(): number {
  // Priority: CLI arg > PORT env var > default 3000
  const cliArg = process.argv[2];
  if (cliArg && /^\d+$/.test(cliArg)) {
    return parseInt(cliArg, 10);
  }
  const envPort = process.env.PORT;
  if (envPort && /^\d+$/.test(envPort)) {
    return parseInt(envPort, 10);
  }
  return 3021;
}

export function isWindows(): boolean {
  return process.platform === "win32";
}

export function runShutdownCommand(
  args: string[],
  customCmd?: string,
): {
  ok: boolean;
  stdout: string;
  stderr: string;
  exitCode: number | null;
} {
  const cmd = customCmd ?? "shutdown";
  const fullCmd = [cmd, ...args];

  if (!isWindows()) {
    return {
      ok: false,
      stdout: "",
      stderr: "Shutdown API is only supported on Windows",
      exitCode: -1,
    };
  }

  try {
    const proc = Bun.spawnSync({ cmd: fullCmd });
    return {
      ok: proc.exitCode === 0,
      stdout: proc.stdout?.toString() || "",
      stderr: proc.stderr?.toString() || "",
      exitCode: proc.exitCode,
    };
  } catch (err) {
    const msg = err instanceof Error ? err.message : String(err);
    return { ok: false, stdout: "", stderr: msg, exitCode: null };
  }
}
export const serverResolve = async (req: Request) => {
      const url = new URL(req.url);
      let filePath = url.pathname.replace(/^\//, "");
      if (!filePath) filePath = "index.html";

      const ext = filePath.split(".").pop()?.toLowerCase() ?? "";
      const mime = MIME_TYPES[ext] ?? "application/octet-stream";
      const absPath = join(STATIC_DIR, filePath);
      const f = file(absPath);

      if (await f.exists()) {
        return new Response(f, { headers: { "Content-Type": mime } });
      }

      // Fallback to index.html (SPA)
      const indexFile = file(join(STATIC_DIR, "index.html"));
      if (await indexFile.exists()) {
        return new Response(indexFile, {
          headers: { "Content-Type": "text/html; charset=utf-8" },
        });
      }

      return new Response(
        `<!DOCTYPE html><html><head><meta charset="UTF-8"><title>Shutdown My PC</title></head><body>
          <h1>⚠️ Build not found</h1>
          <p>Run <code>bun run build:frontend</code> first, or place <code>dist/</code> next to the executable.</p>
        </body></html>`,
        { status: 200, headers: { "Content-Type": "text/html; charset=utf-8" } },
      );
    }