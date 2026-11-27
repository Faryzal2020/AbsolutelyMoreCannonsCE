/**
 * The MCP tool catalogue.
 *
 * Every tool is a thin wrapper over the same services the web editor and the
 * REST API use, so there is exactly one implementation of the XML pipeline.
 * Schemas are hand-written JSON Schema, matching the style of routes/openapi.js.
 *
 * Three hazards shape the design:
 *   - The database is a snapshot of the XML. server.js re-syncs it before every
 *     call (syncIfStale) so no tool reads or writes from stale values.
 *   - `runExtraction` wipes the extracted tables, so every re-read goes through
 *     `reextractPreservingEdits`, and discarding edits needs an explicit flag.
 *   - `runInjection` writes to the user's XML files, so it previews by default.
 */

import { runExtraction } from '../services/extractService.js';
import { reextractPreservingEdits } from '../services/syncService.js';
import { computeDiffs } from '../services/diffService.js';
import { runInjection, runRollback, listBackups } from '../services/injectService.js';
import {
  getTurret, updateTurret, revertTurret, revertAll, previewXml,
} from '../services/turretService.js';
import { getAmmo, revertAmmo, revertAllAmmo } from '../services/ammoService.js';
import { metrics, auditReport } from '../services/auditService.js';
import { getMeta } from '../db.js';
import { buildPatch, fieldCatalogue } from './fieldPatch.js';
import {
  listTurretRows, listAmmoRows, diffSummary, injectSummary,
} from './projections.js';

const str = (description) => ({ type: 'string', description });
const bool = (description, def = false) => ({ type: 'boolean', description, default: def });
const defNameArg = {
  type: 'object',
  properties: { defName: str('Exact defName of the record.') },
  required: ['defName'],
};

const fail = (error, extra = {}) => ({ ok: false, error, ...extra });

/** Pending edits that a re-extraction would destroy. */
function pendingEdits(db) {
  return db.get('SELECT COUNT(*) AS n FROM turrets WHERE modified = 1').n
    + db.get('SELECT COUNT(*) AS n FROM ammo WHERE modified = 1').n;
}

function ammoDefNames(db) {
  return new Set(db.all('SELECT def_name FROM ammo').map((r) => r.def_name));
}

export const TOOLS = [
  // --- reading ----------------------------------------------------------
  {
    name: 'list_turrets',
    description:
      'List turrets as compact summaries (defName, label, category, key stats, modified flag). '
      + 'Use this to find defNames, then get_turret for the full record. Filters are ANDed.',
    inputSchema: {
      type: 'object',
      properties: {
        category: str('Folder category, or "all".'),
        search: str('Substring match over defName, label, category and weapon defName.'),
        modified: bool('Only turrets with unsaved edits.'),
        warnings: bool('Only turrets with validation findings.'),
      },
    },
    handler: (db, args) => {
      const turrets = listTurretRows(db, args);
      return { ok: true, count: turrets.length, turrets };
    },
  },
  {
    name: 'get_turret',
    description:
      'Full record for one turret: stats, costs, ballistics, ammo, comps, barrel extension, '
      + 'smoker settings and validation warnings. Roughly 5.6 KB of JSON.',
    inputSchema: defNameArg,
    handler: (db, { defName }) => {
      const turret = getTurret(db, defName);
      return turret ? { ok: true, turret } : fail(`Unknown turret def: ${defName}`);
    },
  },
  {
    name: 'get_field_catalogue',
    description:
      'Every editable key with its type, label and gating comp, in three groups: "fields" '
      + '(dotted scalar keys), "toggles" (add or remove a whole comp/modExtension) and '
      + '"special" (structures such as the cost list). Call this before update_turret or '
      + 'batch_update_turrets: keys not in this catalogue are rejected.',
    inputSchema: { type: 'object', properties: {} },
    handler: () => ({ ok: true, ...fieldCatalogue() }),
  },
  {
    name: 'get_metrics',
    description: 'Counts of turrets by capability, modified count, last extraction time and backup count.',
    inputSchema: { type: 'object', properties: {} },
    handler: (db) => ({ ok: true, ...metrics(db) }),
  },
  {
    name: 'get_audit',
    description: 'Validation findings grouped by turret, with per-rule totals.',
    inputSchema: {
      type: 'object',
      properties: { level: { type: 'string', enum: ['error', 'warn'], description: 'Only findings at this level.' } },
    },
    handler: (db, { level }) => {
      const report = auditReport(db);
      if (!level) return report;
      const entries = report.entries
        .map((e) => ({ ...e, issues: e.issues.filter((w) => w.level === level) }))
        .filter((e) => e.issues.length);
      return { ...report, affected: entries.length, entries };
    },
  },

  // --- editing ----------------------------------------------------------
  {
    name: 'update_turret',
    description:
      'Set fields on one turret in the database. Changes are keyed by dotted field key '
      + '(e.g. "stats.maxHitPoints", "ballistics.cooldown", "ammo.magazineSize") — see '
      + 'get_field_catalogue. The key "costs" takes a whole [{thingDef, count}] list. '
      + 'Nothing is written to XML until inject_xml_changes runs.',
    inputSchema: {
      type: 'object',
      properties: {
        defName: str('Exact defName of the turret.'),
        changes: {
          type: 'object',
          description: 'Map of dotted field key to new value, e.g. {"stats.maxHitPoints": 500}. '
            + 'The "costs" key takes an array: {"costs": [{"thingDef": "Steel", "count": 900}]}.',
          additionalProperties: true,
        },
      },
      required: ['defName', 'changes'],
    },
    handler: (db, { defName, changes }) => {
      const turret = getTurret(db, defName);
      if (!turret) return fail(`Unknown turret def: ${defName}`);
      if (!changes || typeof changes !== 'object' || Array.isArray(changes)) {
        return fail('changes must be an object of field key to value.');
      }

      const ops = Object.entries(changes).map(([key, value]) => ({ key, op: 'set', value }));
      const { patch, accepted, rejected } = buildPatch(turret, ops);
      if (!accepted.length) return fail('No valid field keys in changes.', { rejected });

      const updated = updateTurret(db, defName, patch);
      return {
        ok: true, defName, modified: updated.modified, applied: accepted, rejected,
      };
    },
  },
  {
    name: 'batch_update_turrets',
    description:
      'Apply the same operations to every turret matching a filter, in one transaction. '
      + 'Ops are {key, op, value} where op is "set", "mul" (multiply) or "add". '
      + 'Relative ops work only on numeric fields; int fields are rounded. On the "costs" '
      + 'key they scale every resource count instead, floored at 1. '
      + 'Use dryRun to preview the resulting values before committing.',
    inputSchema: {
      type: 'object',
      properties: {
        filter: {
          type: 'object',
          description: 'Same filters as list_turrets. Ignored when defNames is given.',
          properties: {
            category: str('Folder category, or "all".'),
            search: str('Substring match.'),
            modified: bool('Only turrets with unsaved edits.'),
          },
        },
        defNames: { type: 'array', items: { type: 'string' }, description: 'Explicit target list.' },
        ops: {
          type: 'array',
          description: 'Operations to apply to every matched turret.',
          items: {
            type: 'object',
            properties: {
              key: str('Dotted field key from get_field_catalogue.'),
              op: { type: 'string', enum: ['set', 'mul', 'add'], default: 'set' },
              value: { description: 'Literal for "set", multiplier for "mul", delta for "add".' },
            },
            required: ['key', 'value'],
          },
          minItems: 1,
        },
        dryRun: bool('Compute the new values without writing to the database.'),
      },
      required: ['ops'],
    },
    handler: (db, { filter = {}, defNames = null, ops, dryRun = false }) => {
      if (!Array.isArray(ops) || !ops.length) return fail('ops must be a non-empty array.');

      const targets = defNames
        ? defNames.map((d) => ({ defName: d }))
        : listTurretRows(db, filter);
      if (!targets.length) return fail('No turrets matched.', { matched: 0 });

      const plans = [];
      for (const { defName } of targets) {
        const turret = getTurret(db, defName);
        if (!turret) {
          plans.push({ defName, ok: false, rejected: [{ reason: 'unknown-field', key: defName }] });
          continue;
        }
        const { patch, accepted, rejected } = buildPatch(turret, ops);
        plans.push({ defName, ok: accepted.length > 0, patch, changes: accepted, rejected });
      }

      // A key that no matched turret accepts is a mistake in the request, not a
      // partial success — surface it instead of silently changing nothing.
      const anyApplied = plans.some((p) => p.ok);
      if (!anyApplied) {
        return fail('No operation applied to any matched turret.', {
          matched: targets.length,
          rejected: plans[0]?.rejected ?? [],
        });
      }

      if (!dryRun) {
        db.transaction(() => {
          for (const plan of plans) {
            if (plan.ok) updateTurret(db, plan.defName, plan.patch);
          }
        })();
      }

      return {
        ok: true,
        dryRun,
        matched: targets.length,
        updated: plans.filter((p) => p.ok).length,
        results: plans.map(({ defName, ok, changes = [], rejected = [] }) =>
          ({ defName, ok, changes, ...(rejected.length ? { rejected } : {}) })),
      };
    },
  },
  {
    name: 'revert_turret',
    description: 'Discard database edits for one turret, restoring the state read from disk. Touches no files.',
    inputSchema: defNameArg,
    handler: (db, { defName }) => {
      const turret = revertTurret(db, defName);
      return turret ? { ok: true, defName, reverted: true } : fail(`Unknown turret def: ${defName}`);
    },
  },
  {
    name: 'revert_all',
    description: 'Discard every pending turret edit in the database. Touches no files.',
    inputSchema: {
      type: 'object',
      properties: { includeAmmo: bool('Also revert pending ammo edits.') },
    },
    handler: (db, { includeAmmo = false }) => ({
      ok: true,
      reverted: revertAll(db),
      ammoReverted: includeAmmo ? revertAllAmmo(db) : 0,
    }),
  },

  // --- diffing and writing ----------------------------------------------
  {
    name: 'get_diffs',
    description:
      'Pending changes between the database and the XML on disk, as a per-field list '
      + '(key, from, to). Covers both turret and ammo edits.',
    inputSchema: {
      type: 'object',
      properties: {
        defNames: { type: 'array', items: { type: 'string' }, description: 'Limit to these defs.' },
      },
    },
    handler: (db, { defNames = null }) => ({ ok: true, ...diffSummary(computeDiffs(db, defNames)) }),
  },
  {
    name: 'preview_turret_xml',
    description:
      'Side-by-side XML for one turret: the block as it is on disk, and the same block with '
      + 'pending edits applied in memory. Nothing is written. One def at a time — this '
      + 're-reads the def tree from disk on every call.',
    inputSchema: defNameArg,
    handler: (db, { defName }) => {
      const preview = previewXml(db, defName);
      return preview ? { ok: true, ...preview } : fail(`Unknown turret def: ${defName}`);
    },
  },
  {
    name: 'inject_xml_changes',
    description:
      'Write pending database edits back into the mod XML by surgical in-place edit. '
      + 'Previews by default: pass dryRun=false to actually write. On the first write of a '
      + 'file a pristine .xml.bak is created next to it. Ammo edits are injected in the same '
      + 'pass and reported under ammoDefsTouched.',
    inputSchema: {
      type: 'object',
      properties: {
        defNames: { type: 'array', items: { type: 'string' }, description: 'Limit the write to these defs.' },
        dryRun: bool('Report what would change without touching any file.', true),
      },
    },
    handler: (db, { defNames = null, dryRun = true }) => {
      if (Array.isArray(defNames) && !defNames.length) return fail('defNames is empty; omit it to inject every pending edit.');
      const result = runInjection(db, { defNames, dryRun });
      return injectSummary(result, ammoDefNames(db));
    },
  },
  {
    name: 'list_backups',
    description: 'Every .xml.bak under Common/Defs, with the file it would restore and whether '
      + 'rollback_xml considers it safe to restore (restorable / reason).',
    inputSchema: { type: 'object', properties: {} },
    handler: (db) => ({ ok: true, backups: listBackups(db) }),
  },
  {
    name: 'rollback_xml',
    description:
      'Restore .xml.bak files over their .xml. Works per file, not per def. Only backups '
      + 'written by inject_xml_changes whose file has not changed since are restored; '
      + 'untracked or stale backups (restoring them would revert newer work such as hand '
      + 'edits or commits) are listed under "skipped" unless force is true. Deletes the '
      + 'restored backups unless keepBackups is true. Pending edits are preserved.',
    inputSchema: {
      type: 'object',
      properties: {
        keepBackups: bool('Leave the .xml.bak files in place after restoring.'),
        dryRun: bool('List what would be restored without writing.', true),
        force: bool('Also restore untracked/stale backups. Reverts any later changes to those files.'),
      },
    },
    handler: (db, { keepBackups = false, dryRun = true, force = false }) =>
      runRollback(db, { keepBackups, dryRun, force }),
  },

  // --- extraction -------------------------------------------------------
  {
    name: 'extract_defs',
    description:
      'Re-read every XML file into the database. Pending edits are kept: they are replayed '
      + 'on top of the fresh values, and any key that also changed on disk is reported under '
      + '"conflicts" (the pending edit wins). Pass force=true to discard every pending edit '
      + 'instead. Rarely needed: every tool call already re-syncs when the XML changed.',
    inputSchema: {
      type: 'object',
      properties: { force: bool('Discard pending edits and re-extract from disk.') },
    },
    handler: (db, { force = false }) => {
      if (force) return { ...runExtraction(db), discarded: true };
      return reextractPreservingEdits(db);
    },
  },

  // --- ammunition (read and revert only) ---------------------------------
  {
    name: 'list_ammo',
    description: 'List ammo defs as compact summaries.',
    inputSchema: {
      type: 'object',
      properties: {
        ammoFamily: str('Exact ammo family.'),
        search: str('Substring match over defName and label.'),
        modified: bool('Only ammo with unsaved edits.'),
      },
    },
    handler: (db, args) => {
      const ammo = listAmmoRows(db, args);
      return { ok: true, count: ammo.length, ammo };
    },
  },
  {
    name: 'get_ammo',
    description: 'Full record for one ammo def, including direct and indirect projectile settings.',
    inputSchema: defNameArg,
    handler: (db, { defName }) => {
      const ammo = getAmmo(db, defName);
      return ammo ? { ok: true, ammo } : fail(`Unknown ammo def: ${defName}`);
    },
  },
  {
    name: 'revert_ammo',
    description: 'Discard database edits for one ammo def. Touches no files.',
    inputSchema: defNameArg,
    handler: (db, { defName }) => {
      const ammo = revertAmmo(db, defName);
      return ammo ? { ok: true, defName, reverted: true } : fail(`Unknown ammo def: ${defName}`);
    },
  },
];

export const TOOL_BY_NAME = new Map(TOOLS.map((t) => [t.name, t]));

/** Whether the database still needs a first extraction, and why. */
export function extractionState(db) {
  const turrets = db.get('SELECT COUNT(*) AS n FROM turrets').n;
  const ammo = db.get('SELECT COUNT(*) AS n FROM ammo').n;
  const pending = pendingEdits(db);

  if (!turrets) return { needed: true, reason: 'empty database' };
  if (!getMeta(db, 'last_extraction')) return { needed: true, reason: 'no extraction recorded' };
  // The shipped database predates the ammo pipeline; refresh it, but never over
  // the top of work that has not been injected yet.
  if (!ammo && !pending) return { needed: true, reason: 'no ammo rows' };
  return { needed: false, turrets, ammo, pending };
}
