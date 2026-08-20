import fs from 'node:fs';
import { abs } from '../paths.js';
import { auditTurret } from '../xml/extract.js';
import { upsertTurret } from './persist.js';
import { knownDefPredicate } from './extractService.js';
import { defBlockText } from '../xml/editor.js';
import { diffTurret } from './diffService.js';
import { applyChange } from './injectService.js';
import { diskSnapshot } from './extractService.js';

/** Recursive merge that replaces arrays wholesale (costs are edited as a unit). */
export function deepMerge(base, patch) {
  if (Array.isArray(patch) || patch === null || typeof patch !== 'object') return patch;
  const out = { ...base };
  for (const [k, v] of Object.entries(patch)) {
    out[k] = (v && typeof v === 'object' && !Array.isArray(v) && base && typeof base[k] === 'object' && base[k] !== null && !Array.isArray(base[k]))
      ? deepMerge(base[k], v)
      : v;
  }
  return out;
}

function rowToTurret(row) {
  const t = JSON.parse(row.data_json);
  t.modified = row.modified === 1;
  t.fileId = row.file_id;
  return t;
}

export function listTurrets(db) {
  return db.all('SELECT def_name, file_id, modified, data_json FROM turrets ORDER BY category, label')
    .map(rowToTurret);
}

export function getTurret(db, defName) {
  const row = db.get('SELECT def_name, file_id, modified, data_json, original_json FROM turrets WHERE def_name = ?', defName);
  return row ? rowToTurret(row) : null;
}

export function getOriginal(db, defName) {
  const row = db.get('SELECT original_json FROM turrets WHERE def_name = ?', defName);
  return row ? JSON.parse(row.original_json) : null;
}

/** Fields the editor must never rewrite — they identify the record itself. */
const IMMUTABLE = ['defName', 'filePath', 'weaponFilePath', 'parentName', 'ancestry', 'category', 'modified', 'fileId', 'warnings'];

/**
 * Apply a partial update to a turret. Returns the stored record, or null when
 * the def is unknown.
 */
export function updateTurret(db, defName, patch) {
  const row = db.get('SELECT file_id, data_json, original_json FROM turrets WHERE def_name = ?', defName);
  if (!row) return null;

  const current = JSON.parse(row.data_json);
  const clean = { ...patch };
  for (const key of IMMUTABLE) delete clean[key];

  const next = deepMerge(current, clean);
  next.defName = current.defName;
  next.filePath = current.filePath;
  next.weaponFilePath = current.weaponFilePath;
  next.parentName = current.parentName;
  next.ancestry = current.ancestry;
  next.category = current.category;
  next.warnings = auditTurret(next, knownDefPredicate(db));

  const original = JSON.parse(row.original_json);
  const modified = JSON.stringify(stripVolatile(next)) !== JSON.stringify(stripVolatile(original));

  upsertTurret(db, next, {
    fileId: row.file_id,
    modified,
    originalJson: row.original_json,
  });

  next.modified = modified;
  return next;
}

/** Warnings are derived, so they must not influence the modified flag. */
function stripVolatile(t) {
  const { warnings, modified, fileId, ...rest } = t;
  return rest;
}

/** Discard edits for one turret, restoring the state captured at extraction. */
export function revertTurret(db, defName) {
  const row = db.get('SELECT file_id, original_json FROM turrets WHERE def_name = ?', defName);
  if (!row) return null;
  const original = JSON.parse(row.original_json);
  upsertTurret(db, original, { fileId: row.file_id, modified: false, originalJson: row.original_json });
  return original;
}

export function revertAll(db) {
  const rows = db.all('SELECT def_name FROM turrets WHERE modified = 1');
  rows.forEach((r) => revertTurret(db, r.def_name));
  return rows.length;
}

/** Replace the whole live state — used by snapshot restore. */
export function restoreState(db, turrets) {
  let count = 0;
  const write = db.transaction(() => {
    for (const t of turrets) {
      const row = db.get('SELECT file_id, original_json FROM turrets WHERE def_name = ?', t.defName);
      if (!row) continue;
      const next = { ...t, warnings: auditTurret(t, knownDefPredicate(db)) };
      const modified = JSON.stringify(stripVolatile(next)) !== JSON.stringify(stripVolatile(JSON.parse(row.original_json)));
      upsertTurret(db, next, { fileId: row.file_id, modified, originalJson: row.original_json });
      count++;
    }
  });
  write();
  return count;
}

/**
 * Side-by-side XML for the diff viewer: the raw block on disk, and the same
 * block with the pending edits applied in memory (nothing is written).
 */
export function previewXml(db, defName) {
  const turret = getTurret(db, defName);
  if (!turret) return null;

  const disk = diskSnapshot();
  const changes = diffTurret(turret, disk.get(defName));

  const targets = [{ role: 'building', defName: turret.defName, filePath: turret.filePath }];
  if (turret.weaponDefName && turret.weaponFilePath) {
    targets.push({ role: 'weapon', defName: turret.weaponDefName, filePath: turret.weaponFilePath });
  }

  const panes = [];
  for (const target of targets) {
    const absPath = abs(target.filePath);
    if (!fs.existsSync(absPath)) continue;
    const fileBody = fs.readFileSync(absPath, 'utf8');
    const original = defBlockText(fileBody, target.defName);
    if (original === null) continue;

    // Replay this def's changes against the real file, then slice the block out.
    let mutated = fileBody;
    for (const change of changes.filter((c) => c.defName === target.defName)) {
      const result = applyChange(mutated, change);
      if (result.changed) mutated = result.content;
    }

    panes.push({
      role: target.role,
      defName: target.defName,
      filePath: target.filePath,
      original,
      updated: defBlockText(mutated, target.defName) ?? original,
    });
  }

  return { defName, label: turret.label, changeCount: changes.length, changes, panes };
}
