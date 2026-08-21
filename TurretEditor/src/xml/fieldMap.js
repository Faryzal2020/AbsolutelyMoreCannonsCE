/**
 * The single source of truth binding a field in the normalised turret object to
 * its exact location in the XML.
 *
 * Extraction, diffing and injection all walk this same table, so a field can
 * never be read from one tag and written to another.
 *
 * Path steps:
 *   {tag}                                    direct child by tag name
 *   {tag:'li', cls:'CompProperties_X'}       <li Class="...X..."> inside a container
 *   {tag:'li', match:{tag,contains}}         <li> whose child <tag> text contains a string
 */

export const BARREL_EXT = [{ tag: 'modExtensions' }, { tag: 'li', cls: 'TurretBarrelExtension' }];
export const SMOKER_COMP = [{ tag: 'comps' }, { tag: 'li', cls: 'CompProperties_TurretSmoker' }];
export const POWER_COMP = [{ tag: 'comps' }, { tag: 'li', cls: 'CompProperties_Power' }];
export const SWAP_COMP = [{ tag: 'comps' }, { tag: 'li', cls: 'CompProperties_TurretModeSwap' }];
export const ACCURACY_COMP = [{ tag: 'comps' }, { tag: 'li', cls: 'CompProperties_AccuracyOverride' }];
export const ENCLOSED_COMP = [{ tag: 'comps' }, { tag: 'li', cls: 'CompProperties_EnclosedTurret' }];
export const AMMO_COMP = [{ tag: 'comps' }, { tag: 'li', cls: 'CompProperties_AmmoUser' }];
export const SHOOT_VERB = [{ tag: 'verbs' }, { tag: 'li', match: { tag: 'verbClass', contains: 'Verb_Shoot' } }];

const f = (key, doc, path, type, label, extra = {}) => ({ key, doc, path, type, label, ...extra });

export const FIELDS = [
  // ---- Basic & stats (building ThingDef) -------------------------------
  f('label', 'building', [{ tag: 'label' }], 'string', 'Label'),
  f('stats.maxHitPoints', 'building', [{ tag: 'statBases' }, { tag: 'MaxHitPoints' }], 'int', 'Max Hit Points'),
  f('stats.workToBuild', 'building', [{ tag: 'statBases' }, { tag: 'WorkToBuild' }], 'int', 'Work To Build'),
  f('stats.mass', 'building', [{ tag: 'statBases' }, { tag: 'Mass' }], 'float', 'Mass (kg)'),
  f('stats.bulk', 'building', [{ tag: 'statBases' }, { tag: 'Bulk' }], 'float', 'Bulk'),
  f('stats.cooldownTime', 'building', [{ tag: 'building' }, { tag: 'turretBurstCooldownTime' }], 'float', 'Turret Cooldown Time (s)'),
  f('stats.topDrawSize', 'building', [{ tag: 'building' }, { tag: 'turretTopDrawSize' }], 'float', 'Turret Top Draw Size'),
  f('stats.constructionSkill', 'building', [{ tag: 'constructionSkillPrerequisite' }], 'int', 'Construction Skill'),

  // ---- Comps & FCS -----------------------------------------------------
  f('comps.powerWatts', 'building', [...POWER_COMP, { tag: 'basePowerConsumption' }], 'float', 'Power (W)', { gate: 'comps.isPowered' }),
  f('comps.swapAltDef', 'building', [...SWAP_COMP, { tag: 'alternateDef' }], 'string', 'Alternate Mode Def', { gate: 'comps.hasModeSwap' }),
  f('comps.swapGizmoLabel', 'building', [...SWAP_COMP, { tag: 'gizmoLabel' }], 'string', 'Mode Swap Gizmo Label', { gate: 'comps.hasModeSwap' }),
  f('comps.accuracy.swayReduction', 'building', [...ACCURACY_COMP, { tag: 'swayReduction' }], 'float', 'Sway Reduction', { gate: 'comps.accuracy.enabled' }),
  f('comps.accuracy.recoilReduction', 'building', [...ACCURACY_COMP, { tag: 'recoilReduction' }], 'float', 'Recoil Reduction', { gate: 'comps.accuracy.enabled' }),
  f('comps.accuracy.spreadReduction', 'building', [...ACCURACY_COMP, { tag: 'spreadReduction' }], 'float', 'Spread Reduction', { gate: 'comps.accuracy.enabled' }),
  f('comps.enclosed.bulletProtection', 'building', [...ENCLOSED_COMP, { tag: 'bulletProtection' }], 'float', 'Bullet Protection', { gate: 'comps.enclosed.enabled' }),
  f('comps.enclosed.explosiveProtection', 'building', [...ENCLOSED_COMP, { tag: 'explosiveProtection' }], 'float', 'Explosive Protection', { gate: 'comps.enclosed.enabled' }),
  f('comps.enclosed.flameProtection', 'building', [...ENCLOSED_COMP, { tag: 'flameProtection' }], 'float', 'Flame Protection', { gate: 'comps.enclosed.enabled' }),
  f('comps.enclosed.meleeProtection', 'building', [...ENCLOSED_COMP, { tag: 'meleeProtection' }], 'float', 'Melee Protection', { gate: 'comps.enclosed.enabled' }),
  f('comps.enclosed.temperatureProtection', 'building', [...ENCLOSED_COMP, { tag: 'temperatureProtection' }], 'float', 'Temperature Protection', { gate: 'comps.enclosed.enabled' }),
  f('comps.enclosed.generalProtection', 'building', [...ENCLOSED_COMP, { tag: 'generalProtection' }], 'float', 'General Protection', { gate: 'comps.enclosed.enabled' }),
  f('comps.enclosed.hidePawnGraphics', 'building', [...ENCLOSED_COMP, { tag: 'hidePawnGraphics' }], 'bool', 'Hide Pawn Graphics', { gate: 'comps.enclosed.enabled' }),
  f('comps.enclosed.centerPawnPosition', 'building', [...ENCLOSED_COMP, { tag: 'centerPawnPosition' }], 'bool', 'Center Pawn Position', { gate: 'comps.enclosed.enabled' }),

  // ---- Ballistics (weapon ThingDef) ------------------------------------
  f('ballistics.verbClass', 'weapon', [...SHOOT_VERB, { tag: 'verbClass' }], 'string', 'Verb Class'),
  f('ballistics.minRange', 'weapon', [...SHOOT_VERB, { tag: 'minRange' }], 'float', 'Min Range (cells)'),
  f('ballistics.maxRange', 'weapon', [...SHOOT_VERB, { tag: 'range' }], 'float', 'Max Range (cells)'),
  f('ballistics.burstShotCount', 'weapon', [...SHOOT_VERB, { tag: 'burstShotCount' }], 'int', 'Burst Shot Count'),
  f('ballistics.ticksBetween', 'weapon', [...SHOOT_VERB, { tag: 'ticksBetweenBurstShots' }], 'int', 'Ticks Between Burst Shots'),
  f('ballistics.warmupTime', 'weapon', [...SHOOT_VERB, { tag: 'warmupTime' }], 'float', 'Warmup Time (s)'),
  f('ballistics.sightsEfficiency', 'weapon', [{ tag: 'statBases' }, { tag: 'SightsEfficiency' }], 'float', 'Sights Efficiency'),
  f('ballistics.shotSpread', 'weapon', [{ tag: 'statBases' }, { tag: 'ShotSpread' }], 'float', 'Shot Spread'),
  f('ballistics.swayFactor', 'weapon', [{ tag: 'statBases' }, { tag: 'SwayFactor' }], 'float', 'Sway Factor'),
  f('ballistics.cooldown', 'weapon', [{ tag: 'statBases' }, { tag: 'RangedWeapon_Cooldown' }], 'float', 'Weapon Cooldown (s)'),

  // ---- AmmoSet ---------------------------------------------------------
  f('ammo.ammoSet', 'weapon', [...AMMO_COMP, { tag: 'ammoSet' }], 'string', 'AmmoSet'),
  f('ammo.magazineSize', 'weapon', [...AMMO_COMP, { tag: 'magazineSize' }], 'int', 'Magazine Size'),
  f('ammo.reloadTime', 'weapon', [...AMMO_COMP, { tag: 'reloadTime' }], 'float', 'Reload Time (s)'),

  // ---- Barrel extension: root ------------------------------------------
  f('barrel.drawSize', 'building', [...BARREL_EXT, { tag: 'barrelDrawSize' }], 'float', 'Barrel Draw Size', { gate: 'barrel.enabled' }),
  f('barrel.offset', 'building', [...BARREL_EXT, { tag: 'barrelOffset' }], 'string', 'Barrel Offset', { gate: 'barrel.enabled' }),
  f('barrel.drawOnTop', 'building', [...BARREL_EXT, { tag: 'drawOnTop' }], 'bool', 'Draw On Top', { gate: 'barrel.enabled' }),
  f('barrel.barrelAmount', 'building', [...BARREL_EXT, { tag: 'barrelAmount' }], 'int', 'Barrel Count', { gate: 'barrel.enabled' }),
  f('barrel.barrelSpacing', 'building', [...BARREL_EXT, { tag: 'barrelSpacing' }], 'float', 'Barrel Spacing', { gate: 'barrel.enabled' }),
  f('barrel.sequentialFiring', 'building', [...BARREL_EXT, { tag: 'sequentialFiring' }], 'bool', 'Sequential Alternating Fire', { gate: 'barrel.enabled' }),
  f('barrel.selectableBursts.counts', 'building', [...BARREL_EXT, { tag: 'selectableBurstCounts' }], 'list', 'Selectable Burst Counts', { gate: 'barrel.selectableBursts.enabled' }),
  f('barrel.maxRPMs', 'building', [...BARREL_EXT, { tag: 'maxRPMs' }], 'list', 'Max RPM Steps', { gate: 'barrel.enabled' }),

  // ---- Barrel extension: recoil animation ------------------------------
  f('barrel.recoil.enabled', 'building', [...BARREL_EXT, { tag: 'recoilAnimation' }, { tag: 'enabled' }], 'bool', 'Recoil Enabled', { gate: 'barrel.enabled' }),
  f('barrel.recoil.maxDistance', 'building', [...BARREL_EXT, { tag: 'recoilAnimation' }, { tag: 'maxDistance' }], 'float', 'Recoil Max Distance', { gate: 'barrel.recoil.enabled' }),
  f('barrel.recoil.recoilDuration', 'building', [...BARREL_EXT, { tag: 'recoilAnimation' }, { tag: 'recoilDuration' }], 'int', 'Recoil Duration (ticks)', { gate: 'barrel.recoil.enabled' }),
  f('barrel.recoil.returnDuration', 'building', [...BARREL_EXT, { tag: 'recoilAnimation' }, { tag: 'returnDuration' }], 'int', 'Return Duration (ticks)', { gate: 'barrel.recoil.enabled' }),
  f('barrel.recoil.useRecoilCurve', 'building', [...BARREL_EXT, { tag: 'recoilAnimation' }, { tag: 'useRecoilCurve' }], 'bool', 'Use Recoil Curve', { gate: 'barrel.recoil.enabled' }),
  f('barrel.recoil.useReturnCurve', 'building', [...BARREL_EXT, { tag: 'recoilAnimation' }, { tag: 'useReturnCurve' }], 'bool', 'Use Return Curve', { gate: 'barrel.recoil.enabled' }),
  f('barrel.recoil.affectsRotation', 'building', [...BARREL_EXT, { tag: 'recoilAnimation' }, { tag: 'affectsRotation' }], 'bool', 'Rotation Impact', { gate: 'barrel.recoil.enabled' }),

  // ---- Barrel extension: firing flash & sound --------------------------
  f('barrel.firing.enabled', 'building', [...BARREL_EXT, { tag: 'firingAnimation' }, { tag: 'enabled' }], 'bool', 'Firing Flash Enabled', { gate: 'barrel.enabled' }),
  f('barrel.firing.durationTicks', 'building', [...BARREL_EXT, { tag: 'firingAnimation' }, { tag: 'durationTicks' }], 'int', 'Flash Duration (ticks)', { gate: 'barrel.firing.enabled' }),
  f('barrel.firing.drawFlash', 'building', [...BARREL_EXT, { tag: 'firingAnimation' }, { tag: 'drawFlash' }], 'bool', 'Draw Muzzle Flash', { gate: 'barrel.firing.enabled' }),
  f('barrel.firing.flashColor', 'building', [...BARREL_EXT, { tag: 'firingAnimation' }, { tag: 'flashColor' }], 'string', 'Flash Color (RGBA)', { gate: 'barrel.firing.enabled' }),
  f('barrel.firing.flashSize', 'building', [...BARREL_EXT, { tag: 'firingAnimation' }, { tag: 'flashSize' }], 'float', 'Flash Size', { gate: 'barrel.firing.enabled' }),
  f('barrel.firing.flashBrightness', 'building', [...BARREL_EXT, { tag: 'firingAnimation' }, { tag: 'flashBrightness' }], 'float', 'Flash Brightness', { gate: 'barrel.firing.enabled' }),
  f('barrel.firing.projectileSpawnOffset', 'building', [...BARREL_EXT, { tag: 'firingAnimation' }, { tag: 'projectileSpawnOffset' }], 'float', 'Projectile Spawn Offset', { gate: 'barrel.firing.enabled' }),
  f('barrel.firing.burstSound', 'building', [...BARREL_EXT, { tag: 'firingAnimation' }, { tag: 'burstSound' }], 'string', 'Sustained Burst SoundDef', { gate: 'barrel.firing.enabled' }),

  // ---- Barrel extension: rotary / spinning animation -------------------
  f('barrel.spinning.enabled', 'building', [...BARREL_EXT, { tag: 'spinningAnimation' }, { tag: 'enabled' }], 'bool', 'Rotary Enabled', { gate: 'barrel.enabled' }),
  f('barrel.spinning.animationMode', 'building', [...BARREL_EXT, { tag: 'spinningAnimation' }, { tag: 'animationMode' }], 'string', 'Animation Mode', { gate: 'barrel.spinning.enabled' }),
  f('barrel.spinning.maxRPM', 'building', [...BARREL_EXT, { tag: 'spinningAnimation' }, { tag: 'maxRPM' }], 'float', 'Max RPM', { gate: 'barrel.spinning.enabled' }),
  f('barrel.spinning.spindownTime', 'building', [...BARREL_EXT, { tag: 'spinningAnimation' }, { tag: 'spindownTime' }], 'float', 'Spindown Time (s)', { gate: 'barrel.spinning.enabled' }),
  f('barrel.spinning.frameCount', 'building', [...BARREL_EXT, { tag: 'spinningAnimation' }, { tag: 'frameCount' }], 'int', 'Frame Count', { gate: 'barrel.spinning.enabled' }),
  f('barrel.spinning.barrelCount', 'building', [...BARREL_EXT, { tag: 'spinningAnimation' }, { tag: 'barrelCount' }], 'int', 'Rotary Barrel Count', { gate: 'barrel.spinning.enabled' }),
  f('barrel.spinning.spinUpSound', 'building', [...BARREL_EXT, { tag: 'spinningAnimation' }, { tag: 'spinUpSound' }], 'string', 'Spin Up SoundDef', { gate: 'barrel.spinning.enabled' }),
  f('barrel.spinning.spinDownSound', 'building', [...BARREL_EXT, { tag: 'spinningAnimation' }, { tag: 'spinDownSound' }], 'string', 'Spin Down SoundDef', { gate: 'barrel.spinning.enabled' }),

  // ---- Smoker comp -----------------------------------------------------
  f('smoker.muzzle.enabled', 'building', [...SMOKER_COMP, { tag: 'muzzleEnabled' }], 'bool', 'Muzzle Smoke Enabled', { gate: 'smoker.enabled' }),
  f('smoker.muzzle.fleckDef', 'building', [...SMOKER_COMP, { tag: 'muzzleFleckDef' }], 'string', 'Muzzle FleckDef', { gate: 'smoker.muzzle.enabled' }),
  f('smoker.muzzle.particleCount', 'building', [...SMOKER_COMP, { tag: 'muzzleParticleCount' }], 'int', 'Muzzle Particle Count', { gate: 'smoker.muzzle.enabled' }),
  f('smoker.muzzle.velocity', 'building', [...SMOKER_COMP, { tag: 'muzzleVelocity' }], 'float', 'Muzzle Velocity (cells/s)', { gate: 'smoker.muzzle.enabled' }),
  // muzzleParticleSize accepts RimWorld range syntax such as "1~2", so it stays a string.
  f('smoker.muzzle.particleSize', 'building', [...SMOKER_COMP, { tag: 'muzzleParticleSize' }], 'string', 'Muzzle Size Multiplier', { gate: 'smoker.muzzle.enabled' }),

  f('smoker.heat.enabled', 'building', [...SMOKER_COMP, { tag: 'heatEnabled' }], 'bool', 'Heat Smoke Enabled', { gate: 'smoker.enabled' }),
  f('smoker.heat.fleckDef', 'building', [...SMOKER_COMP, { tag: 'heatFleckDef' }], 'string', 'Heat FleckDef', { gate: 'smoker.heat.enabled' }),
  f('smoker.heat.threshold', 'building', [...SMOKER_COMP, { tag: 'heatThreshold' }], 'int', 'Heat Threshold (shots)', { gate: 'smoker.heat.enabled' }),
  f('smoker.heat.decayRate', 'building', [...SMOKER_COMP, { tag: 'heatDecayRate' }], 'float', 'Heat Decay Rate (/s)', { gate: 'smoker.heat.enabled' }),
  f('smoker.heat.emissionRate', 'building', [...SMOKER_COMP, { tag: 'heatEmissionRate' }], 'float', 'Heat Emission Rate (/s)', { gate: 'smoker.heat.enabled' }),

  f('smoker.shockwave.enabled', 'building', [...SMOKER_COMP, { tag: 'shockwaveEnabled' }], 'bool', 'Shockwave Enabled', { gate: 'smoker.enabled' }),
  f('smoker.shockwave.fleckDef', 'building', [...SMOKER_COMP, { tag: 'shockwaveFleckDef' }], 'string', 'Shockwave FleckDef', { gate: 'smoker.shockwave.enabled' }),
  f('smoker.shockwave.radius', 'building', [...SMOKER_COMP, { tag: 'shockwaveRadius' }], 'float', 'Shockwave Radius (cells)', { gate: 'smoker.shockwave.enabled' }),
  f('smoker.shockwave.density', 'building', [...SMOKER_COMP, { tag: 'shockwaveDensity' }], 'int', 'Shockwave Particle Density', { gate: 'smoker.shockwave.enabled' }),
];

export const FIELD_BY_KEY = new Map(FIELDS.map((x) => [x.key, x]));

/**
 * Toggles that add or remove a whole `<li Class="...">` element rather than
 * editing a value inside one.
 */
export const COMPONENT_TOGGLES = [
  { key: 'comps.isManned', container: 'comps', cls: 'CompProperties_Mannable', label: 'Mannable Turret',
    body: '<li Class="CompProperties_Mannable">\n  <manWorkType>Violent</manWorkType>\n</li>' },
  { key: 'comps.isPowered', container: 'comps', cls: 'CompProperties_Power', label: 'Electrical Power',
    body: '<li Class="CompProperties_Power">\n  <compClass>CompPowerTrader</compClass>\n  <basePowerConsumption>200</basePowerConsumption>\n</li>' },
  { key: 'comps.hasFcs', container: 'comps', cls: 'CompProperties_TurretFCS', label: 'FCS Comp',
    body: '<li Class="AbsolutelyMoreCannons.CompProperties_TurretFCS" />' },
  { key: 'comps.hasModeSwap', container: 'comps', cls: 'CompProperties_TurretModeSwap', label: 'Mode Swap',
    body: '<li Class="AbsolutelyMoreCannons.CompProperties_TurretModeSwap">\n  <alternateDef></alternateDef>\n  <gizmoLabel>Switch Mode</gizmoLabel>\n</li>' },
  { key: 'comps.accuracy.enabled', container: 'comps', cls: 'CompProperties_AccuracyOverride', label: 'Accuracy Override',
    body: '<li Class="AbsolutelyMoreCannons.CompProperties_AccuracyOverride">\n  <swayReduction>0.1</swayReduction>\n  <recoilReduction>0.1</recoilReduction>\n  <spreadReduction>0.1</spreadReduction>\n</li>' },
  { key: 'comps.enclosed.enabled', container: 'comps', cls: 'CompProperties_EnclosedTurret', label: 'Enclosed Turret',
    body: '<li Class="AbsolutelyMoreCannons.CompProperties_EnclosedTurret">\n  <bulletProtection>1.0</bulletProtection>\n  <explosiveProtection>0.8</explosiveProtection>\n  <temperatureProtection>0.5</temperatureProtection>\n  <hidePawnGraphics>true</hidePawnGraphics>\n</li>' },
  { key: 'smoker.enabled', container: 'comps', cls: 'CompProperties_TurretSmoker', label: 'Smoker Component',
    body: '<li Class="AbsolutelyMoreCannons.CompProperties_TurretSmoker">\n  <muzzleEnabled>false</muzzleEnabled>\n  <heatEnabled>false</heatEnabled>\n  <shockwaveEnabled>false</shockwaveEnabled>\n</li>' },
  { key: 'barrel.enabled', container: 'modExtensions', cls: 'TurretBarrelExtension', label: 'Barrel Extension',
    body: '<li Class="AbsolutelyMoreCannons.TurretBarrelExtension">\n  <barrelDrawSize>1.0</barrelDrawSize>\n  <barrelOffset>(0,0,0.0)</barrelOffset>\n  <drawOnTop>false</drawOnTop>\n</li>' },
];

export const TOGGLE_BY_KEY = new Map(COMPONENT_TOGGLES.map((t) => [t.key, t]));

// ---------------------------------------------------------------------------

export function getPath(obj, dotted) {
  return dotted.split('.').reduce((acc, k) => (acc == null ? undefined : acc[k]), obj);
}

export function setPath(obj, dotted, value) {
  const parts = dotted.split('.');
  let cur = obj;
  for (let i = 0; i < parts.length - 1; i++) {
    if (cur[parts[i]] == null || typeof cur[parts[i]] !== 'object') cur[parts[i]] = {};
    cur = cur[parts[i]];
  }
  cur[parts[parts.length - 1]] = value;
  return obj;
}

/** Normalise a raw XML string into the JS type declared for the field. */
export function coerce(type, raw, fallback = null) {
  if (raw === null || raw === undefined || raw === '') return fallback;
  switch (type) {
    case 'int': { const n = parseInt(String(raw), 10); return Number.isNaN(n) ? fallback : n; }
    case 'float': { const n = Number(raw); return Number.isNaN(n) ? fallback : n; }
    case 'bool': return String(raw).trim().toLowerCase() === 'true';
    case 'list': return Array.isArray(raw) ? raw.join(', ') : String(raw);
    default: return String(raw);
  }
}

/** Render a JS value back into the exact text RimWorld expects in the tag. */
export function serialise(type, value) {
  if (value === null || value === undefined) return '';
  if (type === 'bool') return value ? 'true' : 'false';
  if (type === 'list') {
    return String(value)
      .split(',')
      .map((s) => s.trim())
      .filter(Boolean);
  }
  return String(value);
}
