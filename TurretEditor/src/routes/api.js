import fs from 'node:fs';
import path from 'node:path';
import { TEXTURES_DIR, MUZZLE_FLASH_ASSETS_DIR } from '../paths.js';
import { MUZZLE_FLASH_DEFS } from '../xml/extract.js';
import { runExtraction } from '../services/extractService.js';
import { computeDiffs } from '../services/diffService.js';
import { runInjection, runRollback, listBackups } from '../services/injectService.js';
import {
  listTurrets, getTurret, updateTurret, revertTurret, revertAll, restoreState, previewXml,
} from '../services/turretService.js';
import {
  listAmmo, getAmmo, updateAmmo, revertAmmo, revertAllAmmo,
} from '../services/ammoService.js';
import { metrics, auditReport } from '../services/auditService.js';
import { buildSheet, toCsv, toWorkbook, describeColumns, exportMeta, DATASETS } from '../services/exportService.js';
import { FIELDS, COMPONENT_TOGGLES } from '../xml/fieldMap.js';
import { openApiSpec } from './openapi.js';

const notFound = (reply, message) => reply.code(404).send({ ok: false, error: message });

/**
 * Every route is synchronous, deterministic and JSON-only, so a CLI or agent
 * can drive the whole extract -> edit -> inject -> rollback loop without a browser.
 */
export default async function apiRoutes(fastify, { db }) {
  fastify.get('/api/health', async () => ({
    ok: true,
    driver: db.kind,
    turrets: db.get('SELECT COUNT(*) AS n FROM turrets').n,
    ammo: db.get('SELECT COUNT(*) AS n FROM ammo').n,
  }));

  fastify.get('/api/openapi.json', async () => openApiSpec());

  // --- extraction -------------------------------------------------------
  fastify.post('/api/extract', async () => runExtraction(db));

  // --- reading turrets --------------------------------------------------
  fastify.get('/api/turrets', async (request) => {
    const { category, search, modified, warnings } = request.query;
    let turrets = listTurrets(db);

    if (category && category !== 'all') {
      turrets = turrets.filter((t) => t.category.toLowerCase() === String(category).toLowerCase());
    }
    if (modified === 'true') turrets = turrets.filter((t) => t.modified);
    if (warnings === 'true') turrets = turrets.filter((t) => t.warnings.length > 0);
    if (search) {
      const q = String(search).toLowerCase();
      turrets = turrets.filter((t) => [
        t.defName, t.label, t.category, t.weaponDefName, t.ammo.ammoSet,
        t.ballistics.verbClass, t.comps.swapAltDef, t.parentName,
      ].some((v) => String(v || '').toLowerCase().includes(q)));
    }

    return { ok: true, count: turrets.length, turrets };
  });

  fastify.get('/api/turrets/:defName', async (request, reply) => {
    const turret = getTurret(db, request.params.defName);
    return turret ? { ok: true, turret } : notFound(reply, `Unknown def: ${request.params.defName}`);
  });

  fastify.put('/api/turrets/:defName', async (request, reply) => {
    const patch = request.body;
    if (!patch || typeof patch !== 'object' || Array.isArray(patch)) {
      return reply.code(400).send({ ok: false, error: 'Request body must be a JSON object.' });
    }
    const turret = updateTurret(db, request.params.defName, patch);
    return turret ? { ok: true, turret } : notFound(reply, `Unknown def: ${request.params.defName}`);
  });

  fastify.post('/api/turrets/:defName/revert', async (request, reply) => {
    const turret = revertTurret(db, request.params.defName);
    return turret ? { ok: true, turret } : notFound(reply, `Unknown def: ${request.params.defName}`);
  });

  fastify.post('/api/revert-all', async () => ({ ok: true, reverted: revertAll(db) }));

  // --- ammunition endpoints ---------------------------------------------
  fastify.get('/api/ammo', async (request) => {
    const { ammoFamily, search, modified, mode } = request.query;
    let list = listAmmo(db);

    if (ammoFamily && ammoFamily !== 'all') {
      list = list.filter((a) => a.ammoFamily.toLowerCase() === String(ammoFamily).toLowerCase());
    }
    if (modified === 'true') list = list.filter((a) => a.modified);
    if (mode === 'direct') list = list.filter((a) => a.hasDirectMode && !a.hasIndirectMode);
    if (mode === 'indirect') list = list.filter((a) => a.hasIndirectMode && !a.hasDirectMode);
    if (mode === 'dual') list = list.filter((a) => a.hasDirectMode && a.hasIndirectMode);
    if (search) {
      const q = String(search).toLowerCase();
      list = list.filter((a) => [
        a.defName, a.label, a.ammoFamily, a.ammoClass, a.ammoSetName, a.indirectAmmoSetName,
        a.directBulletDef, a.indirectBulletDef, a.filePath,
      ].some((v) => String(v || '').toLowerCase().includes(q)));
    }

    return { ok: true, count: list.length, ammo: list };
  });

  fastify.get('/api/ammo/:defName', async (request, reply) => {
    const item = getAmmo(db, request.params.defName);
    return item ? { ok: true, ammo: item } : notFound(reply, `Unknown ammo def: ${request.params.defName}`);
  });

  fastify.put('/api/ammo/:defName', async (request, reply) => {
    const patch = request.body;
    if (!patch || typeof patch !== 'object' || Array.isArray(patch)) {
      return reply.code(400).send({ ok: false, error: 'Request body must be a JSON object.' });
    }
    const item = updateAmmo(db, request.params.defName, patch);
    return item ? { ok: true, ammo: item } : notFound(reply, `Unknown ammo def: ${request.params.defName}`);
  });

  fastify.post('/api/ammo/:defName/revert', async (request, reply) => {
    const item = revertAmmo(db, request.params.defName);
    return item ? { ok: true, ammo: item } : notFound(reply, `Unknown ammo def: ${request.params.defName}`);
  });

  fastify.post('/api/revert-all-ammo', async () => ({ ok: true, reverted: revertAllAmmo(db) }));

  fastify.post('/api/restore', async (request, reply) => {
    const turrets = request.body?.turrets;
    if (!Array.isArray(turrets)) {
      return reply.code(400).send({ ok: false, error: 'Body must be { turrets: [...] }.' });
    }
    return { ok: true, restored: restoreState(db, turrets) };
  });

  // --- diff / inject / rollback ----------------------------------------
  fastify.get('/api/diffs', async (request) => {
    const only = request.query.defName ? [request.query.defName] : null;
    return computeDiffs(db, only);
  });

  fastify.get('/api/diffs/:defName/xml', async (request, reply) => {
    const preview = previewXml(db, request.params.defName);
    return preview ? { ok: true, ...preview } : notFound(reply, `Unknown def: ${request.params.defName}`);
  });

  fastify.post('/api/inject', async (request) => runInjection(db, {
    defNames: Array.isArray(request.body?.defNames) ? request.body.defNames : null,
    dryRun: request.body?.dryRun === true,
  }));

  fastify.post('/api/rollback', async (request) => runRollback(db, {
    keepBackups: request.body?.keepBackups === true,
    dryRun: request.body?.dryRun === true,
    force: request.body?.force === true,
  }));

  fastify.get('/api/backups', async () => ({ ok: true, backups: listBackups(db) }));

  // --- reporting --------------------------------------------------------
  fastify.get('/api/metrics', async () => ({ ok: true, ...metrics(db) }));
  fastify.get('/api/audit', async () => auditReport(db));

  /**
   * Spreadsheet export. `format=csv` (default) returns one dataset; `format=xlsx`
   * returns every dataset as sheets in a single workbook.
   */
  fastify.get('/api/export', async (request, reply) => {
    const format = String(request.query.format || 'csv').toLowerCase();
    const scope = request.query.scope === 'modified' ? 'modified' : 'all';
    const dataset = String(request.query.dataset || 'turrets').toLowerCase();

    if (format === 'xlsx' || format === 'xls') {
      const buffer = toWorkbook(db, { scope });
      return reply
        .header('Content-Type', 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet')
        .header('Content-Disposition', `attachment; filename="amc_turret_matrix${scope === 'modified' ? '_modified' : ''}.xlsx"`)
        .send(buffer);
    }

    if (format !== 'csv') {
      return reply.code(400).send({ ok: false, error: `Unsupported format "${format}". Use csv or xlsx.` });
    }
    if (!DATASETS[dataset]) {
      return reply.code(400).send({
        ok: false,
        error: `Unknown dataset "${dataset}". Use one of: ${Object.keys(DATASETS).join(', ')}.`,
      });
    }

    const sheet = buildSheet(db, dataset, { scope });
    return reply
      .header('Content-Type', 'text/csv; charset=utf-8')
      .header('Content-Disposition', `attachment; filename="${DATASETS[dataset].file}${scope === 'modified' ? '_modified' : ''}.csv"`)
      .send(toCsv(sheet));
  });

  /** Column catalogue behind the export dialog and for agent discovery. */
  fastify.get('/api/export/columns', async (request) => {
    const dataset = String(request.query.dataset || 'turrets').toLowerCase();
    return {
      ok: true,
      ...exportMeta(db),
      dataset,
      columns: describeColumns(dataset),
    };
  });

  /** Field catalogue — lets an agent discover every editable path at runtime. */
  fastify.get('/api/schema', async () => ({
    ok: true,
    fields: FIELDS.map((f) => ({
      key: f.key, label: f.label, doc: f.doc, type: f.type, gate: f.gate || null,
      xmlPath: f.path.map((s) => (s.cls ? `li[Class*=${s.cls}]` : s.tag)).join(' > '),
    })),
    componentToggles: COMPONENT_TOGGLES.map((t) => ({
      key: t.key, label: t.label, container: t.container, cls: t.cls,
    })),
  }));

  /**
   * Serve a texture from Common/Textures by RimWorld texPath.
   * Supports Graphic_Single file paths and Graphic_Random / Graphic_Collection directory paths.
   */
  fastify.get('/api/texture', async (request, reply) => {
    const rawPath = String(request.query.texPath || '').trim();
    const frame = parseInt(request.query.frame ?? '0', 10) || 0;
    if (!rawPath || rawPath.includes('..')) {
      return reply.code(400).send({ ok: false, error: 'Invalid texPath' });
    }

    const baseTarget = path.resolve(TEXTURES_DIR, ...rawPath.split('/'));
    const texturesRoot = path.resolve(TEXTURES_DIR);
    if (!baseTarget.startsWith(texturesRoot)) {
      return reply.code(403).send({ ok: false, error: 'Forbidden path' });
    }

    let resolvedFile = null;
    if (fs.existsSync(`${baseTarget}.png`)) {
      resolvedFile = `${baseTarget}.png`;
    } else if (fs.existsSync(`${baseTarget}.jpg`)) {
      resolvedFile = `${baseTarget}.jpg`;
    } else if (fs.existsSync(baseTarget) && fs.statSync(baseTarget).isDirectory()) {
      const files = fs.readdirSync(baseTarget)
        .filter((f) => /\.(png|jpg)$/i.test(f))
        .sort();
      if (files.length > 0) {
        const picked = files[Math.min(Math.max(0, frame), files.length - 1)];
        resolvedFile = path.join(baseTarget, picked);
      }
    }

    if (!resolvedFile || !fs.existsSync(resolvedFile)) {
      return reply.code(404).send({ ok: false, error: `Texture not found: ${rawPath}` });
    }

    const ext = path.extname(resolvedFile).toLowerCase();
    const mime = ext === '.jpg' || ext === '.jpeg' ? 'image/jpeg' : 'image/png';
    return reply
      .header('Content-Type', mime)
      .header('Cache-Control', 'public, max-age=300')
      .send(fs.readFileSync(resolvedFile));
  });

  /**
   * Serve extracted Muzzle Flash mod sprite sheets by MuzzleFlashDef name or filename.
   */
  fastify.get('/api/muzzle-flash-texture/:defOrFile', async (request, reply) => {
    const param = String(request.params.defOrFile || '').trim();
    const defInfo = MUZZLE_FLASH_DEFS[param];
    const fileName = defInfo ? defInfo.texture : param;

    if (!fileName || fileName.includes('..') || fileName.includes('/') || fileName.includes('\\')) {
      return reply.code(400).send({ ok: false, error: 'Invalid muzzle flash texture identifier' });
    }

    const absFile = path.join(MUZZLE_FLASH_ASSETS_DIR, fileName);
    if (!fs.existsSync(absFile)) {
      return reply.code(404).send({ ok: false, error: `Muzzle flash texture not found: ${fileName}` });
    }

    return reply
      .header('Content-Type', 'image/png')
      .header('Cache-Control', 'public, max-age=3600')
      .send(fs.readFileSync(absFile));
  });
}
