import fs from 'node:fs';
import path from 'node:path';
import { DefIndex, findXmlFiles } from './defIndex.js';
import { AMMO_DIR, DEFS_DIR, rel } from '../paths.js';
import { AMMO_FIELDS } from './fieldMapAmmo.js';
import { coerce, getPath, setPath } from './fieldMap.js';

function stepMatches(node, step) {
  if (node.tag !== step.tag) return false;
  if (step.cls && !(node.attrs.Class || '').includes(step.cls)) return false;
  if (step.match) {
    const t = node.children.find((k) => k.tag === step.match.tag);
    if (!t || !String(t.text).includes(step.match.contains)) return false;
  }
  return true;
}

function resolveAll(node, pathSteps) {
  if (!node) return [];
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

function categoryOfAmmo(defName, filePath) {
  if (defName.startsWith('Ammo_')) {
    const parts = defName.replace(/^Ammo_/, '').split('_');
    if (parts.length > 1) return parts[0];
  }
  return path.basename(filePath, '.xml');
}

export function blankAmmo() {
  return {
    defName: '', label: '', ammoFamily: '', ammoSetName: '', indirectAmmoSetName: '',
    ammoClass: '', filePath: '',
    hasDirectMode: true, hasIndirectMode: true,
    directBulletDef: '', indirectBulletDef: '',
    directFilePath: '', indirectFilePath: '',
    stats: { marketValue: null, mass: null, bulk: null, stackLimit: null, cookOffFlashScale: null },
    recipe: { defName: '', workAmount: null, yieldCount: null, ingredients: [] },
    payload: {
      damageDef: '', damageAmountBase: null, armorPenetrationSharp: null, armorPenetrationBlunt: null,
      explosionRadius: null, soundExplode: '', applyNeighbors: false, aimHeightOffset: null,
    },
    secondary: { enabled: false, damageAmountBase: null, explosiveDamageType: '', explosiveRadius: null },
    fragments: { enabled: false, list: [], fragAngleRange: '' },
    airburst: {
      enabled: false, armingTicks: null, type: '', proximityRadius: null, burstAltitude: null,
      fragments: [], fragAngleRange: '', fragXZAngleRange: '', useEllipticalCone: false,
      airburstSound: '', airburstFlashEffect: '', airburstFlashScale: null, airburstSmokeScale: null,
    },
    guidance: {
      enabled: false, guidanceOnDescending: false, homingAcceleration: null, retargetRadius: null,
      guidanceDelay: null, gravityFactor: null, trajectoryWorker: '',
    },
    direct: { speed: null, dropsCasings: false, soundImpactAnticipate: '' },
    indirect: { speed: null, flyOverhead: false, shellingTilesPerTick: null, shellingRange: null, shellingDamage: null, soundHitThickRoof: '' },
    textures: { ammo: '' },
    warnings: [],
  };
}

function extractRecipe(recipeNode, productDefName) {
  if (!recipeNode) return { defName: '', workAmount: null, yieldCount: null, ingredients: [] };
  const merged = recipeNode;
  const defName = merged.children.find((c) => c.tag === 'defName')?.text || '';
  const workAmount = parseInt(resolveOne(merged, [{ tag: 'workAmount' }])?.text, 10) || null;

  let yieldCount = 1;
  const products = resolveOne(merged, [{ tag: 'products' }]);
  if (products) {
    const prodTag = products.children.find((c) => c.tag === productDefName);
    if (prodTag) yieldCount = parseInt(prodTag.text, 10) || 1;
  }

  const ingredients = [];
  const ingNode = resolveOne(merged, [{ tag: 'ingredients' }]);
  if (ingNode) {
    for (const li of ingNode.children.filter((c) => c.tag === 'li')) {
      const thingDefs = resolveOne(li, [{ tag: 'filter' }, { tag: 'thingDefs' }]);
      const countNode = resolveOne(li, [{ tag: 'count' }]);
      if (thingDefs && countNode) {
        const thingDef = thingDefs.children.find((c) => c.tag)?.tag || '';
        const count = parseInt(countNode.text, 10) || 0;
        if (thingDef) ingredients.push({ thingDef, count });
      }
    }
  }

  return { defName, workAmount, yieldCount, ingredients };
}

function extractFragments(bulletMerged) {
  const comp = resolveOne(bulletMerged, [{ tag: 'comps' }, { tag: 'li', cls: 'CompProperties_Fragments' }]);
  if (!comp) return { enabled: false, list: [], fragAngleRange: '' };

  const fragNode = resolveOne(comp, [{ tag: 'fragments' }]);
  const list = [];
  if (fragNode) {
    for (const c of fragNode.children) {
      if (c.tag && c.text) list.push({ thingDef: c.tag, count: parseInt(c.text, 10) || 0 });
    }
  }
  const fragAngleRange = resolveOne(comp, [{ tag: 'fragAngleRange' }])?.text || '';
  return { enabled: true, list, fragAngleRange };
}

function extractAirburst(bulletMerged) {
  const comp = resolveOne(bulletMerged, [{ tag: 'comps' }, { tag: 'li', cls: 'CompProperties_AirburstFragments' }]);
  const ext = resolveOne(bulletMerged, [{ tag: 'modExtensions' }, { tag: 'li', cls: 'AirburstExtension' }]);
  if (!comp && !ext) return {
    enabled: false, armingTicks: null, type: '', proximityRadius: null, burstAltitude: null,
    fragments: [], fragAngleRange: '', fragXZAngleRange: '', useEllipticalCone: false,
    airburstSound: '', airburstFlashEffect: '', airburstFlashScale: null, airburstSmokeScale: null,
  };

  const armingTicks = parseInt(resolveOne(ext, [{ tag: 'armingTicks' }])?.text, 10) || null;
  const type = resolveOne(ext, [{ tag: 'type' }])?.text || '';
  const proximityRadius = parseFloat(resolveOne(ext, [{ tag: 'proximityRadius' }])?.text) || null;
  const burstAltitude = parseFloat(resolveOne(ext, [{ tag: 'burstAltitude' }])?.text) || null;

  const fragments = [];
  const fragNode = resolveOne(comp, [{ tag: 'fragments' }]);
  if (fragNode) {
    for (const li of fragNode.children.filter((c) => c.tag === 'li')) {
      const thingDef = resolveOne(li, [{ tag: 'thingDef' }])?.text || '';
      const count = parseInt(resolveOne(li, [{ tag: 'count' }])?.text, 10) || 0;
      const radius = parseFloat(resolveOne(li, [{ tag: 'explosionRadius' }])?.text) || null;
      if (thingDef) fragments.push({ thingDef, count, explosionRadius: radius });
    }
  }

  const fragAngleRange = resolveOne(comp, [{ tag: 'fragAngleRange' }])?.text || '';
  const fragXZAngleRange = resolveOne(comp, [{ tag: 'fragXZAngleRange' }])?.text || '';
  const useEllipticalCone = resolveOne(comp, [{ tag: 'useEllipticalCone' }])?.text === 'true';
  const airburstSound = resolveOne(comp, [{ tag: 'airburstSound' }])?.text || '';
  const airburstFlashEffect = resolveOne(comp, [{ tag: 'airburstFlashEffect' }])?.text || '';
  const airburstFlashScale = parseFloat(resolveOne(comp, [{ tag: 'airburstFlashScale' }])?.text) || null;
  const airburstSmokeScale = parseFloat(resolveOne(comp, [{ tag: 'airburstSmokeScale' }])?.text) || null;

  return {
    enabled: true, armingTicks, type, proximityRadius, burstAltitude,
    fragments, fragAngleRange, fragXZAngleRange, useEllipticalCone,
    airburstSound, airburstFlashEffect, airburstFlashScale, airburstSmokeScale,
  };
}

export function extractAllAmmo() {
  const index = new DefIndex().scan(DEFS_DIR);
  const ammoRecords = [];

  // Map productDefName -> RecipeDef merged tree
  const recipeMap = new Map();
  for (const [, entry] of index.byDefName) {
    if (entry.tree.tag === 'RecipeDef') {
      const merged = index.merged(entry.tree);
      const products = resolveOne(merged, [{ tag: 'products' }]);
      if (products) {
        for (const p of products.children) {
          recipeMap.set(p.tag, merged);
        }
      }
    }
  }

  // Find all AmmoDef definitions
  for (const [defName, entry] of index.byDefName) {
    const { tree, file } = entry;
    if (tree.tag !== 'ThingDef') continue;
    const isAmmo = (tree.attrs.Class || '').includes('AmmoDef')
      || (tree.attrs.ParentName || '').includes('AmmoBase')
      || (tree.attrs.ParentName || '').includes('Ammo');
    if (!isAmmo) continue;

    const mergedAmmo = index.merged(tree);
    const item = blankAmmo();
    item.defName = defName;
    item.label = resolveOne(mergedAmmo, [{ tag: 'label' }])?.text || defName;
    item.filePath = rel(file);
    item.ammoFamily = categoryOfAmmo(defName, file);
    item.ammoClass = resolveOne(mergedAmmo, [{ tag: 'ammoClass' }])?.text || '';
    item.textures.ammo = resolveOne(mergedAmmo, [{ tag: 'graphicData' }, { tag: 'texPath' }])?.text || '';

    // Direct / Indirect projectile defNames
    const directProj = resolveOne(mergedAmmo, [{ tag: 'detonateProjectile' }])?.text || '';

    // Find linked AmmoSets
    for (const [, setEntry] of index.byDefName) {
      if (setEntry.tree.tag.includes('AmmoSetDef')) {
        const setMerged = index.merged(setEntry.tree);
        const ammoTypes = resolveOne(setMerged, [{ tag: 'ammoTypes' }]);
        if (ammoTypes) {
          const linkedProj = resolveOne(ammoTypes, [{ tag: defName }])?.text;
          if (linkedProj) {
            const isIndirectSet = setEntry.tree.children.find((c) => c.tag === 'defName')?.text.includes('indirect');
            if (isIndirectSet) {
              item.indirectAmmoSetName = setEntry.tree.children.find((c) => c.tag === 'defName')?.text || '';
              item.indirectBulletDef = linkedProj;
            } else {
              item.ammoSetName = setEntry.tree.children.find((c) => c.tag === 'defName')?.text || '';
              item.directBulletDef = linkedProj;
            }
          }
        }
      }
    }

    if (!item.directBulletDef && directProj) item.directBulletDef = directProj;
    if (!item.indirectBulletDef && item.directBulletDef) {
      const indirectCand = item.directBulletDef.replace('Bullet_', 'Bullet_indirect_').replace('_indirect_indirect_', '_indirect_');
      if (index.byDefName.has(indirectCand)) item.indirectBulletDef = indirectCand;
      else {
        const cand2 = item.directBulletDef.replace(/Bullet_([^_]+)_/, 'Bullet_$1_indirect_');
        if (index.byDefName.has(cand2)) item.indirectBulletDef = cand2;
      }
    }

    item.hasDirectMode = Boolean(item.directBulletDef && index.byDefName.has(item.directBulletDef));
    item.hasIndirectMode = Boolean(item.indirectBulletDef && index.byDefName.has(item.indirectBulletDef));

    // Resolve projectile fields (Direct preferred, fallback to Indirect)
    const directEntry = item.directBulletDef ? index.byDefName.get(item.directBulletDef) : null;
    const indirectEntry = item.indirectBulletDef ? index.byDefName.get(item.indirectBulletDef) : null;

    if (directEntry) item.directFilePath = rel(directEntry.file);
    if (indirectEntry) item.indirectFilePath = rel(indirectEntry.file);

    const primaryProjEntry = directEntry || indirectEntry;
    const primaryProjMerged = primaryProjEntry ? index.merged(primaryProjEntry.tree) : null;

    // Read field values
    for (const field of AMMO_FIELDS) {
      let root = null;
      if (field.doc === 'ammo') root = mergedAmmo;
      else if (field.doc === 'shared_projectile') root = primaryProjMerged;
      else if (field.doc === 'direct_projectile') root = directEntry ? index.merged(directEntry.tree) : null;
      else if (field.doc === 'indirect_projectile') root = indirectEntry ? index.merged(indirectEntry.tree) : null;

      if (!root) continue;
      const node = resolveOne(root, field.path);
      if (!node) continue;
      const value = coerce(field.type, node.text, getPath(item, field.key));
      setPath(item, field.key, value);
    }

    // Secondary Explosive
    if (primaryProjMerged) {
      const secComp = resolveOne(primaryProjMerged, [{ tag: 'comps' }, { tag: 'li', cls: 'CompProperties_ExplosiveCE' }]);
      if (secComp) {
        item.secondary.enabled = true;
        item.secondary.damageAmountBase = parseInt(resolveOne(secComp, [{ tag: 'damageAmountBase' }])?.text, 10) || null;
        item.secondary.explosiveDamageType = resolveOne(secComp, [{ tag: 'explosiveDamageType' }])?.text || '';
        item.secondary.explosiveRadius = parseFloat(resolveOne(secComp, [{ tag: 'explosiveRadius' }])?.text) || null;
      }

      item.fragments = extractFragments(primaryProjMerged);
      item.airburst = extractAirburst(primaryProjMerged);

      const projTag = resolveOne(primaryProjMerged, [{ tag: 'projectile' }]);
      if (projTag) {
        const homing = resolveOne(projTag, [{ tag: 'homingAcceleration' }]);
        const desc = resolveOne(projTag, [{ tag: 'guidanceOnDescending' }]);
        item.guidance.enabled = Boolean(homing || desc);
      }
    }

    // Recipe
    const recipeMerged = recipeMap.get(defName);
    item.recipe = extractRecipe(recipeMerged, defName);

    ammoRecords.push(item);
  }

  ammoRecords.sort((a, b) => a.ammoFamily.localeCompare(b.ammoFamily) || a.label.localeCompare(b.label));

  return {
    ammo: ammoRecords,
    index,
  };
}
