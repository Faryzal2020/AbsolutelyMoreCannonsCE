import { listTurrets } from './turretService.js';
import { listBackups } from './injectService.js';
import { getMeta } from '../db.js';

/** Dashboard counters shown in the metrics panel. */
export function metrics(db) {
  const turrets = listTurrets(db);
  return {
    total: turrets.length,
    modeSwapCapable: turrets.filter((t) => t.comps.hasModeSwap).length,
    powered: turrets.filter((t) => t.comps.isPowered).length,
    animated: turrets.filter((t) => t.barrel.recoil.enabled || t.barrel.spinning.enabled).length,
    modified: turrets.filter((t) => t.modified).length,
    warnings: turrets.reduce((n, t) => n + t.warnings.length, 0),
    errors: turrets.reduce((n, t) => n + t.warnings.filter((w) => w.level === 'error').length, 0),
    categories: [...new Set(turrets.map((t) => t.category))].sort(),
    lastExtraction: getMeta(db, 'last_extraction'),
    backups: listBackups().length,
  };
}

/** Full validation report grouped by turret, plus rule totals. */
export function auditReport(db) {
  const turrets = listTurrets(db);
  const entries = turrets
    .filter((t) => t.warnings.length)
    .map((t) => ({
      defName: t.defName,
      label: t.label,
      category: t.category,
      filePath: t.filePath,
      modified: t.modified,
      errors: t.warnings.filter((w) => w.level === 'error').length,
      warnings: t.warnings.filter((w) => w.level === 'warn').length,
      issues: t.warnings,
    }))
    .sort((a, b) => b.errors - a.errors || a.label.localeCompare(b.label));

  const byRule = {};
  for (const t of turrets) {
    for (const w of t.warnings) {
      byRule[w.rule] = byRule[w.rule] || { rule: w.rule, errors: 0, warnings: 0 };
      byRule[w.rule][w.level === 'error' ? 'errors' : 'warnings'] += 1;
    }
  }

  return {
    ok: true,
    generatedAt: new Date().toISOString(),
    scanned: turrets.length,
    affected: entries.length,
    totals: {
      errors: entries.reduce((n, e) => n + e.errors, 0),
      warnings: entries.reduce((n, e) => n + e.warnings, 0),
    },
    byRule: Object.values(byRule).sort((a, b) => b.errors - a.errors),
    entries,
  };
}
