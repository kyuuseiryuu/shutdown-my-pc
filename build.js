/**
 * Build script that produces a single executable:
 *   out/ShutdownMyPC.exe
 *
 * The exe embeds the Bun HTTP server (appended after a marker),
 * a custom tray icon (via -win32icon), and a System Tray wrapper
 * that auto-extracts and launches the server as a hidden child process.
 */

import { $ } from "bun";
import { readdirSync, existsSync, writeFileSync, readFileSync, statSync, unlinkSync } from "fs";
import { join } from "path";

const ROOT = join(import.meta.dir ?? ".", "out");
const DIST = join(ROOT, "dist");
const TRAY_ICO = join(import.meta.dir ?? ".", "tray", "tray-icon.ico");

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

// ── Step 2: Compile server exe (temp) ─────────────────────────────
console.log("\n🔧 Step 2: Compiling server...");
const TMP_SERVER = join(ROOT, ".server.tmp");
await $`bun build ./src/index.ts --compile --outfile="${TMP_SERVER}" --define:process.env.NODE_ENV='"production"'`.nothrow().quiet();

// Bun appends .exe automatically on Windows
const TMP_SERVER_EXE = TMP_SERVER + ".exe";
if (!existsSync(TMP_SERVER_EXE)) {
  console.error("   ❌ Server exe not found!");
  process.exit(1);
}
const serverSize = statSync(TMP_SERVER_EXE).size;
console.log(`   ✅ Server exe compiled (${(serverSize / 1024 / 1024).toFixed(1)} MB)`);

// ── Step 3: Compile tray app (with embedded icon) ─────────────────
console.log("\n🔧 Step 3: Compiling tray wrapper...");

const CSC = "C:\\Windows\\Microsoft.NET\\Framework\\v4.0.30319\\csc.exe";
const TRAY_SRC = join(import.meta.dir ?? ".", "tray", "TrayApp.cs");
const TRAY_TMP = join(ROOT, ".tray.tmp");

let trayBuilt = false;

if (existsSync(CSC) && existsSync(TRAY_SRC)) {
    const result = await $`"${CSC}" -target:winexe -win32icon:"${TRAY_ICO}" -reference:System.Windows.Forms.dll -reference:System.Drawing.dll -reference:System.Net.Http.dll -out:"${TRAY_TMP}" "${TRAY_SRC}"`.nothrow().quiet();

  if (result.exitCode === 0 && existsSync(TRAY_TMP)) {
    const kb = statSync(TRAY_TMP).size / 1024;
    console.log(`   ✅ Tray app compiled (${kb.toFixed(1)} KB)`);
    trayBuilt = true;
  } else {
    console.error(`   ⚠️  Tray compilation failed (exit ${result.exitCode})`);
  }
}

if (!trayBuilt) {
  console.error("\n   ❌ Tray build failed — cannot produce output.");
  process.exit(1);
}

// ── Step 4: Merge server into tray ────────────────────────────────
console.log("\n🔧 Step 4: Merging → final exe...");

const FINAL_OUT = join(ROOT, "ShutdownMyPC.exe");
const trayData = readFileSync(TRAY_TMP);
const serverData = readFileSync(TMP_SERVER_EXE);

  // Marker: SMPC_SRV\0
  const marker = Buffer.from([0x53, 0x4D, 0x50, 0x43, 0x5F, 0x53, 0x52, 0x56, 0x00]);

// Align to 4KB boundary after tray data + marker + 8 bytes length
const dataStart = trayData.length + marker.length + 8;
const padding = (4096 - (dataStart % 4096)) % 4096;

  const serverLenBuf = Buffer.alloc(8);
  serverLenBuf.writeBigInt64LE(BigInt(serverData.length), 0);

  const merged = Buffer.concat([trayData, Buffer.alloc(padding), marker, serverLenBuf, serverData]);
  writeFileSync(FINAL_OUT, merged);

  const mergedSize = statSync(FINAL_OUT).size;
// ── Step 5: Cleanup temp files ────────────────────────────────────
try { unlinkSync(TMP_SERVER_EXE); } catch {}
try { unlinkSync(TRAY_TMP); } catch {}

// ── Summary ───────────────────────────────────────────────────────
console.log(`   ✅ ${join("out", "ShutdownMyPC.exe")} (${(mergedSize / 1024 / 1024).toFixed(1)} MB)`);
console.log(`\n✅ Build complete!`);
console.log(`   Run: ./out/ShutdownMyPC.exe`);

