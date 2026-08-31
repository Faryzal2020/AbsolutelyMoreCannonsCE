import fs from 'node:fs';
import path from 'node:path';
import { abs, rel, DEFS_DIR } from '../paths.js';
import { FIELD_BY_KEY, TOGGLE_BY_KEY, serialise } from '../xml/fieldMap.js';
import { AMMO_FIELD_BY_KEY } from '../xml/fieldMapAmmo.js';
import {
  setValue, setListValue, setCostList, addComponent, removeComponent,
  SKIP_NO_CHANGE,
} from '../xml/editor.js';
import { computeDiffs } from './diffService.js';
import { runExtraction } from './extractService.js';

/** Copy file -> file.xml.bak, but never overwrite an existing backup: the
 *  first one holds the pristine original that rollback must restore. */
function ensureBackup(absPath) {
  const bak = `${absPath}.bak`;
  if (fs.existsSync(bak)) return { created: false, path: rel(bak) };
  fs.copyFileSync(absPath, bak);
  return { created: true, path: rel(bak) };
}

/** Apply one change descriptor to an in-memory file body. */
export function applyChange(content, change) {
  if (change.kind === 'costList') {
    return setCostList(content, change.defName, change.to);
  }

  if (change.kind === 'component') {
    const toggle = TOGGLE_BY_KEY.get(change.key);
    if (!toggle) return { content, changed: false, reason: 'unknown-toggle' };
    return change.action === 'add'
      ? addComponent(content, change.defName, toggle.container, toggle.cls, toggle.body)
      : removeComponent(content, change.defName, toggle.container, toggle.cls);
  }

  if (change.kind === 'field') {
    const field = FIELD_BY_KEY.get(change.key) || AMMO_FIELD_BY_KEY.get(change.key);
    if (!field) return { content, changed: false, reason: 'unknown-field' };
    const value = serialise(field.type, change.to);
    return field.type === 'list'
      ? setListValue(content, change.defName, field.path, value)
      : setValue(content, change.defName, field.path, value);
  }

  return { content, changed: false, reason: change.kind };
}

/**
 * Commit pending database changes to the XML files on disk.
 * @param {object} db
 * @param {{defNames?:string[]|null, dryRun?:boolean}} options
 */
export function runInjection(db, { defNames = null, dryRun = false } = {}) {
  const { diffs } = computeDiffs(db, defNames);

  // One pass per file: read once, apply every change, write once.
  const byFile = new Map();
  for (const d of diffs) {
    for (const c of d.changes) {
      if (!c.filePath) continue;
      if (!byFile.has(c.filePath)) byFile.set(c.filePath, []);
      byFile.get(c.filePath).push(c);
    }
  }

  const applied = [];
  const skipped = [];
  const backups = [];
  const touchedFiles = [];

  for (const [filePath, changes] of byFile) {
    const absPath = abs(filePath);
    if (!fs.existsSync(absPath)) {
      changes.forEach((c) => skipped.push({ ...c, reason: 'file-missing' }));
      continue;
    }

    const before = fs.readFileSync(absPath, 'utf8');
    let content = before;
    const localApplied = [];

    for (const change of changes) {
      const result = applyChange(content, change);
      if (result.changed) {
        content = result.content;
        localApplied.push(change);
      } else {
        (result.reason === SKIP_NO_CHANGE ? applied : skipped).push({ ...change, reason: result.reason });
      }
    }

    if (content === before) {
      applied.push(...localApplied);
      continue;
    }

    if (!dryRun) {
      const bak = ensureBackup(absPath);
      if (bak.created) backups.push(bak.path);
      fs.writeFileSync(absPath, content, 'utf8');
    }
    touchedFiles.push(filePath);
    applied.push(...localApplied);
  }

  let reExtract = null;
  if (!dryRun && touchedFiles.length) {
    // Re-ingest so data_json / original_json agree with what is now on disk.
    reExtract = runExtraction(db);
  }

  return {
    ok: true,
    dryRun,
    defCount: diffs.length,
    filesWritten: touchedFiles.length,
    files: touchedFiles,
    backups,
    appliedCount: applied.length,
    skippedCount: skipped.length,
    applied: applied.map(({ defName, key, label, from, to, xmlPath, filePath }) =>
      ({ defName, key, label, from, to, xmlPath, filePath })),
    skipped: skipped.map(({ defName, key, label, reason, xmlPath, filePath }) =>
      ({ defName, key, label, reason, xmlPath, filePath })),
    reExtract: reExtract && reExtract.counts,
  };
}

function findBackups(dir) {
  const out = [];
  if (!fs.existsSync(dir)) return out;
  for (const entry of fs.readdirSync(dir, { withFileTypes: true })) {
    const full = path.join(dir, entry.name);
    if (entry.isDirectory()) out.push(...findBackups(full));
    else if (entry.isFile() && entry.name.endsWith('.xml.bak')) out.push(full);
  }
  return out.sort();
}

/**
 * Restore every *.xml.bak back over its *.xml and drop the backup.
 * @param {{keepBackups?:boolean}} options
 */
export function runRollback(db, { keepBackups = false, dryRun = false } = {}) {
  const restored = [];
  for (const bak of findBackups(DEFS_DIR)) {
    const target = bak.slice(0, -'.bak'.length);
    if (!dryRun) {
      fs.copyFileSync(bak, target);
      if (!keepBackups) fs.rmSync(bak);
    }
    restored.push(rel(target));
  }

  const reExtract = !dryRun && restored.length ? runExtraction(db) : null;
  return {
    ok: true,
    dryRun,
    restoredCount: restored.length,
    restored,
    reExtract: reExtract && reExtract.counts,
  };
}

export function listBackups() {
  return findBackups(DEFS_DIR).map((b) => ({
    backup: rel(b),
    target: rel(b.slice(0, -'.bak'.length)),
    size: fs.statSync(b).size,
    modifiedAt: fs.statSync(b).mtime.toISOString(),
  }));
}
