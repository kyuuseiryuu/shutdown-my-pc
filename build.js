/**
 * Build script that produces a single executable:
 *   out/ShutdownMyPC.exe
 *
 * The exe is a Windows tray application (C#) with:
 * - Built-in HTTP server (HttpListener) serving the frontend SPA
 * - System tray icon with context menu
 * - Power management API (shutdown.exe)
 * - Static files served from out/dist/ (built frontend)
 */

import { $ } from "bun";
import { existsSync, statSync, unlinkSync } from "fs";
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

// ── Step 2: Compile tray app ──────────────────────────────────────
console.log("\n🔧 Step 2: Compiling tray app...");
const CSC = "C:\\Windows\\Microsoft.NET\\Framework\\v4.0.30319\\csc.exe";
const TRAY_DIR = join(import.meta.dir ?? ".", "tray");
const FINAL_OUT = join(ROOT, "ShutdownMyPC.exe");

let trayBuilt = false;

if (existsSync(CSC)) {
  // Collect all C# source files in the tray/ directory
  const { readdirSync } = await import("fs");
  const sources = readdirSync(TRAY_DIR)
    .filter(f => f.endsWith(".cs") && f !== "ServerSize.cs") // exclude auto-generated
    .map(f => join(TRAY_DIR, f));

  const result = await $`"${CSC}" -target:winexe -win32icon:"${TRAY_ICO}" -reference:System.Windows.Forms.dll -reference:System.Drawing.dll -reference:System.Net.Http.dll -reference:Microsoft.VisualBasic.dll -out:"${FINAL_OUT}" ${sources}`.nothrow().quiet();

  if (result.exitCode === 0 && existsSync(FINAL_OUT)) {
    const kb = statSync(FINAL_OUT).size / 1024;
    console.log(`   ✅ Tray app compiled (${kb.toFixed(1)} KB)`);
    trayBuilt = true;
  } else {
    console.error(`   ⚠️  Tray compilation failed (exit ${result.exitCode})`);
  }
}

if (!trayBuilt) {
  console.error("\n   ❌ Build failed — cannot produce output.");
  process.exit(1);
}

// ── Step 3: Cleanup temp files ────────────────────────────────────
try { unlinkSync(join(TRAY_DIR, "ServerSize.cs")); } catch {}
try { unlinkSync(join(ROOT, ".server.tmp.exe")); } catch {}
try { unlinkSync(join(ROOT, ".tray.tmp")); } catch {}
// ── Summary ───────────────────────────────────────────────────────
const finalSize = statSync(FINAL_OUT).size;
console.log(`\n✅ Build complete!`);
console.log(`   ${join("out", "ShutdownMyPC.exe")} (${(finalSize / 1024 / 1024).toFixed(1)} MB)`);
console.log(`   Frontend build: out/dist/ (served by embedded HTTP server)`);
console.log(`   Run: ./out/ShutdownMyPC.exe`);

