/**
 * Build script that embeds frontend static assets into the exe.
 *
 * Approach:
 * 1. Build the React frontend → dist/
 * 2. Read all static files and generate an inline asset map
 * 3. Compile the server together with embedded assets into a single .exe
 */

import { $ } from "bun";
import { readdirSync, existsSync } from "fs";
import { join } from "path";

const ROOT = join(import.meta.dir ?? ".", "out");
const DIST = join(ROOT, "dist");

console.log("=".repeat(60));
console.log("  🔨 Building Shutdown My PC");
console.log("=".repeat(60));

// Step 1: Build frontend
console.log("\n📦 Step 1: Building frontend...");
await $`bun run build:frontend`.quiet();
console.log("   ✅ Frontend built successfully");

// Step 2: Verify dist contents
if (!existsSync(DIST)) {
  console.error("   ❌ dist/ directory not found after build!");
  process.exit(1);
}

const distFiles = readdirSync(DIST, { recursive: true });
console.log(`   📋 Files in dist/: ${distFiles.join(", ")}`);

// Step 3: Build the server exe
console.log("\n🔧 Step 2: Compiling server exe...");
await $`
  bun build ./src/index.ts \
    --compile \
    --outfile=out/shutdown-my-pc \
    --define:process.env.NODE_ENV='"production"' \
    // --sourcemap=external \
    // --minify
`.quiet();

console.log("   ✅ Exe compiled successfully");

// Step 4: Show output
const exeName = "shutdown-my-pc.exe";
const exePath = join(ROOT, exeName);
if (existsSync(exePath)) {
  const size = (await Bun.file(exePath).size) / (1024 * 1024);
  console.log(`\n📦 Output: ${exeName} (${size.toFixed(1)} MB)`);
} else {
  console.error(`\n   ❌ Expected exe not found at ${exePath}`);
  process.exit(1);
}

console.log("\n✅ Build complete!");
console.log("   Run: ./out/shutdown-my-pc.exe");
