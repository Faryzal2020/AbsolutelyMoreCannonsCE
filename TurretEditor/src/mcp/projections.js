/**
 * Compact result shapes for the MCP tools.
 *
 * `listTurrets()` returns every parsed record — about 5.6 KB of JSON each, or
 * ~365 KB for the whole mod, which would swamp a model's context on a single
 * call. The extracted tables already carry the headline stats in indexed
 * columns, so listings read those instead and full records stay behind
 * `get_turret`.
 */

const LIST_COLUMNS = `def_name, label, category, mode, weapon_def_name,
  max_hp, cooldown_time, power_consumption, modified`;

const AMMO_LIST_COLUMNS = `def_name, label, ammo_family, ammo_set_name,
  has_direct_mode, has_indirect_mode, market_value, modified`;

const like = (term) => `%${String(term).toLowerCase()}%`;

/** Filtered turret listing, one small row per turret. */
export function listTurretRows(db, { category, search, modified, warnings } = {}) {
  const where = [];
  const params = [];

  if (category && category !== 'all') {
    where.push('LOWER(category) = ?');
    params.push(String(category).toLowerCase());
  }
  if (modified === true) where.push('modified = 1');
  if (search) {
    where.push(`(LOWER(def_name) LIKE ? OR LOWER(label) LIKE ?
      OR LOWER(category) LIKE ? OR LOWER(COALESCE(weapon_def_name, '')) LIKE ?)`);
    params.push(like(search), like(search), like(search), like(search));
  }

  // Warnings are derived at extraction time and live in data_json, so that one
  // filter has to read the blob. It is still projected down before returning.
  const columns = warnings === true ? `${LIST_COLUMNS}, data_json` : LIST_COLUMNS;
  const sql = `SELECT ${columns} FROM turrets
    ${where.length ? `WHERE ${where.join(' AND ')}` : ''}
    ORDER BY category, label`;

  let rows = db.all(sql, ...params);
  if (warnings === true) {
    rows = rows.filter((r) => (JSON.parse(r.data_json).warnings || []).length > 0);
  }
  return rows.map(turretSummary);
}

export function turretSummary(row) {
  return {
    defName: row.def_name,
    label: row.label,
    category: row.category,
    mode: row.mode,
    weapon: row.weapon_def_name,
    maxHitPoints: row.max_hp,
    cooldownTime: row.cooldown_time,
    powerWatts: row.power_consumption,
    modified: row.modified === 1,
  };
}

export function listAmmoRows(db, { ammoFamily, search, modified } = {}) {
  const where = [];
  const params = [];

  if (ammoFamily) {
    where.push('LOWER(COALESCE(ammo_family, \'\')) = ?');
    params.push(String(ammoFamily).toLowerCase());
  }
  if (modified === true) where.push('modified = 1');
  if (search) {
    where.push('(LOWER(def_name) LIKE ? OR LOWER(label) LIKE ?)');
    params.push(like(search), like(search));
  }

  return db.all(`SELECT ${AMMO_LIST_COLUMNS} FROM ammo
    ${where.length ? `WHERE ${where.join(' AND ')}` : ''}
    ORDER BY ammo_family, label`, ...params).map((row) => ({
    defName: row.def_name,
    label: row.label,
    family: row.ammo_family,
    ammoSet: row.ammo_set_name,
    direct: row.has_direct_mode === 1,
    indirect: row.has_indirect_mode === 1,
    marketValue: row.market_value,
    modified: row.modified === 1,
  }));
}

/**
 * Diff listing without the XML bodies. `previewXml` re-reads the whole def tree
 * from disk, so full XML stays behind `preview_turret_xml`, one def at a time.
 */
export function diffSummary({ diffs, defCount, changeCount, generatedAt }) {
  return {
    generatedAt,
    defCount,
    changeCount,
    diffs: diffs.map((d) => ({
      defName: d.defName,
      filePath: d.filePath,
      changes: d.changes.map((c) => ({
        key: c.key, label: c.label, from: c.from, to: c.to,
      })),
    })),
  };
}

/** Injection report trimmed to what a client needs to decide what happened. */
export function injectSummary(result, ammoDefNames) {
  const isAmmo = (defName) => ammoDefNames.has(defName);
  return {
    ok: result.ok,
    dryRun: result.dryRun,
    defCount: result.defCount,
    filesWritten: result.filesWritten,
    files: result.files,
    backups: result.backups,
    appliedCount: result.appliedCount,
    skippedCount: result.skippedCount,
    // Ammo diffs ride along in the same injection pass, so they are called out
    // explicitly rather than being written invisibly.
    ammoDefsTouched: [...new Set(result.applied.map((c) => c.defName).filter(isAmmo))],
    applied: result.applied.map((c) => ({ defName: c.defName, key: c.key, from: c.from, to: c.to })),
    skipped: result.skipped.map((c) => ({ defName: c.defName, key: c.key, reason: c.reason })),
    reExtract: result.reExtract,
  };
}
