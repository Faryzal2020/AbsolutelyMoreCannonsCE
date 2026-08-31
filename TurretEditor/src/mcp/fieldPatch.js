/**
 * Translation layer between the flat, dotted field keys an AI client works with
 * and the nested patch objects `updateTurret` expects.
 *
 * This exists to close a silent failure mode: `updateTurret` merges whatever it
 * is handed, so a patch of `{ 'stats.maxHitPoints': 500 }` would store a literal
 * top-level key of that name, mark the record modified, and then produce no diff
 * and inject nothing. Every key is therefore checked against the field map first
 * and unknown keys are rejected rather than merged.
 */

import {
  FIELDS, FIELD_BY_KEY, COMPONENT_TOGGLES, TOGGLE_BY_KEY, coerce, getPath, setPath,
} from '../xml/fieldMap.js';
import { deepMerge } from '../services/turretService.js';

const NUMERIC = new Set(['int', 'float']);

/**
 * Keys that are edited as a whole structure rather than through a field-map
 * path. `costList` has its own diff kind and its own injector (`setCostList`),
 * because the resource rows are arbitrary ThingDef names rather than a fixed
 * set of tags.
 */
const SPECIAL = new Map([
  ['costs', {
    key: 'costs',
    type: 'costList',
    doc: 'building',
    label: 'Resource Cost List',
    shape: 'Array of {thingDef, count}. Replaces the whole list. "mul" and "add" '
      + 'scale every count instead, rounded to whole units and floored at 1.',
  }],
]);

/** The full list of editable keys, for `get_field_catalogue`. */
export function fieldCatalogue() {
  return {
    fields: FIELDS.map(({ key, type, label, doc, gate }) => ({
      key, type, label, def: doc, ...(gate ? { gate } : {}),
    })),
    toggles: COMPONENT_TOGGLES.map(({ key, label, cls }) => ({
      key, type: 'bool', label, adds: cls,
    })),
    special: [...SPECIAL.values()].map(({ key, type, label, doc, shape }) => ({
      key, type, label, def: doc, shape,
    })),
  };
}

/** Validate and normalise a whole cost list. */
function coerceCosts(value) {
  if (!Array.isArray(value) || !value.length) return null;
  const out = [];
  for (const row of value) {
    if (!row || typeof row !== 'object') return null;
    const thingDef = String(row.thingDef ?? '').trim();
    const count = Number(row.count);
    if (!thingDef || !Number.isFinite(count)) return null;
    out.push({ thingDef, count: Math.max(1, Math.round(count)) });
  }
  return out;
}

/** Scale every row of an existing cost list. */
function scaleCosts(current, op, value) {
  if (!Array.isArray(current) || !current.length) return { reason: 'no-current-value' };
  const operand = Number(value);
  if (Number.isNaN(operand)) return { reason: 'invalid-value' };
  return {
    value: current.map((c) => ({
      thingDef: c.thingDef,
      count: Math.max(1, Math.round(op === 'mul' ? c.count * operand : c.count + operand)),
    })),
  };
}

/** Resolve one `{key, op, value}` against the record's current value. */
function resolveValue(turret, key, op, value) {
  if (SPECIAL.has(key)) {
    if (op === 'mul' || op === 'add') return scaleCosts(getPath(turret, key), op, value);
    const costs = coerceCosts(value);
    return costs ? { value: costs } : { reason: 'invalid-value' };
  }

  const field = FIELD_BY_KEY.get(key);

  if (op === 'set' || op === undefined) {
    if (!field) return { value: Boolean(value) };            // component toggle
    const coerced = coerce(field.type, value);
    if (coerced === null && value !== null && value !== '') {
      return { reason: 'invalid-value' };
    }
    return { value: coerced };
  }

  if (!field) return { reason: 'toggle-not-numeric' };
  if (!NUMERIC.has(field.type)) return { reason: 'not-numeric' };

  const current = getPath(turret, key);
  if (typeof current !== 'number') return { reason: 'no-current-value' };

  const operand = Number(value);
  if (Number.isNaN(operand)) return { reason: 'invalid-value' };

  const next = op === 'mul' ? current * operand : current + operand;
  return { value: field.type === 'int' ? Math.round(next) : next };
}

/**
 * Build a nested patch from dotted keys.
 *
 * @param {object} turret  the current record, used for relative ops and gates
 * @param {Array<{key:string, op?:'set'|'mul'|'add', value:*}>} ops
 * @returns {{patch:object, accepted:Array, rejected:Array}}
 */
export function buildPatch(turret, ops) {
  const accepted = [];
  const rejected = [];

  for (const { key, op = 'set', value } of ops) {
    if (!FIELD_BY_KEY.has(key) && !TOGGLE_BY_KEY.has(key) && !SPECIAL.has(key)) {
      rejected.push({ key, reason: 'unknown-field' });
      continue;
    }
    if (!['set', 'mul', 'add'].includes(op)) {
      rejected.push({ key, reason: 'unknown-op' });
      continue;
    }
    const resolved = resolveValue(turret, key, op, value);
    if (resolved.reason) {
      rejected.push({ key, reason: resolved.reason });
      continue;
    }
    accepted.push({ key, from: getPath(turret, key) ?? null, to: resolved.value });
  }

  // A field inside a comp that is switched off has no tag to write to, so the
  // injector would silently skip it. Gates are checked against the *merged*
  // result so that enabling a comp and setting its fields in one call works.
  const merged = deepMerge(turret, toPatch(accepted));
  const gated = [];
  const surviving = accepted.filter((change) => {
    const gate = FIELD_BY_KEY.get(change.key)?.gate;
    if (!gate || getPath(merged, gate)) return true;
    gated.push({ key: change.key, reason: 'gate-disabled', gate });
    return false;
  });

  return { patch: toPatch(surviving), accepted: surviving, rejected: [...rejected, ...gated] };
}

function toPatch(changes) {
  const patch = {};
  for (const { key, to } of changes) setPath(patch, key, to);
  return patch;
}
