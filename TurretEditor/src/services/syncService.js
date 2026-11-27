/**
 * Keeps the database in step with the XML on disk without losing pending edits.
 *
 * The database is a snapshot. When the XML changes underneath it — a hand edit,
 * a git pull, another tool — every record read from it is stale, and worse, a
 * pending edit's record still carries the old values of every *other* field.
 * `runExtraction` alone cannot fix that because it wipes the tables, pending
 * edits included.
 *
 * `reextractPreservingEdits` re-reads the XML and then replays only what the
 * user actually changed (live value != extracted original) on top of the fresh
 * records. `syncIfStale` does that automatically when a fingerprint of the def
 * files no longer matches the one recorded at extraction.
 */

export { diskChecksums };

import { FIELDS, COMPONENT_TOGGLES, getPath, setPath } from '../xml/fieldMap.js';
import { AMMO_FIELDS } from '../xml/fieldMapAmmo.js';
import { getMeta } from '../db.js';
import { runExtraction, diskChecksums, CHECKSUMS_KEY } from './extractService.js';
import { sameValue, costsEqual } from './diffService.js';
import { updateTurret } from './turretService.js';
import { updateAmmo } from './ammoService.js';

/**
 * Files that differ from the state recorded at the last extraction.
 * A database extracted before fingerprints existed reports `unknown: true`.
 */
export function diskDrift(db, current = diskChecksums()) {
  let recorded = null;
  try { recorded = JSON.parse(getMeta(db, CHECKSUMS_KEY) || 'null'); } catch { recorded = null; }
  if (!recorded) return { stale: true, unknown: true, changed: [], added: [], removed: [] };

  const changed = [];
  const added = [];
  for (const [file, sum] of Object.entries(current)) {
    if (!(file in recorded)) added.push(file);
    else if (recorded[file] !== sum) changed.push(file);
  }
  const removed = Object.keys(recorded).filter((file) => !(file in current));
  return {
    stale: changed.length + added.length + removed.length > 0,
    unknown: false,
    changed,
    added,
    removed,
  };
}

/** Injectable edits on a turret: keys whose live value differs from the extracted original. */
export function turretEdits(data, original) {
  const edits = [];
  for (const toggle of COMPONENT_TOGGLES) {
    const now = Boolean(getPath(data, toggle.key));
    if (now !== Boolean(getPath(original, toggle.key))) edits.push({ key: toggle.key, value: now });
  }
  for (const field of FIELDS) {
    const now = getPath(data, field.key);
    if (!sameValue(now, getPath(original, field.key), field.type)) edits.push({ key: field.key, value: now });
  }
  if (!costsEqual(data.costs, original.costs)) edits.push({ key: 'costs', value: data.costs });
  return edits;
}

export function ammoEdits(data, original) {
  const edits = [];
  for (const field of AMMO_FIELDS) {
    const now = getPath(data, field.key);
    if (!sameValue(now, getPath(original, field.key), field.type)) edits.push({ key: field.key, value: now });
  }
  return edits;
}

const toPatch = (edits) => {
  const patch = {};
  for (const { key, value } of edits) setPath(patch, key, value);
  return patch;
};

/**
 * A replayed edit conflicts when the same key also changed on disk since the
 * edit was made, to something other than the edit itself. The edit wins; the
 * conflict is reported so the caller can tell the user.
 */
function conflictsFor(defName, edits, oldOriginal, freshOriginal, typeOf) {
  const out = [];
  for (const { key, value } of edits) {
    const type = typeOf(key);
    const base = key === 'costs' ? oldOriginal.costs : getPath(oldOriginal, key);
    const disk = key === 'costs' ? freshOriginal.costs : getPath(freshOriginal, key);
    const diskMoved = key === 'costs' ? !costsEqual(base, disk) : !sameValue(base, disk, type);
    const agrees = key === 'costs' ? costsEqual(value, disk) : sameValue(value, disk, type);
    if (diskMoved && !agrees) out.push({ defName, key, base, disk, pending: value });
  }
  return out;
}

const FIELD_TYPE = new Map(FIELDS.map((f) => [f.key, f.type]));
const AMMO_TYPE = new Map(AMMO_FIELDS.map((f) => [f.key, f.type]));

/**
 * Re-read the XML into the database, then replay every pending edit on top.
 * Use this instead of `runExtraction` anywhere edits may be pending.
 */
export function reextractPreservingEdits(db) {
  const pendingTurrets = db.all('SELECT def_name, data_json, original_json FROM turrets WHERE modified = 1')
    .map((r) => {
      const data = JSON.parse(r.data_json);
      const original = JSON.parse(r.original_json);
      return { defName: r.def_name, original, edits: turretEdits(data, original) };
    })
    .filter((p) => p.edits.length);
  const pendingAmmo = db.all('SELECT def_name, data_json, original_json FROM ammo WHERE modified = 1')
    .map((r) => {
      const data = JSON.parse(r.data_json);
      const original = JSON.parse(r.original_json);
      return { defName: r.def_name, original, edits: ammoEdits(data, original) };
    })
    .filter((p) => p.edits.length);

  const result = runExtraction(db);

  const conflicts = [];
  const lost = [];
  let turrets = 0;
  let ammo = 0;

  db.transaction(() => {
    for (const p of pendingTurrets) {
      const row = db.get('SELECT original_json FROM turrets WHERE def_name = ?', p.defName);
      if (!row) { lost.push({ defName: p.defName, reason: 'def no longer on disk', edits: p.edits }); continue; }
      conflicts.push(...conflictsFor(p.defName, p.edits, p.original, JSON.parse(row.original_json),
        (k) => FIELD_TYPE.get(k) || 'bool'));
      // An edit that now matches disk (just injected, or made by hand too) is
      // no longer pending, so only records still modified are counted.
      if (updateTurret(db, p.defName, toPatch(p.edits))?.modified) turrets++;
    }
    for (const p of pendingAmmo) {
      const row = db.get('SELECT original_json FROM ammo WHERE def_name = ?', p.defName);
      if (!row) { lost.push({ defName: p.defName, reason: 'def no longer on disk', edits: p.edits }); continue; }
      conflicts.push(...conflictsFor(p.defName, p.edits, p.original, JSON.parse(row.original_json),
        (k) => AMMO_TYPE.get(k)));
      if (updateAmmo(db, p.defName, toPatch(p.edits))?.modified) ammo++;
    }
  })();

  return { ...result, preserved: { turrets, ammo }, conflicts, lost };
}

/**
 * Re-extract only when the XML changed since the last extraction. Cheap when
 * nothing changed: one sha1 per def file, no parsing.
 * @returns {null | {reason:string, files:object, extraction:object}}
 */
export function syncIfStale(db) {
  const current = diskChecksums();
  const drift = diskDrift(db, current);
  if (!drift.stale) return null;

  const extraction = reextractPreservingEdits(db);
  return {
    reason: drift.unknown ? 'no disk fingerprint recorded' : 'XML changed on disk since last extraction',
    files: { changed: drift.changed, added: drift.added, removed: drift.removed },
    preserved: extraction.preserved,
    conflicts: extraction.conflicts,
    lost: extraction.lost,
    counts: extraction.counts,
  };
}
