import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const here = path.dirname(fileURLToPath(import.meta.url));
const REAL_MOD_ROOT = path.resolve(here, '..', '..');

/**
 * Copy the real Common/Defs tree into a throwaway directory and point the app
 * at it. Tests therefore run against genuine mod XML — with its comments, tabs
 * and inheritance — without ever writing to the user's files.
 */
export function makeFixture() {
  const root = fs.mkdtempSync(path.join(os.tmpdir(), 'amc-turret-editor-'));
  fs.cpSync(path.join(REAL_MOD_ROOT, 'Common', 'Defs'), path.join(root, 'Common', 'Defs'), {
    recursive: true,
    // The working tree carries stale .xml.bak files from the previous Python
    // tooling; copying them would let rollback tests restore unrelated files.
    filter: (src) => !src.endsWith('.xml.bak'),
  });

  process.env.AMC_MOD_ROOT = root;
  process.env.AMC_DB_PATH = path.join(root, 'DevTools', 'turret_editor.db');

  return {
    root,
    file: (relPath) => path.join(root, ...relPath.split('/')),
    read: (relPath) => fs.readFileSync(path.join(root, ...relPath.split('/')), 'utf8'),
    exists: (relPath) => fs.existsSync(path.join(root, ...relPath.split('/'))),
    cleanup: () => fs.rmSync(root, { recursive: true, force: true }),
  };
}

/**
 * Fresh app instance bound to the current fixture. Modules are imported lazily
 * so the env vars set by makeFixture() are read first.
 */
export async function startApp() {
  const { buildApp } = await import('../src/app.js');
  const { closeDb } = await import('../src/db.js');
  const { fastify, db } = await buildApp();
  return {
    fastify,
    db,
    async close() {
      await fastify.close();
      await closeDb();
    },
    async get(url) { return json(await fastify.inject({ method: 'GET', url })); },
    async post(url, payload) { return json(await fastify.inject({ method: 'POST', url, payload: payload ?? {} })); },
    async put(url, payload) { return json(await fastify.inject({ method: 'PUT', url, payload })); },
  };
}

function json(res) {
  let body;
  try { body = JSON.parse(res.body); } catch { body = res.body; }
  return { status: res.statusCode, body };
}

/** Count occurrences of a substring — used to prove unrelated tags survived. */
export function countOccurrences(haystack, needle) {
  return haystack.split(needle).length - 1;
}
