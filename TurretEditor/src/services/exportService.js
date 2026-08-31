/**
 * Spreadsheet export.
 *
 * The dashboard and the API share one column catalogue, so a CSV column and the
 * matching Excel column always carry the same header and the same value.
 * Headers are written for a human reading a spreadsheet — full words, units in
 * the header rather than the cell — not as raw JSON keys.
 */

import { listTurrets } from './turretService.js';
import { auditReport } from './auditService.js';
import { getMeta } from '../db.js';
import { buildWorkbook } from '../xlsx.js';

const yesNo = (v) => (v ? 'Yes' : 'No');
const verbShort = (v) => String(v || '').split('.').pop();
const costText = (costs) => costs.map((c) => `${c.thingDef} x${c.count}`).join('; ');

/** Column definitions for the main turret sheet, grouped for the UI preview. */
const TURRET_COLUMNS = [
  // Identity
  { key: 'defName', header: 'Def Name', group: 'Identity', width: 30, summary: true, get: (t) => t.defName },
  { key: 'label', header: 'Label', group: 'Identity', width: 28, summary: true, get: (t) => t.label },
  { key: 'category', header: 'Category', group: 'Identity', width: 15, summary: true, get: (t) => t.category },
  { key: 'mode', header: 'Fire Mode', group: 'Identity', width: 11, summary: true, get: (t) => (t.mode === 'indirect' ? 'Indirect' : 'Direct') },
  { key: 'parentName', header: 'Parent Def', group: 'Identity', width: 24, summary: true, get: (t) => t.parentName },
  { key: 'swapAltDef', header: 'Alternate Mode Def', group: 'Identity', width: 32, summary: true, get: (t) => t.comps.swapAltDef },
  { key: 'designator', header: 'Designator Group', group: 'Identity', width: 26, get: (t) => t.designatorDropdown },
  { key: 'weaponDefName', header: 'Weapon Def', group: 'Identity', width: 32, summary: true, get: (t) => t.weaponDefName },
  { key: 'size', header: 'Building Size', group: 'Identity', width: 13, get: (t) => t.size },

  // Gameplay stats
  { key: 'maxHitPoints', header: 'Max Hit Points', group: 'Gameplay Stats', width: 14, type: 'number', summary: true, get: (t) => t.stats.maxHitPoints },
  { key: 'workToBuild', header: 'Work To Build', group: 'Gameplay Stats', width: 14, type: 'number', summary: true, get: (t) => t.stats.workToBuild },
  { key: 'mass', header: 'Mass (kg)', group: 'Gameplay Stats', width: 11, type: 'number', summary: true, get: (t) => t.stats.mass },
  { key: 'bulk', header: 'Bulk', group: 'Gameplay Stats', width: 9, type: 'number', summary: true, get: (t) => t.stats.bulk },
  { key: 'cooldownTime', header: 'Turret Cooldown (s)', group: 'Gameplay Stats', width: 17, type: 'number', summary: true, get: (t) => t.stats.cooldownTime },
  { key: 'topDrawSize', header: 'Turret Top Draw Size', group: 'Gameplay Stats', width: 18, type: 'number', get: (t) => t.stats.topDrawSize },
  { key: 'constructionSkill', header: 'Construction Skill', group: 'Gameplay Stats', width: 16, type: 'number', get: (t) => t.stats.constructionSkill },
  { key: 'costList', header: 'Resource Costs', group: 'Gameplay Stats', width: 46, summary: true, get: (t) => costText(t.costs) },

  // Ballistics
  { key: 'verbClass', header: 'Verb Class', group: 'Firing & Ballistics', width: 20, summary: true, get: (t) => verbShort(t.ballistics.verbClass) },
  { key: 'minRange', header: 'Min Range (cells)', group: 'Firing & Ballistics', width: 15, type: 'number', summary: true, get: (t) => t.ballistics.minRange },
  { key: 'maxRange', header: 'Max Range (cells)', group: 'Firing & Ballistics', width: 15, type: 'number', summary: true, get: (t) => t.ballistics.maxRange },
  { key: 'burstShotCount', header: 'Burst Shot Count', group: 'Firing & Ballistics', width: 15, type: 'number', summary: true, get: (t) => t.ballistics.burstShotCount },
  { key: 'ticksBetween', header: 'Ticks Between Shots', group: 'Firing & Ballistics', width: 17, type: 'number', get: (t) => t.ballistics.ticksBetween },
  { key: 'warmupTime', header: 'Warmup Time (s)', group: 'Firing & Ballistics', width: 14, type: 'number', get: (t) => t.ballistics.warmupTime },
  { key: 'sightsEfficiency', header: 'Sights Efficiency', group: 'Firing & Ballistics', width: 15, type: 'number', get: (t) => t.ballistics.sightsEfficiency },
  { key: 'shotSpread', header: 'Shot Spread', group: 'Firing & Ballistics', width: 12, type: 'number', get: (t) => t.ballistics.shotSpread },
  { key: 'swayFactor', header: 'Sway Factor', group: 'Firing & Ballistics', width: 12, type: 'number', get: (t) => t.ballistics.swayFactor },
  { key: 'weaponCooldown', header: 'Weapon Cooldown (s)', group: 'Firing & Ballistics', width: 17, type: 'number', get: (t) => t.ballistics.cooldown },

  // Ammunition
  { key: 'ammoSet', header: 'AmmoSet', group: 'Ammunition', width: 32, summary: true, get: (t) => t.ammo.ammoSet },
  { key: 'magazineSize', header: 'Magazine Size', group: 'Ammunition', width: 13, type: 'number', summary: true, get: (t) => t.ammo.magazineSize },
  { key: 'reloadTime', header: 'Reload Time (s)', group: 'Ammunition', width: 14, type: 'number', summary: true, get: (t) => t.ammo.reloadTime },

  // Comps
  { key: 'isManned', header: 'Manned', group: 'Comps & FCS', width: 9, summary: true, get: (t) => yesNo(t.comps.isManned) },
  { key: 'isPowered', header: 'Powered', group: 'Comps & FCS', width: 9, get: (t) => yesNo(t.comps.isPowered) },
  { key: 'powerWatts', header: 'Power Draw (W)', group: 'Comps & FCS', width: 14, type: 'number', summary: true, get: (t) => t.comps.powerWatts },
  { key: 'hasFcs', header: 'FCS Comp', group: 'Comps & FCS', width: 10, summary: true, get: (t) => yesNo(t.comps.hasFcs) },
  { key: 'hasModeSwap', header: 'Mode Swap', group: 'Comps & FCS', width: 11, get: (t) => yesNo(t.comps.hasModeSwap) },
  { key: 'hasPreserveAmmo', header: 'Preserve Ammo', group: 'Comps & FCS', width: 14, get: (t) => yesNo(t.comps.hasPreserveAmmo) },
  { key: 'hasFireArc', header: 'Fire Arc', group: 'Comps & FCS', width: 10, get: (t) => yesNo(t.comps.hasFireArc) },
  { key: 'hasSuppressionImmunity', header: 'Suppression Immunity', group: 'Comps & FCS', width: 18, get: (t) => yesNo(t.comps.hasSuppressionImmunity) },
  { key: 'accuracyOverride', header: 'Accuracy Override', group: 'Comps & FCS', width: 16, get: (t) => yesNo(t.comps.accuracy.enabled) },
  { key: 'swayReduction', header: 'Sway Reduction', group: 'Comps & FCS', width: 14, type: 'number', get: (t) => t.comps.accuracy.swayReduction },
  { key: 'recoilReduction', header: 'Recoil Reduction', group: 'Comps & FCS', width: 15, type: 'number', get: (t) => t.comps.accuracy.recoilReduction },
  { key: 'spreadReduction', header: 'Spread Reduction', group: 'Comps & FCS', width: 15, type: 'number', get: (t) => t.comps.accuracy.spreadReduction },
  { key: 'enclosedTurret', header: 'Enclosed Turret', group: 'Comps & FCS', width: 15, summary: true, get: (t) => yesNo(t.comps.enclosed?.enabled) },
  { key: 'bulletProtection', header: 'Bullet Protection', group: 'Comps & FCS', width: 15, type: 'number', get: (t) => t.comps.enclosed?.bulletProtection },
  { key: 'explosiveProtection', header: 'Explosive Protection', group: 'Comps & FCS', width: 17, type: 'number', get: (t) => t.comps.enclosed?.explosiveProtection },
  { key: 'temperatureProtection', header: 'Temp Protection', group: 'Comps & FCS', width: 15, type: 'number', get: (t) => t.comps.enclosed?.temperatureProtection },
  { key: 'hidePawnGraphics', header: 'Hide Pawn Graphics', group: 'Comps & FCS', width: 16, get: (t) => yesNo(t.comps.enclosed?.hidePawnGraphics) },

  // Barrel
  { key: 'barrelExtension', header: 'Barrel Extension', group: 'Barrel & Animations', width: 15, get: (t) => yesNo(t.barrel.enabled) },
  { key: 'barrelDrawSize', header: 'Barrel Draw Size', group: 'Barrel & Animations', width: 15, type: 'number', get: (t) => t.barrel.drawSize },
  { key: 'barrelOffset', header: 'Barrel Offset', group: 'Barrel & Animations', width: 15, get: (t) => t.barrel.offset },
  { key: 'barrelAmount', header: 'Barrel Count', group: 'Barrel & Animations', width: 12, type: 'number', get: (t) => t.barrel.barrelAmount },
  { key: 'barrelSpacing', header: 'Barrel Spacing', group: 'Barrel & Animations', width: 13, type: 'number', get: (t) => t.barrel.barrelSpacing },
  { key: 'drawOnTop', header: 'Draw On Top', group: 'Barrel & Animations', width: 12, get: (t) => yesNo(t.barrel.drawOnTop) },
  { key: 'sequentialFiring', header: 'Sequential Fire', group: 'Barrel & Animations', width: 14, get: (t) => yesNo(t.barrel.sequentialFiring) },
  { key: 'selectableBursts', header: 'Selectable Bursts', group: 'Barrel & Animations', width: 16, get: (t) => yesNo(t.barrel.selectableBursts.enabled) },
  { key: 'burstCounts', header: 'Burst Counts', group: 'Barrel & Animations', width: 16, summary: true, get: (t) => t.barrel.selectableBursts.counts },
  { key: 'maxRPMs', header: 'Max RPM Steps', group: 'Barrel & Animations', width: 15, get: (t) => t.barrel.maxRPMs },

  { key: 'recoilAnim', header: 'Recoil Animation', group: 'Recoil Animation', width: 15, summary: true, get: (t) => yesNo(t.barrel.recoil.enabled) },
  { key: 'recoilMaxDistance', header: 'Recoil Max Distance', group: 'Recoil Animation', width: 17, type: 'number', get: (t) => t.barrel.recoil.maxDistance },
  { key: 'recoilDuration', header: 'Recoil Duration (ticks)', group: 'Recoil Animation', width: 20, type: 'number', get: (t) => t.barrel.recoil.recoilDuration },
  { key: 'returnDuration', header: 'Return Duration (ticks)', group: 'Recoil Animation', width: 20, type: 'number', get: (t) => t.barrel.recoil.returnDuration },
  { key: 'useRecoilCurve', header: 'Recoil Curve', group: 'Recoil Animation', width: 12, get: (t) => yesNo(t.barrel.recoil.useRecoilCurve) },
  { key: 'useReturnCurve', header: 'Return Curve', group: 'Recoil Animation', width: 12, get: (t) => yesNo(t.barrel.recoil.useReturnCurve) },
  { key: 'affectsRotation', header: 'Rotation Impact', group: 'Recoil Animation', width: 14, get: (t) => yesNo(t.barrel.recoil.affectsRotation) },

  { key: 'firingAnim', header: 'Firing Flash', group: 'Firing Flash', width: 12, get: (t) => yesNo(t.barrel.firing.enabled) },
  { key: 'flashDuration', header: 'Flash Duration (ticks)', group: 'Firing Flash', width: 19, type: 'number', get: (t) => t.barrel.firing.durationTicks },
  { key: 'drawFlash', header: 'Draw Muzzle Flash', group: 'Firing Flash', width: 16, get: (t) => yesNo(t.barrel.firing.drawFlash) },
  { key: 'flashColor', header: 'Flash Color (RGBA)', group: 'Firing Flash', width: 18, get: (t) => t.barrel.firing.flashColor },
  { key: 'flashSize', header: 'Flash Size', group: 'Firing Flash', width: 11, type: 'number', get: (t) => t.barrel.firing.flashSize },
  { key: 'flashBrightness', header: 'Flash Brightness', group: 'Firing Flash', width: 15, type: 'number', get: (t) => t.barrel.firing.flashBrightness },
  { key: 'projectileSpawnOffset', header: 'Projectile Spawn Offset', group: 'Firing Flash', width: 20, type: 'number', get: (t) => t.barrel.firing.projectileSpawnOffset },
  { key: 'burstSound', header: 'Burst SoundDef', group: 'Firing Flash', width: 24, get: (t) => t.barrel.firing.burstSound },

  { key: 'rotaryAnim', header: 'Rotary Animation', group: 'Rotary Animation', width: 15, summary: true, get: (t) => yesNo(t.barrel.spinning.enabled) },
  { key: 'rotaryMode', header: 'Rotary Mode', group: 'Rotary Animation', width: 13, get: (t) => (t.barrel.spinning.enabled ? t.barrel.spinning.animationMode : '') },
  { key: 'maxRPM', header: 'Max RPM', group: 'Rotary Animation', width: 11, type: 'number', get: (t) => t.barrel.spinning.maxRPM },
  { key: 'spindownTime', header: 'Spindown Time (s)', group: 'Rotary Animation', width: 16, type: 'number', get: (t) => t.barrel.spinning.spindownTime },
  { key: 'frameCount', header: 'Frame Count', group: 'Rotary Animation', width: 12, type: 'number', get: (t) => t.barrel.spinning.frameCount },
  { key: 'rotaryBarrelCount', header: 'Rotary Barrel Count', group: 'Rotary Animation', width: 17, type: 'number', get: (t) => t.barrel.spinning.barrelCount },
  { key: 'spinUpSound', header: 'Spin Up SoundDef', group: 'Rotary Animation', width: 24, get: (t) => t.barrel.spinning.spinUpSound },
  { key: 'spinDownSound', header: 'Spin Down SoundDef', group: 'Rotary Animation', width: 24, get: (t) => t.barrel.spinning.spinDownSound },

  // Smoke
  { key: 'smokerComp', header: 'Smoker Comp', group: 'Smoke & Particles', width: 12, get: (t) => yesNo(t.smoker.enabled) },
  { key: 'muzzleSmoke', header: 'Muzzle Smoke', group: 'Smoke & Particles', width: 13, get: (t) => yesNo(t.smoker.muzzle.enabled) },
  { key: 'muzzleFleck', header: 'Muzzle FleckDef', group: 'Smoke & Particles', width: 20, get: (t) => t.smoker.muzzle.fleckDef },
  { key: 'muzzleParticleCount', header: 'Muzzle Particle Count', group: 'Smoke & Particles', width: 19, type: 'number', get: (t) => t.smoker.muzzle.particleCount },
  { key: 'muzzleVelocity', header: 'Muzzle Velocity (cells/s)', group: 'Smoke & Particles', width: 21, type: 'number', get: (t) => t.smoker.muzzle.velocity },
  { key: 'muzzleSize', header: 'Muzzle Size Multiplier', group: 'Smoke & Particles', width: 19, get: (t) => t.smoker.muzzle.particleSize },
  { key: 'heatSmoke', header: 'Heat Smoke', group: 'Smoke & Particles', width: 11, get: (t) => yesNo(t.smoker.heat.enabled) },
  { key: 'heatFleck', header: 'Heat FleckDef', group: 'Smoke & Particles', width: 20, get: (t) => t.smoker.heat.fleckDef },
  { key: 'heatThreshold', header: 'Heat Threshold (shots)', group: 'Smoke & Particles', width: 19, type: 'number', get: (t) => t.smoker.heat.threshold },
  { key: 'heatDecayRate', header: 'Heat Decay Rate (/s)', group: 'Smoke & Particles', width: 18, type: 'number', get: (t) => t.smoker.heat.decayRate },
  { key: 'heatEmissionRate', header: 'Heat Emission Rate (/s)', group: 'Smoke & Particles', width: 20, type: 'number', get: (t) => t.smoker.heat.emissionRate },
  { key: 'shockwave', header: 'Shockwave', group: 'Smoke & Particles', width: 11, get: (t) => yesNo(t.smoker.shockwave.enabled) },
  { key: 'shockwaveFleck', header: 'Shockwave FleckDef', group: 'Smoke & Particles', width: 22, get: (t) => t.smoker.shockwave.fleckDef },
  { key: 'shockwaveRadius', header: 'Shockwave Radius (cells)', group: 'Smoke & Particles', width: 21, type: 'number', get: (t) => t.smoker.shockwave.radius },
  { key: 'shockwaveDensity', header: 'Shockwave Density', group: 'Smoke & Particles', width: 17, type: 'number', get: (t) => t.smoker.shockwave.density },

  // Status
  { key: 'modified', header: 'Modified', group: 'Status & Files', width: 10, summary: true, get: (t) => yesNo(t.modified) },
  { key: 'errorCount', header: 'Audit Errors', group: 'Status & Files', width: 12, type: 'number', summary: true, get: (t) => t.warnings.filter((w) => w.level === 'error').length },
  { key: 'warningCount', header: 'Audit Warnings', group: 'Status & Files', width: 14, type: 'number', summary: true, get: (t) => t.warnings.filter((w) => w.level === 'warn').length },
  { key: 'buildingTexture', header: 'Building Texture', group: 'Status & Files', width: 34, get: (t) => t.textures.building },
  { key: 'menuIcon', header: 'Menu Icon', group: 'Status & Files', width: 34, get: (t) => t.textures.icon },
  { key: 'weaponTexture', header: 'Weapon Texture', group: 'Status & Files', width: 34, get: (t) => t.textures.weapon },
  { key: 'filePath', header: 'Building Def File', group: 'Status & Files', width: 52, summary: true, get: (t) => t.filePath },
  { key: 'weaponFilePath', header: 'Weapon Def File', group: 'Status & Files', width: 52, get: (t) => t.weaponFilePath },
];

const COST_COLUMNS = [
  { key: 'defName', header: 'Def Name', width: 30 },
  { key: 'label', header: 'Label', width: 28 },
  { key: 'category', header: 'Category', width: 15 },
  { key: 'mode', header: 'Fire Mode', width: 11 },
  { key: 'thingDef', header: 'Resource ThingDef', width: 24 },
  { key: 'count', header: 'Count', width: 10, type: 'number' },
];

const AUDIT_COLUMNS = [
  { key: 'defName', header: 'Def Name', width: 30 },
  { key: 'label', header: 'Label', width: 28 },
  { key: 'category', header: 'Category', width: 15 },
  { key: 'severity', header: 'Severity', width: 11 },
  { key: 'rule', header: 'Rule', width: 14 },
  { key: 'message', header: 'Finding', width: 74 },
  { key: 'filePath', header: 'Def File', width: 52 },
];

export const DATASETS = {
  turrets: { id: 'turrets', name: 'Turret Systems', label: 'Turret Matrix (full)', file: 'amc_turret_matrix' },
  summary: { id: 'summary', name: 'Turret Summary', label: 'Turret Matrix (summary)', file: 'amc_turret_summary' },
  costs: { id: 'costs', name: 'Resource Costs', label: 'Resource Costs', file: 'amc_turret_costs' },
  audit: { id: 'audit', name: 'Audit Findings', label: 'Audit Findings', file: 'amc_audit_findings' },
};

const strip = (cols) => cols.map(({ key, header, width, type, group }) => ({ key, header, width, type, group }));

function selectTurrets(db, scope) {
  const all = listTurrets(db);
  return scope === 'modified' ? all.filter((t) => t.modified) : all;
}

/**
 * Build one sheet: `{ name, columns, rows }`, ready for CSV or xlsx rendering.
 * @param {object} db
 * @param {'turrets'|'summary'|'costs'|'audit'} dataset
 * @param {{scope?:'all'|'modified'}} options
 */
export function buildSheet(db, dataset, { scope = 'all' } = {}) {
  const meta = DATASETS[dataset];
  if (!meta) throw new Error(`Unknown dataset: ${dataset}`);

  if (dataset === 'costs') {
    const rows = [];
    for (const t of selectTurrets(db, scope)) {
      for (const c of t.costs) {
        rows.push({
          defName: t.defName, label: t.label, category: t.category,
          mode: t.mode === 'indirect' ? 'Indirect' : 'Direct',
          thingDef: c.thingDef, count: c.count,
        });
      }
    }
    return { name: meta.name, columns: COST_COLUMNS, rows };
  }

  if (dataset === 'audit') {
    const report = auditReport(db);
    const wanted = new Set(selectTurrets(db, scope).map((t) => t.defName));
    const rows = [];
    for (const entry of report.entries) {
      if (!wanted.has(entry.defName)) continue;
      for (const issue of entry.issues) {
        rows.push({
          defName: entry.defName, label: entry.label, category: entry.category,
          severity: issue.level === 'error' ? 'Error' : 'Warning',
          rule: issue.rule, message: issue.message, filePath: entry.filePath,
        });
      }
    }
    return { name: meta.name, columns: AUDIT_COLUMNS, rows };
  }

  const columns = dataset === 'summary' ? TURRET_COLUMNS.filter((c) => c.summary) : TURRET_COLUMNS;
  const rows = selectTurrets(db, scope).map((t) => {
    const row = {};
    for (const col of columns) row[col.key] = col.get(t);
    return row;
  });
  return { name: meta.name, columns: strip(columns), rows };
}

/** RFC 4180 CSV. Excel needs a BOM to read UTF-8 def names correctly. */
export function toCsv(sheet, { bom = true } = {}) {
  const cell = (v) => {
    if (v === null || v === undefined) return '';
    const s = String(v);
    return /[",\r\n]/.test(s) ? `"${s.replace(/"/g, '""')}"` : s;
  };
  const lines = [sheet.columns.map((c) => cell(c.header)).join(',')];
  for (const row of sheet.rows) lines.push(sheet.columns.map((c) => cell(row[c.key])).join(','));
  return (bom ? '﻿' : '') + lines.join('\r\n') + '\r\n';
}

/** Every dataset as one multi-sheet workbook. */
export function toWorkbook(db, { scope = 'all' } = {}) {
  return buildWorkbook([
    buildSheet(db, 'turrets', { scope }),
    buildSheet(db, 'costs', { scope }),
    buildSheet(db, 'audit', { scope }),
  ]);
}

/** Column catalogue for the export dialog's preview. */
export function describeColumns(dataset = 'turrets') {
  const sheet = { turrets: TURRET_COLUMNS, summary: TURRET_COLUMNS.filter((c) => c.summary), costs: COST_COLUMNS, audit: AUDIT_COLUMNS }[dataset];
  if (!sheet) return [];
  return strip(sheet);
}

export function exportMeta(db) {
  const turrets = listTurrets(db);
  return {
    lastExtraction: getMeta(db, 'last_extraction'),
    total: turrets.length,
    modified: turrets.filter((t) => t.modified).length,
    datasets: Object.values(DATASETS).map((d) => ({
      ...d,
      columnCount: describeColumns(d.id === 'summary' ? 'summary' : d.id).length,
    })),
  };
}
