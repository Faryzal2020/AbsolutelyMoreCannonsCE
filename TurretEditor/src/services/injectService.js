import fs from 'node:fs';
import path from 'node:path';
import { abs, rel, DEFS_DIR } from '../paths.js';
import { FIELD_BY_KEY, TOGGLE_BY_KEY, serialise } from '../xml/fieldMap.js';
import { AMMO_FIELD_BY_KEY } from '../xml/fieldMapAmmo.js';
import {
  setValue, setListValue, setCostList, addComponent, removeComponent,
  SKIP_NO_CHANGE, removeValue,
} from '../xml/editor.js';
import { getMeta, setMeta } from '../db.js';
import { checksum } from '../xml/defIndex.js';
import { computeDiffs } from './diffService.js';
import { reextractPreservingEdits } from './syncService.js';

/*
 * Backup ledger. Every .xml.bak this tool writes is recorded in `meta` with the
 * checksum of the content the tool last wrote over its target. That checksum
 * is what makes a backup trustworthy:
 *
 *   target still matches it  -> nobody touched the file since our last write,
 *                               so the backup is the pristine pre-session file
 *                               and rollback may restore it.
 *   target does not match    -> the file moved on (hand edit, git pull, an
 *                               older backup left behind by other tooling),
 *                               so restoring the backup would silently revert
 *                               that newer work.
 */
const LEDGER_KEY = 'backup_ledger';

function readLedger(db) {
  try { return JSON.parse(getMeta(db, LEDGER_KEY) || '{}'); } catch { return {}; }
}
function writeLedger(db, ledger) {
  setMeta(db, LEDGER_KEY, JSON.stringify(ledger));
}

/** Whether a backup is safe to restore, and why not when it isn't. */
function backupStatus(ledger, bakRel, targetAbs) {
  const entry = ledger[bakRel];
  if (!entry) return { trusted: false, reason: 'untracked: not written by this editor database' };
  if (!fs.existsSync(targetAbs)) return { trusted: false, reason: 'target file missing' };
  if (checksum(fs.readFileSync(targetAbs, 'utf8')) !== entry.writtenChecksum) {
    return { trusted: false, reason: 'target changed since the editor last wrote it' };
  }
  return { trusted: true, reason: null };
}

/**
 * Make sure file.xml.bak holds the state from before this editing session.
 * An existing backup is kept only while it is still trusted; a stale or
 * untracked one is refreshed from the current file, which is the real
 * pre-injection state.
 */
function ensureBackup(ledger, absPath) {
  const bak = `${absPath}.bak`;
  const bakRel = rel(bak);
  const exists = fs.existsSync(bak);
  if (exists && backupStatus(ledger, bakRel, absPath).trusted) return { created: false, path: bakRel };
  fs.copyFileSync(absPath, bak);
  ledger[bakRel] = { createdAt: new Date().toISOString(), writtenChecksum: null };
  return { created: !exists, refreshed: exists, path: bakRel };
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
    if (field.type === 'list') {
      const items = Array.isArray(change.to)
        ? change.to
        : String(change.to ?? '').split(',').map((s) => s.trim()).filter(Boolean);
      return setListValue(content, change.defName, field.path, items);
    }
    // Optional tags whose empty value RimWorld cannot parse (e.g. a Vector3) are removed instead of emptied.
    if (field.removeWhenEmpty && (change.to === null || change.to === undefined || String(change.to).trim() === '')) {
      return removeValue(content, change.defName, field.path);
    }
    const value = serialise(field.type, change.to);
    return setValue(content, change.defName, field.path, value);
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
  const refreshedBackups = [];
  const touchedFiles = [];
  const ledger = readLedger(db);

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
      const bak = ensureBackup(ledger, absPath);
      if (bak.created) backups.push(bak.path);
      if (bak.refreshed) refreshedBackups.push(bak.path);
      fs.writeFileSync(absPath, content, 'utf8');
      ledger[bak.path].writtenChecksum = checksum(content);
    }
    touchedFiles.push(filePath);
    applied.push(...localApplied);
  }

  let reExtract = null;
  if (!dryRun && touchedFiles.length) {
    writeLedger(db, ledger);
    // Re-ingest so data_json / original_json agree with what is now on disk.
    // Edits outside a `defNames` filter are still pending and must survive.
    reExtract = reextractPreservingEdits(db);
  }

  return {
    ok: true,
    dryRun,
    defCount: diffs.length,
    filesWritten: touchedFiles.length,
    files: touchedFiles,
    backups,
    refreshedBackups,
    appliedCount: applied.length,
    skippedCount: skipped.length,
    applied: applied.map(({ defName, key, label, from, to, xmlPath, filePath }) =>
      ({ defName, key, label, from, to, xmlPath, filePath })),
    skipped: skipped.map(({ defName, key, label, reason, xmlPath, filePath }) =>
      ({ defName, key, label, reason, xmlPath, filePath })),
    reExtract: reExtract && reExtract.counts,
    stillPending: reExtract ? reExtract.preserved : null,
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
 * Restore *.xml.bak files over their *.xml.
 *
 * Only backups the editor itself wrote, whose target has not changed since,
 * are restored. Anything else would revert work done after the backup was
 * taken, so it is reported under `skipped` unless `force` is set.
 * @param {{keepBackups?:boolean, dryRun?:boolean, force?:boolean}} options
 */
export function runRollback(db, { keepBackups = false, dryRun = false, force = false } = {}) {
  const ledger = readLedger(db);
  const restored = [];
  const skipped = [];
  for (const bak of findBackups(DEFS_DIR)) {
    const target = bak.slice(0, -'.bak'.length);
    const bakRel = rel(bak);
    const status = backupStatus(ledger, bakRel, target);
    if (!status.trusted && !force) {
      skipped.push({ file: rel(target), backup: bakRel, reason: status.reason });
      continue;
    }
    if (!dryRun) {
      fs.copyFileSync(bak, target);
      if (keepBackups) {
        ledger[bakRel] = { ...(ledger[bakRel] || { createdAt: new Date().toISOString() }),
          writtenChecksum: checksum(fs.readFileSync(target, 'utf8')) };
      } else {
        fs.rmSync(bak);
        delete ledger[bakRel];
      }
    }
    restored.push(rel(target));
  }

  if (!dryRun && restored.length) writeLedger(db, ledger);
  const reExtract = !dryRun && restored.length ? reextractPreservingEdits(db) : null;
  return {
    ok: true,
    dryRun,
    restoredCount: restored.length,
    restored,
    skippedCount: skipped.length,
    skipped,
    reExtract: reExtract && reExtract.counts,
  };
}

export function listBackups(db = null) {
  const ledger = db ? readLedger(db) : null;
  return findBackups(DEFS_DIR).map((b) => {
    const target = b.slice(0, -'.bak'.length);
    const status = ledger ? backupStatus(ledger, rel(b), target) : null;
    return {
      backup: rel(b),
      target: rel(target),
      size: fs.statSync(b).size,
      modifiedAt: fs.statSync(b).mtime.toISOString(),
      ...(status ? { restorable: status.trusted, ...(status.reason ? { reason: status.reason } : {}) } : {}),
    };
  });
}
