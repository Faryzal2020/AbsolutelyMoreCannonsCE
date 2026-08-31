#!/usr/bin/env node
/**
 * Build the dashboard bundle when it is missing or older than its sources, so
 * `npm start` works from a clean checkout without a separate build step.
 */
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { spawnSync } from 'node:child_process';

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
const bundle = path.join(root, 'public', 'index.html');
const uiDir = path.join(root, 'ui');

function newestMtime(dir) {
  let newest = 0;
  for (const entry of fs.readdirSync(dir, { withFileTypes: true })) {
    const full = path.join(dir, entry.name);
    newest = Math.max(newest, entry.isDirectory() ? newestMtime(full) : fs.statSync(full).mtimeMs);
  }
  return newest;
}

const built = fs.existsSync(bundle) ? fs.statSync(bundle).mtimeMs : 0;
const sources = fs.existsSync(uiDir) ? newestMtime(uiDir) : 0;

if (built > sources) process.exit(0);

console.log(built ? 'UI sources changed - rebuilding dashboard...' : 'Building dashboard for the first time...');
const result = spawnSync('npx', ['vite', 'build'], { cwd: root, stdio: 'inherit', shell: process.platform === 'win32' });

if (result.status !== 0) {
  console.error('\nUI build failed. The API still works; run "npm run serve" to start without the dashboard.');
  process.exit(result.status ?? 1);
}
