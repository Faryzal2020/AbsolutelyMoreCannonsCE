import { FIELDS, COMPONENT_TOGGLES, getPath, serialise } from '../xml/fieldMap.js';
import { diskSnapshot } from './extractService.js';

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
 * @returns {Array<object>} change descriptors consumed by both the diff view
 *          and the injector.
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
    changes.push({
      kind: 'component', action: now ? 'add' : 'remove',
      defName: current.defName, doc: 'building', filePath: current.filePath,
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

/**
 * Diff every turret marked modified in the database against disk.
 * @param {object} db
 * @param {string[]|null} onlyDefs restrict to these defNames
 */
export function computeDiffs(db, onlyDefs = null) {
  const disk = diskSnapshot();
  const rows = db.all('SELECT def_name, data_json FROM turrets WHERE modified = 1 ORDER BY def_name');

  const result = [];
  for (const row of rows) {
    if (onlyDefs && !onlyDefs.includes(row.def_name)) continue;
    const current = JSON.parse(row.data_json);
    const changes = diffTurret(current, disk.get(row.def_name));
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

  return {
    ok: true,
    generatedAt: new Date().toISOString(),
    defCount: result.length,
    changeCount: result.reduce((n, d) => n + d.changeCount, 0),
    diffs: result,
  };
}
