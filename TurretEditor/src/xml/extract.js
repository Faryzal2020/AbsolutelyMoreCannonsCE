import fs from 'node:fs';
import path from 'node:path';
import { DefIndex, findXmlFiles } from './defIndex.js';
import { BUILDINGS_DIR, DEFS_DIR, TEXTURES_DIR, MOD_ROOT, rel } from '../paths.js';
import { FIELDS, coerce, setPath, getPath } from './fieldMap.js';
import { liTexts } from './tree.js';

const MANNED_PARENTS = new Set(['AMCTurretMannedBase', 'AMCArtilleryBase']);
const AUTO_PARENTS = new Set(['AMCTurretAutoBase', 'AMCArtilleryAutoBase']);

function stepMatches(node, step) {
  if (node.tag !== step.tag) return false;
  if (step.cls && !(node.attrs.Class || '').includes(step.cls)) return false;
  if (step.match) {
    const t = node.children.find((k) => k.tag === step.match.tag);
    if (!t || !String(t.text).includes(step.match.contains)) return false;
  }
  return true;
}

/**
 * Resolve a field path against a merged tree, returning every match in document
 * order. Parent-def children are merged in first, so the *last* match is the
 * value the game would actually use.
 */
function resolveAll(node, pathSteps) {
  if (pathSteps.length === 0) return [node];
  const [step, ...rest] = pathSteps;
  const out = [];
  for (const c of node.children) {
    if (stepMatches(c, step)) out.push(...resolveAll(c, rest));
  }
  return out;
}

function resolveOne(node, pathSteps) {
  const all = resolveAll(node, pathSteps);
  return all.length ? all[all.length - 1] : null;
}

function hasElement(node, pathSteps) {
  return resolveAll(node, pathSteps).length > 0;
}

function textureExists(texPath) {
  if (!texPath) return true;
  // A tree without a Textures folder (e.g. a test fixture) should not report
  // every single path as missing.
  if (!fs.existsSync(TEXTURES_DIR)) return true;
  // Vanilla / Core textures live outside this mod's Textures folder.
  if (texPath.startsWith('Things/Building/Security/') || texPath.startsWith('Things/Item/')) return true;
  const p = path.join(TEXTURES_DIR, ...texPath.split('/'));
  return fs.existsSync(`${p}.png`) || fs.existsSync(`${p}.jpg`);
}

function categoryOf(absPath) {
  const dir = path.dirname(absPath);
  if (path.resolve(dir) === path.resolve(BUILDINGS_DIR)) return 'Root';
  return path.basename(dir);
}

/** Skeleton with every branch present, so the UI never has to null-guard. */
function blankTurret() {
  return {
    defName: '', label: '', parentName: '', ancestry: [], category: '', filePath: '',
    designatorDropdown: '', size: '', mode: 'direct',
    weaponDefName: '', weaponFilePath: '', weaponLabel: '',
    stats: { maxHitPoints: null, workToBuild: null, mass: null, bulk: null, cooldownTime: null, topDrawSize: null, constructionSkill: null },
    costs: [],
    comps: {
      isManned: false, isPowered: false, powerWatts: null,
      hasFcs: false, hasPreserveAmmo: false, hasSuppressionImmunity: false, hasFireArc: false,
      hasModeSwap: false, swapAltDef: '', swapGizmoLabel: '',
      accuracy: { enabled: false, swayReduction: null, recoilReduction: null, spreadReduction: null },
      enclosed: {
        enabled: false, bulletProtection: null, explosiveProtection: null, flameProtection: null,
        meleeProtection: null, temperatureProtection: null, generalProtection: null,
        hidePawnGraphics: true, centerPawnPosition: true,
      },
    },
    ballistics: {
      verbClass: '', minRange: null, maxRange: null, burstShotCount: null,
      ticksBetween: null, warmupTime: null,
      sightsEfficiency: null, shotSpread: null, swayFactor: null, cooldown: null,
    },
    ammo: { ammoSet: '', magazineSize: null, reloadTime: null },
    barrel: {
      enabled: false, drawSize: null, offset: '', drawOnTop: false,
      barrelAmount: null, barrelSpacing: null, sequentialFiring: false,
      selectableBursts: { enabled: false, counts: '' },
      maxRPMs: '',
      recoil: { enabled: false, maxDistance: null, recoilDuration: null, returnDuration: null, useRecoilCurve: false, useReturnCurve: false, affectsRotation: false },
      firing: { enabled: false, durationTicks: null, drawFlash: false, flashColor: '', flashSize: null, flashBrightness: null, projectileSpawnOffset: null, burstSound: '' },
      spinning: { enabled: false, animationMode: 'RPMBased', maxRPM: null, spindownTime: null, frameCount: null, barrelCount: null, spinUpSound: '', spinDownSound: '' },
    },
    smoker: {
      enabled: false,
      muzzle: { enabled: false, fleckDef: '', particleCount: null, velocity: null, particleSize: '' },
      heat: { enabled: false, fleckDef: '', threshold: null, decayRate: null, emissionRate: null },
      shockwave: { enabled: false, fleckDef: '', radius: null, density: null },
    },
    textures: { building: '', icon: '', weapon: '' },
    warnings: [],
  };
}

/** Read every fieldMap entry for one document into the normalised object. */
function readFields(target, mergedBuilding, mergedWeapon) {
  for (const field of FIELDS) {
    const root = field.doc === 'weapon' ? mergedWeapon : mergedBuilding;
    if (!root) continue;
    const node = resolveOne(root, field.path);
    if (!node) continue;
    const raw = field.type === 'list' ? liTexts(node).join(', ') : node.text;
    const value = coerce(field.type, raw, getPath(target, field.key));
    setPath(target, field.key, value);
  }
}

function extractCosts(merged) {
  const rows = [];
  const seen = new Set();
  for (let i = merged.children.length - 1; i >= 0; i--) {
    const c = merged.children[i];
    if (c.tag !== 'costList') continue;
    for (const item of c.children) {
      if (seen.has(item.tag)) continue;
      seen.add(item.tag);
      rows.push({ thingDef: item.tag, count: parseInt(item.text, 10) || 0 });
    }
    break; // the last costList wins outright; RimWorld does not merge them
  }
  return rows;
}

/**
 * Rule checker. Takes a `hasDef` predicate rather than the whole index so the
 * same rules can re-run against an edited record without re-scanning the mod.
 */
export function auditTurret(t, hasDef = () => true) {
  const w = [];
  const num = (v) => (v === null || v === undefined ? null : Number(v));

  if (num(t.stats.maxHitPoints) !== null && num(t.stats.maxHitPoints) <= 0) {
    w.push({ level: 'error', rule: 'hp', message: `Max Hit Points must be greater than 0 (found ${t.stats.maxHitPoints}).` });
  }
  if (num(t.stats.workToBuild) !== null && num(t.stats.workToBuild) < 0) {
    w.push({ level: 'error', rule: 'work', message: `Work To Build cannot be negative (found ${t.stats.workToBuild}).` });
  }
  const min = num(t.ballistics.minRange), max = num(t.ballistics.maxRange);
  if (min !== null && max !== null && max > 0 && min > max) {
    w.push({ level: 'error', rule: 'range', message: `Min Range (${min}) exceeds Max Range (${max}).` });
  }
  if (t.parentName && !hasDef(t.parentName)) {
    w.push({ level: 'error', rule: 'parent', message: `Parent def "${t.parentName}" is not defined anywhere in Common/Defs.` });
  }
  if (t.weaponDefName && !hasDef(t.weaponDefName)) {
    w.push({ level: 'error', rule: 'weapon', message: `turretGunDef "${t.weaponDefName}" does not exist.` });
  }
  if (t.comps.hasModeSwap && (!t.comps.swapAltDef || !hasDef(t.comps.swapAltDef))) {
    w.push({ level: 'error', rule: 'modeswap', message: `Mode swap target "${t.comps.swapAltDef || '(empty)'}" does not exist.` });
  }
  for (const c of t.costs) {
    // Resource defs such as Steel and ComponentIndustrial come from Core, so
    // an unresolved name here is normal; only structurally invalid rows matter.
    if (!c.thingDef) w.push({ level: 'error', rule: 'cost', message: 'Cost row has an empty ThingDef name.' });
    if (!Number.isFinite(c.count) || c.count <= 0) w.push({ level: 'error', rule: 'cost', message: `Cost "${c.thingDef}" must have a positive count (found ${c.count}).` });
  }
  if (!textureExists(t.textures.building)) w.push({ level: 'warn', rule: 'texture', message: `Missing building texture: ${t.textures.building}` });
  if (!textureExists(t.textures.icon)) w.push({ level: 'warn', rule: 'texture', message: `Missing menu icon: ${t.textures.icon}` });
  if (t.textures.weapon && !textureExists(t.textures.weapon)) w.push({ level: 'warn', rule: 'texture', message: `Missing gun texture: ${t.textures.weapon}` });

  const unmanned = t.category.toLowerCase() === 'unmanned' || t.ancestry.some((a) => AUTO_PARENTS.has(a));
  if (unmanned) {
    if (t.comps.isManned) w.push({ level: 'error', rule: 'unmanned', message: 'Unmanned turret cannot carry CompProperties_Mannable.' });
    if (!t.comps.isPowered) w.push({ level: 'warn', rule: 'unmanned', message: 'Unmanned turret has no power consumption comp.' });
    if (!t.comps.hasFcs) w.push({ level: 'warn', rule: 'unmanned', message: 'Unmanned turret has no FCS comp.' });
  }
  if (t.barrel.spinning.enabled && !(Number(t.barrel.spinning.maxRPM) > 0) && !t.barrel.maxRPMs) {
    w.push({ level: 'warn', rule: 'rotary', message: 'Rotary animation is enabled but no maxRPM / maxRPMs value is set.' });
  }
  return w;
}

function parseOne(entry, index, hasDef) {
  const { tree, file } = entry;
  const merged = index.merged(tree);
  const weaponDefName = resolveOne(merged, [{ tag: 'building' }, { tag: 'turretGunDef' }])?.text || '';
  if (!weaponDefName) return null; // not a turret

  const t = blankTurret();
  t.defName = tree.children.find((c) => c.tag === 'defName')?.text || '';
  t.parentName = tree.attrs.ParentName || '';
  t.ancestry = index.ancestry(tree);
  t.category = categoryOf(file);
  t.filePath = rel(file);
  t.designatorDropdown = resolveOne(merged, [{ tag: 'designatorDropdown' }])?.text || '';
  t.size = resolveOne(merged, [{ tag: 'size' }])?.text || '';
  t.weaponDefName = weaponDefName;
  t.textures.building = resolveOne(merged, [{ tag: 'graphicData' }, { tag: 'texPath' }])?.text || '';
  t.textures.icon = resolveOne(merged, [{ tag: 'uiIconPath' }])?.text || '';

  const weaponEntry = index.byDefName.get(weaponDefName) || null;
  const mergedWeapon = weaponEntry ? index.merged(weaponEntry.tree) : null;
  if (weaponEntry) {
    t.weaponFilePath = rel(weaponEntry.file);
    t.weaponLabel = resolveOne(mergedWeapon, [{ tag: 'label' }])?.text || '';
    t.textures.weapon = resolveOne(mergedWeapon, [{ tag: 'graphicData' }, { tag: 'texPath' }])?.text || '';
  }

  // Component / extension presence drives the badge pills and the edit gates.
  t.comps.isManned = hasElement(merged, [{ tag: 'comps' }, { tag: 'li', cls: 'CompProperties_Mannable' }])
    || t.ancestry.some((a) => MANNED_PARENTS.has(a));
  t.comps.isPowered = hasElement(merged, [{ tag: 'comps' }, { tag: 'li', cls: 'CompProperties_Power' }]);
  t.comps.hasFcs = hasElement(merged, [{ tag: 'comps' }, { tag: 'li', cls: 'CompProperties_TurretFCS' }]);
  t.comps.hasPreserveAmmo = hasElement(merged, [{ tag: 'comps' }, { tag: 'li', cls: 'CompProperties_TurretPreserveAmmo' }]);
  t.comps.hasFireArc = hasElement(merged, [{ tag: 'comps' }, { tag: 'li', cls: 'CompProperties_FireArc' }]);
  t.comps.hasModeSwap = hasElement(merged, [{ tag: 'comps' }, { tag: 'li', cls: 'CompProperties_TurretModeSwap' }]);
  t.comps.accuracy.enabled = hasElement(merged, [{ tag: 'comps' }, { tag: 'li', cls: 'CompProperties_AccuracyOverride' }]);
  t.comps.enclosed.enabled = hasElement(merged, [{ tag: 'comps' }, { tag: 'li', cls: 'CompProperties_EnclosedTurret' }]);
  t.comps.hasSuppressionImmunity = hasElement(merged, [{ tag: 'modExtensions' }, { tag: 'li', cls: 'TurretSuppressionImmunityExtension' }]);
  t.smoker.enabled = hasElement(merged, [{ tag: 'comps' }, { tag: 'li', cls: 'CompProperties_TurretSmoker' }]);
  t.barrel.enabled = hasElement(merged, [{ tag: 'modExtensions' }, { tag: 'li', cls: 'TurretBarrelExtension' }]);
  t.barrel.selectableBursts.enabled = hasElement(merged, [
    { tag: 'modExtensions' }, { tag: 'li', cls: 'TurretBarrelExtension' }, { tag: 'selectableBurstCounts' },
  ]);
  // Sub-animation blocks default to enabled when the block exists without an
  // explicit <enabled> flag, which is how the C# side reads them.
  t.barrel.recoil.enabled = hasElement(merged, [
    { tag: 'modExtensions' }, { tag: 'li', cls: 'TurretBarrelExtension' }, { tag: 'recoilAnimation' },
  ]);
  t.barrel.firing.enabled = hasElement(merged, [
    { tag: 'modExtensions' }, { tag: 'li', cls: 'TurretBarrelExtension' }, { tag: 'firingAnimation' },
  ]);
  t.barrel.spinning.enabled = hasElement(merged, [
    { tag: 'modExtensions' }, { tag: 'li', cls: 'TurretBarrelExtension' }, { tag: 'spinningAnimation' },
  ]);

  readFields(t, merged, mergedWeapon);
  t.costs = extractCosts(merged);

  t.label = t.label || t.defName;
  t.mode = /_indirect(_|$)/i.test(t.defName) || /Mortar/i.test(t.ballistics.verbClass) ? 'indirect' : 'direct';
  t.warnings = auditTurret(t, hasDef);
  return t;
}

/**
 * Parse the whole mod and return normalised turret records.
 * Every def under Common/Defs is indexed (so parent / cost / mode-swap targets
 * can be validated), but only ThingDefs_Buildings is scanned for turrets.
 */
export function extractAll() {
  const index = new DefIndex().scan(DEFS_DIR);
  const turretFiles = new Set(findXmlFiles(BUILDINGS_DIR).map((f) => path.resolve(f)));
  const hasDef = (name) => index.byDefName.has(name) || index.byName.has(name);

  const turrets = [];
  for (const [defName, entry] of index.byDefName) {
    if (!turretFiles.has(path.resolve(entry.file))) continue;
    if (entry.tree.tag !== 'ThingDef') continue;
    try {
      const t = parseOne(entry, index, hasDef);
      if (t) turrets.push(t);
    } catch (err) {
      index.malformed.push({ file: entry.file, error: `Failed to parse def ${defName}: ${err.message}` });
    }
  }

  turrets.sort((a, b) => (a.category.localeCompare(b.category) || a.label.localeCompare(b.label)));

  // Link dual-mode partners in both directions so the UI can offer parallel edit.
  const byDef = new Map(turrets.map((t) => [t.defName, t]));
  for (const t of turrets) {
    t.linkedModeDef = t.comps.swapAltDef && byDef.has(t.comps.swapAltDef) ? t.comps.swapAltDef : '';
  }

  const files = new Map();
  for (const t of turrets) {
    for (const p of [t.filePath, t.weaponFilePath]) {
      if (!p || files.has(p)) continue;
      const info = index.files.get(path.resolve(MOD_ROOT, ...p.split('/')));
      if (info) files.set(p, info.checksum);
    }
  }

  return {
    turrets,
    files,
    malformed: index.malformed,
    index,
    knownDefs: [...index.byDefName.keys(), ...index.byName.keys()],
  };
}
