import { extractAll } from '../xml/extract.js';
import { resetTables, setMeta, getMeta } from '../db.js';
import { abs } from '../paths.js';
import { upsertTurret, extensionRows } from './persist.js';

/** Full XML -> SQLite ingest. Wipes and repopulates every extracted table. */
export function runExtraction(db) {
  const started = Date.now();
  const { turrets, files, malformed, knownDefs } = extractAll();

  const write = db.transaction(() => {
    resetTables(db);

    const fileIds = new Map();
    const now = new Date().toISOString();
    for (const [filePath, checksum] of files) {
      db.run('INSERT INTO files (file_path, abs_path, checksum, scanned_at) VALUES (?, ?, ?, ?)',
        filePath, abs(filePath), checksum, now);
      fileIds.set(filePath, db.get('SELECT file_id FROM files WHERE file_path = ?', filePath).file_id);
    }

    for (const t of turrets) {
      upsertTurret(db, t, {
        fileId: fileIds.get(t.filePath) ?? null,
        weaponFileId: fileIds.get(t.weaponFilePath) ?? null,
        modified: false,
        originalJson: JSON.stringify(t),
      });
    }

    setMeta(db, 'last_extraction', new Date().toISOString());
    setMeta(db, 'turret_count', turrets.length);
    setMeta(db, 'known_defs', JSON.stringify(knownDefs));
  });

  write();

  return {
    ok: true,
    durationMs: Date.now() - started,
    counts: {
      files: files.size,
      turrets: turrets.length,
      weapons: new Set(turrets.map((t) => t.weaponDefName).filter(Boolean)).size,
      costs: turrets.reduce((n, t) => n + t.costs.length, 0),
      modExtensions: turrets.reduce((n, t) => n + extensionRows(t).length, 0),
      warnings: turrets.reduce((n, t) => n + t.warnings.length, 0),
    },
    malformed: malformed.map((m) => ({ file: m.file, error: m.error, line: m.line })),
  };
}

/** Fresh, uncached read of the current on-disk state, keyed by defName. */
export function diskSnapshot() {
  const { turrets } = extractAll();
  return new Map(turrets.map((t) => [t.defName, t]));
}

/** Predicate over every def name seen during the last extraction. */
export function knownDefPredicate(db) {
  let names;
  try {
    names = new Set(JSON.parse(getMeta(db, 'known_defs') || '[]'));
  } catch {
    names = new Set();
  }
  return names.size ? (name) => names.has(name) : () => true;
}
