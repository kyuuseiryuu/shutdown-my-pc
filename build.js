/**
 * Build script that compiles:
 *   1. React frontend → out/dist/
 *   2. Bun HTTP server → out/shutdown-my-pc.exe
 *   3. System tray app → out/ShutdownPcTray.exe
 *   4. Merge both into out/ShutdownMyPC.exe (single-file release)
 *
 * The merged exe auto-extracts the server to a temp directory,
 * launches it, and shows a system-tray icon.
 */

import { $ } from "bun";
import { readdirSync, existsSync, writeFileSync, readFileSync, appendFileSync, statSync } from "fs";
import { join } from "path";

const ROOT = join(import.meta.dir ?? ".", "out");
const DIST = join(ROOT, "dist");

console.log("=".repeat(60));
console.log("  🔨 Building Shutdown My PC");
console.log("=".repeat(60));

// ── Step 1: Build frontend ────────────────────────────────────────
console.log("\n📦 Step 1: Building frontend...");
await $`bun run build:frontend`.quiet();
console.log("   ✅ Frontend built successfully");

if (!existsSync(DIST)) {
  console.error("   ❌ dist/ directory not found after build!");
  process.exit(1);
}

const distFiles = readdirSync(DIST, { recursive: true });
console.log(`   📋 Files in dist/: ${distFiles.join(", ")}`);

// ── Step 2: Compile server exe ────────────────────────────────────
console.log("\n🔧 Step 2: Compiling server exe...");
await $`
  bun build ./src/index.ts \
    --compile \
    --outfile=out/shutdown-my-pc \
    --define:process.env.NODE_ENV='"production"'
`.quiet();

const SERVER_EXE = join(ROOT, "shutdown-my-pc.exe");
if (!existsSync(SERVER_EXE)) {
  console.error("   ❌ Server exe not found!");
  process.exit(1);
}
const serverSize = statSync(SERVER_EXE).size;
console.log(`   ✅ Server exe compiled (${(serverSize / 1024 / 1024).toFixed(1)} MB)`);

// ── Step 3: Compile tray app ──────────────────────────────────────
console.log("\n🔧 Step 3: Compiling tray app...");

const CSC = "C:\\Windows\\Microsoft.NET\\Framework\\v4.0.30319\\csc.exe";
const TRAY_SRC = join(import.meta.dir ?? ".", "tray", "TrayApp.cs");
const TRAY_OUT = join(ROOT, "ShutdownPcTray.exe");

let trayBuilt = false;

if (existsSync(CSC) && existsSync(TRAY_SRC)) {
  const result = await $`"${CSC}" -target:winexe -reference:System.Windows.Forms.dll -reference:System.Drawing.dll -reference:System.Net.Http.dll -out:"${TRAY_OUT}" "${TRAY_SRC}"`.nothrow().quiet();

  if (result.exitCode === 0 && existsSync(TRAY_OUT)) {
    const kb = statSync(TRAY_OUT).size / 1024;
    console.log(`   ✅ Tray app compiled (${kb.toFixed(1)} KB)`);
    trayBuilt = true;
  } else {
    console.error(`   ⚠️  Tray compilation failed (exit ${result.exitCode})`);
  }
} else {
  console.log("   ⏭️  Skipping tray (C# compiler or source not found)");
}

// ── Step 4: Merge server exe into tray exe ────────────────────────
const FINAL_OUT = join(ROOT, "ShutdownMyPC.exe");

if (trayBuilt) {
  console.log("\n🔧 Step 4: Merging server into tray exe...");

  const trayData = readFileSync(TRAY_OUT);
  const serverData = readFileSync(SERVER_EXE);

  // Marker: SMPC_SRV\0
  const marker = Buffer.from([0x53, 0x4D, 0x50, 0x43, 0x5F, 0x53, 0x52, 0x56, 0x00]);

  // Write output: tray + padding + marker + server length (BigInt) + server data
  const padding = (4096 - ((trayData.length + marker.length + 8) % 4096)) % 4096;
  const serverLenBuf = Buffer.alloc(8);
  serverLenBuf.writeBigInt64LE(BigInt(serverData.length), 0);

  const merged = Buffer.concat([trayData, Buffer.alloc(padding), marker, serverLenBuf, serverData]);
  writeFileSync(FINAL_OUT, merged);

  const mergedSize = statSync(FINAL_OUT).size;
  console.log(`   ✅ Merged: ShutdownMyPC.exe (${(mergedSize / 1024 / 1024).toFixed(1)} MB)`);
}

// ── Step 5: Summary ───────────────────────────────────────────────
console.log(`\n📦 Outputs in ${ROOT}/:`);
if (trayBuilt) {
  console.log(`   • ShutdownMyPC.exe — ✅ Single-file release (recommended)`);
}
console.log(`   • shutdown-my-pc.exe — HTTP server only`);
if (trayBuilt) {
  console.log(`   • ShutdownPcTray.exe — Tray wrapper (standalone, needs server alongside)`);
}

console.log(`\n✅ Build complete!`);
if (trayBuilt) {
  console.log(`   Run: ./out/ShutdownMyPC.exe`);
} else {
  console.log(`   Run: ./out/shutdown-my-pc.exe`);
}
