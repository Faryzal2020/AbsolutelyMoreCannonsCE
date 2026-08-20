#!/usr/bin/env node
/**
 * Single entry point: boot the local server, then open the dashboard.
 *   npm start        build the UI if needed, serve, and open the browser
 *   npm run serve    same without opening a browser (CI / agent use)
 */

import net from 'node:net';
import { buildApp } from './src/app.js';
import { MOD_ROOT, DB_PATH, BUILDINGS_DIR } from './src/paths.js';

const HOST = process.env.HOST || '127.0.0.1';
const BASE_PORT = Number(process.env.PORT || 3000);
const shouldOpen = !process.argv.includes('--no-open') && process.env.AMC_NO_OPEN !== '1';

/** Resolve the first free port at or after `start`, so a stale server never blocks boot. */
async function findPort(start, attempts = 20) {
  for (let port = start; port < start + attempts; port++) {
    const free = await new Promise((resolve) => {
      const probe = net.createServer();
      probe.once('error', () => resolve(false));
      probe.once('listening', () => probe.close(() => resolve(true)));
      probe.listen(port, HOST);
    });
    if (free) return port;
  }
  throw new Error(`No free port found in range ${start}-${start + attempts}.`);
}

async function main() {
  const port = await findPort(BASE_PORT);
  const { fastify, db } = await buildApp({ logger: false });

  await fastify.listen({ port, host: HOST });
  const url = `http://localhost:${port}`;

  console.log('');
  console.log('  AMC Turret Editor');
  console.log(`  ${'-'.repeat(46)}`);
  console.log(`  Dashboard   ${url}`);
  console.log(`  API docs    ${url}/api/openapi.json`);
  console.log(`  Mod root    ${MOD_ROOT}`);
  console.log(`  Scanning    ${BUILDINGS_DIR}`);
  console.log(`  Database    ${DB_PATH}  (${db.kind})`);
  if (port !== BASE_PORT) console.log(`  Note        port ${BASE_PORT} was busy, using ${port}`);
  console.log('');
  console.log('  Press Ctrl+C to stop.');
  console.log('');

  if (shouldOpen) {
    try {
      const { default: open } = await import('open');
      await open(url);
    } catch (err) {
      console.warn(`  (Could not open a browser automatically: ${err.message})`);
    }
  }

  for (const signal of ['SIGINT', 'SIGTERM']) {
    process.on(signal, async () => {
      await fastify.close();
      db.close();
      process.exit(0);
    });
  }
}

main().catch((err) => {
  console.error('Failed to start AMC Turret Editor:');
  console.error(err);
  process.exit(1);
});
