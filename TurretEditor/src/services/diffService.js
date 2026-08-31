import { FIELDS, COMPONENT_TOGGLES, getPath, serialise } from '../xml/fieldMap.js';
import { AMMO_FIELDS } from '../xml/fieldMapAmmo.js';
import { diskSnapshot } from './extractService.js';
import { extractAllAmmo } from '../xml/extractAmmo.js';

const isBlank = (v) => v === null || v === undefined || v === '';

/** Field-aware equality: null and "" mean the same thing in RimWorld XML. */
function sameValue(a, b, type) {
  if (isBlank(a) && isBlank(b)) return true;
  if (isBlank(a) || isBlank(b)) return false;
  if (type === 'bool') return Boolean(a) === Boolean(b);
  if (type === 'int' || type === 'float') return Number(a) === Number(b);
  if (type === 'list') {
    const norm = (v) => String(v).split(',').map((s) => s.trim()).filter(Boolean).join(',');
    return norm(a) === norm(b);
  }
  return String(a) === String(b);
}

function costsEqual(a = [], b = []) {
  if (a.length !== b.length) return false;
  const key = (rows) => rows.map((r) => `${r.thingDef}=${r.count}`).sort().join('|');
  return key(a) === key(b);
}

/** A gate that is off means the field's XML home does not exist — skip it. */
function gateOpen(obj, field) {
  return !field.gate || Boolean(getPath(obj, field.gate));
}

/**
 * Compute every pending change for one turret: DB state vs on-disk state.
 */
export function diffTurret(current, disk) {
  const changes = [];
  if (!disk) {
    return [{
      kind: 'missing', defName: current.defName, filePath: current.filePath,
      label: 'Def not found on disk', from: null, to: null,
    }];
  }

  for (const toggle of COMPONENT_TOGGLES) {
    const now = Boolean(getPath(current, toggle.key));
    const was = Boolean(getPath(disk, toggle.key));
    if (now === was) continue;

    // A weapon-side comp has to be added to the weapon ThingDef, in the weapon's
    // file — writing it into the building def would attach it to the wrong thing.
    const onWeapon = toggle.doc === 'weapon';
    if (onWeapon && !current.weaponDefName) continue;

    changes.push({
      kind: 'component', action: now ? 'add' : 'remove',
      defName: onWeapon ? current.weaponDefName : current.defName,
      ownerDefName: current.defName,
      doc: toggle.doc || 'building',
      filePath: onWeapon ? (current.weaponFilePath || current.filePath) : current.filePath,
      key: toggle.key, label: toggle.label, container: toggle.container, cls: toggle.cls,
      from: was, to: now,
      xmlPath: `${toggle.container} > li Class="${toggle.cls}"`,
    });
  }

  for (const field of FIELDS) {
    if (!gateOpen(current, field)) continue;
    const to = getPath(current, field.key);
    const from = getPath(disk, field.key);
    if (sameValue(from, to, field.type)) continue;
    changes.push({
      kind: 'field',
      defName: field.doc === 'weapon' ? current.weaponDefName : current.defName,
      ownerDefName: current.defName,
      doc: field.doc,
      filePath: field.doc === 'weapon' ? (current.weaponFilePath || current.filePath) : current.filePath,
      key: field.key, label: field.label, type: field.type,
      from, to,
      xmlPath: field.path.map((s) => (s.cls ? `li[${s.cls}]` : s.tag)).join(' > '),
      serialised: serialise(field.type, to),
    });
  }

  if (!costsEqual(current.costs, disk.costs)) {
    changes.push({
      kind: 'costList',
      defName: current.defName, ownerDefName: current.defName, doc: 'building',
      filePath: current.filePath, key: 'costs', label: 'Resource Cost List',
      from: disk.costs, to: current.costs, xmlPath: 'costList',
    });
  }

  return changes;
}

export function diffAmmo(current, disk) {
  const changes = [];
  if (!disk) return changes;

  for (const field of AMMO_FIELDS) {
    const to = getPath(current, field.key);
    const from = getPath(disk, field.key);
    if (sameValue(from, to, field.type)) continue;

    const targets = [];
    if (field.doc === 'ammo') {
      targets.push({ defName: current.defName, filePath: current.filePath });
    } else if (field.doc === 'recipe') {
      if (current.recipe?.defName) {
        targets.push({ defName: current.recipe.defName, filePath: current.filePath });
      }
    } else if (field.doc === 'shared_projectile') {
      if (current.hasDirectMode && current.directBulletDef) {
        targets.push({ defName: current.directBulletDef, filePath: current.directFilePath || current.filePath });
      }
      if (current.hasIndirectMode && current.indirectBulletDef) {
        targets.push({ defName: current.indirectBulletDef, filePath: current.indirectFilePath || current.filePath });
      }
    } else if (field.doc === 'direct_projectile') {
      if (current.hasDirectMode && current.directBulletDef) {
        targets.push({ defName: current.directBulletDef, filePath: current.directFilePath || current.filePath });
      }
    } else if (field.doc === 'indirect_projectile') {
      if (current.hasIndirectMode && current.indirectBulletDef) {
        targets.push({ defName: current.indirectBulletDef, filePath: current.indirectFilePath || current.filePath });
      }
    }

    for (const t of targets) {
      changes.push({
        kind: 'field',
        defName: t.defName,
        ownerDefName: current.defName,
        doc: field.doc,
        filePath: t.filePath,
        key: field.key,
        label: field.key,
        type: field.type,
        from, to,
        xmlPath: field.path.map((s) => (s.cls ? `li[${s.cls}]` : s.tag)).join(' > '),
        serialised: serialise(field.type, to),
      });
    }
  }

  return changes;
}

/**
 * Diff every turret and ammo marked modified in the database against disk.
 */
export function computeDiffs(db, onlyDefs = null) {
  const diskTurrets = diskSnapshot();
  const turretRows = db.all('SELECT def_name, data_json FROM turrets WHERE modified = 1 ORDER BY def_name');

  const result = [];
  for (const row of turretRows) {
    if (onlyDefs && !onlyDefs.includes(row.def_name)) continue;
    const current = JSON.parse(row.data_json);
    const changes = diffTurret(current, diskTurrets.get(row.def_name));
    if (changes.length) {
      result.push({
        defName: row.def_name,
        label: current.label,
        filePath: current.filePath,
        weaponFilePath: current.weaponFilePath,
        changeCount: changes.length,
        changes,
      });
    }
  }

  const ammoRows = db.all('SELECT def_name, data_json FROM ammo WHERE modified = 1 ORDER BY def_name');
  if (ammoRows.length) {
    const { ammo: diskAmmoList } = extractAllAmmo();
    const diskAmmoMap = new Map(diskAmmoList.map((a) => [a.defName, a]));

    for (const row of ammoRows) {
      if (onlyDefs && !onlyDefs.includes(row.def_name)) continue;
      const current = JSON.parse(row.data_json);
      const changes = diffAmmo(current, diskAmmoMap.get(row.def_name));
      if (changes.length) {
        result.push({
          defName: row.def_name,
          label: current.label,
          filePath: current.filePath,
          changeCount: changes.length,
          changes,
        });
      }
    }
  }

  return {
    ok: true,
    generatedAt: new Date().toISOString(),
    defCount: result.length,
    changeCount: result.reduce((n, d) => n + d.changeCount, 0),
    diffs: result,
  };
}
