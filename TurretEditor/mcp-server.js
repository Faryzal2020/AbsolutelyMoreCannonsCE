#!/usr/bin/env node
/**
 * MCP entry point: expose the turret editor pipeline to AI clients over stdio.
 *   npm run mcp
 *
 * stdout carries JSON-RPC frames and nothing else — every diagnostic goes to
 * stderr, or the transport breaks.
 */

import { StdioServerTransport } from '@modelcontextprotocol/sdk/server/stdio.js';
import { getDb, closeDb } from './src/db.js';
import { runExtraction } from './src/services/extractService.js';
import { extractionState } from './src/mcp/tools.js';
import { syncIfStale } from './src/services/syncService.js';
import { createMcpServer } from './src/mcp/server.js';
import { MOD_ROOT, DB_PATH } from './src/paths.js';

const log = (...parts) => process.stderr.write(`${parts.join(' ')}\n`);

async function main() {
  const db = await getDb();

  log('AMC Turret Editor MCP');
  log(`  mod root  ${MOD_ROOT}`);
  log(`  database  ${DB_PATH} (${db.kind})`);

  // Only a database that has never been populated is extracted automatically:
  // runExtraction() wipes the tables, so it must never run over pending edits.
  const state = extractionState(db);
  if (state.needed) {
    log(`  extracting (${state.reason}) ...`);
    const result = runExtraction(db);
    log(`  extracted ${result.counts.turrets} turrets, ${result.counts.ammo} ammo in ${result.durationMs}ms`);
  } else {
    log(`  ${state.turrets} turrets, ${state.ammo} ammo, ${state.pending} with pending edits`);
    // Pick up XML changed while the server was not running; pending edits are replayed.
    const sync = syncIfStale(db);
    if (sync) {
      const n = sync.files.changed.length + sync.files.added.length + sync.files.removed.length;
      log(`  re-synced from disk (${sync.reason}${n ? `, ${n} file(s)` : ''}); `
        + `${sync.preserved.turrets + sync.preserved.ammo} pending edit(s) kept, ${sync.conflicts.length} conflict(s)`);
    }
  }

  await createMcpServer({ db }).connect(new StdioServerTransport());
  log('  ready on stdio');
}

for (const signal of ['SIGINT', 'SIGTERM']) {
  process.on(signal, async () => {
    await closeDb();
    process.exit(0);
  });
}

main().catch(async (err) => {
  log(`fatal: ${err.stack || err.message}`);
  await closeDb();
  process.exit(1);
});
