import path from 'node:path';
import fs from 'node:fs';
import Fastify from 'fastify';
import fastifyStatic from '@fastify/static';
import { getDb } from './db.js';
import { PUBLIC_DIR, DB_PATH, MOD_ROOT, BUILDINGS_DIR } from './paths.js';
import apiRoutes from './routes/api.js';

/**
 * Build the Fastify instance. Exported separately from server.js so the test
 * suite can drive the exact same app through `fastify.inject()` with no socket.
 */
export async function buildApp({ logger = false, dbFile = DB_PATH } = {}) {
  const db = await getDb(dbFile);

  const fastify = Fastify({
    logger,
    bodyLimit: 32 * 1024 * 1024, // snapshot restore payloads can be large
  });

  fastify.decorate('db', db);
  fastify.decorate('config', { modRoot: MOD_ROOT, buildingsDir: BUILDINGS_DIR, dbFile });

  await fastify.register(apiRoutes, { db });

  if (fs.existsSync(PUBLIC_DIR)) {
    await fastify.register(fastifyStatic, { root: PUBLIC_DIR, index: ['index.html'] });
    // SPA fallback: anything that is not /api/* serves the dashboard shell.
    fastify.setNotFoundHandler((request, reply) => {
      if (request.url.startsWith('/api/')) {
        return reply.code(404).send({ ok: false, error: `No such endpoint: ${request.url}` });
      }
      return reply.sendFile('index.html');
    });
  } else {
    fastify.get('/', async (_request, reply) => reply.type('text/html').send(
      '<h1>UI not built</h1><p>Run <code>npm run build</code>, or use <code>npm start</code> which builds automatically.</p>',
    ));
  }

  return { fastify, db };
}

export { path };
