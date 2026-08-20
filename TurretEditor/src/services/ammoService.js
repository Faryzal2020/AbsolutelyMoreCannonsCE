import { getMeta } from '../db.js';

function deepMerge(target, source) {
  for (const [k, v] of Object.entries(source || {})) {
    if (v === null || v === undefined) continue;
    if (Array.isArray(v)) {
      target[k] = v;
    } else if (typeof v === 'object') {
      target[k] = target[k] || {};
      deepMerge(target[k], v);
    } else {
      target[k] = v;
    }
  }
  return target;
}

export function listAmmo(db) {
  const rows = db.all('SELECT data_json, modified FROM ammo ORDER BY ammo_family ASC, label ASC');
  return rows.map((r) => {
    const item = JSON.parse(r.data_json);
    item.modified = Boolean(r.modified);
    return item;
  });
}

export function getAmmo(db, defName) {
  const row = db.get('SELECT data_json, modified FROM ammo WHERE def_name = ?', defName);
  if (!row) return null;
  const item = JSON.parse(row.data_json);
  item.modified = Boolean(row.modified);
  return item;
}

export function upsertAmmoRecord(db, item, { fileId = null, modified = false, originalJson = null } = {}) {
  const now = new Date().toISOString();
  const orig = originalJson ?? JSON.stringify(item);

  db.run(`INSERT OR REPLACE INTO ammo (
      def_name, file_id, label, ammo_family, ammo_set_name, indirect_ammo_set_name,
      ammo_class, has_direct_mode, has_indirect_mode, direct_bullet_def, indirect_bullet_def,
      market_value, mass, bulk, modified, data_json, original_json, updated_at
    ) VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)`,
    item.defName, fileId, item.label, item.ammoFamily, item.ammoSetName, item.indirectAmmoSetName,
    item.ammoClass, item.hasDirectMode ? 1 : 0, item.hasIndirectMode ? 1 : 0,
    item.directBulletDef, item.indirectBulletDef,
    parseFloat(item.stats.marketValue) || null,
    parseFloat(item.stats.mass) || null,
    parseFloat(item.stats.bulk) || null,
    modified ? 1 : 0,
    JSON.stringify(item), orig, now
  );

  db.run('DELETE FROM ammo_recipes WHERE def_name = ?', item.defName);
  if (item.recipe && item.recipe.defName) {
    db.run(`INSERT INTO ammo_recipes (def_name, recipe_def, work_amount, yield_count, ingredients_json)
      VALUES (?, ?, ?, ?, ?)`,
      item.defName, item.recipe.defName, item.recipe.workAmount, item.recipe.yieldCount,
      JSON.stringify(item.recipe.ingredients || [])
    );
  }
}

export function updateAmmo(db, defName, patch) {
  const current = getAmmo(db, defName);
  if (!current) return null;

  const previousHasDirect = current.hasDirectMode;
  const previousHasIndirect = current.hasIndirectMode;

  deepMerge(current, patch);

  // Auto-generation logic when toggling Direct/Indirect modes ON
  if (!previousHasDirect && current.hasDirectMode) {
    if (!current.directBulletDef) {
      current.directBulletDef = current.defName.replace('Ammo_', 'Bullet_');
    }
    if (!current.direct.speed) current.direct.speed = 168;
    current.direct.dropsCasings = true;
  }

  if (!previousHasIndirect && current.hasIndirectMode) {
    if (!current.indirectBulletDef) {
      current.indirectBulletDef = current.directBulletDef
        ? current.directBulletDef.replace('Bullet_', 'Bullet_indirect_')
        : current.defName.replace('Ammo_', 'Bullet_indirect_');
    }
    current.indirect.speed = 0;
    current.indirect.flyOverhead = true;
    if (!current.indirect.shellingTilesPerTick) current.indirect.shellingTilesPerTick = 0.15;
    if (!current.indirect.shellingRange) current.indirect.shellingRange = 18;
    if (!current.indirect.shellingDamage) current.indirect.shellingDamage = 0.1;
  }

  current.modified = true;
  upsertAmmoRecord(db, current, { modified: true, originalJson: current.originalJson });
  return current;
}

export function revertAmmo(db, defName) {
  const row = db.get('SELECT original_json FROM ammo WHERE def_name = ?', defName);
  if (!row) return null;
  const original = JSON.parse(row.original_json);
  original.modified = false;
  upsertAmmoRecord(db, original, { modified: false, originalJson: row.original_json });
  return original;
}

export function revertAllAmmo(db) {
  const rows = db.all('SELECT def_name, original_json FROM ammo WHERE modified = 1');
  for (const r of rows) {
    const original = JSON.parse(r.original_json);
    original.modified = false;
    upsertAmmoRecord(db, original, { modified: false, originalJson: r.original_json });
  }
  return rows.length;
}
