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
export const VIEW_TRANSFER_COMP = [{ tag: 'comps' }, { tag: 'li', cls: 'CompProperties_TurretViewTransfer' }];
export const TRACER_COMP = [{ tag: 'comps' }, { tag: 'li', cls: 'CompProperties_TracerLine' }];
export const FIREARC_COMP = [{ tag: 'comps' }, { tag: 'li', cls: 'CompProperties_FireArc' }];
export const TRACKING_EXT = [{ tag: 'modExtensions' }, { tag: 'li', cls: 'TurretTrackingExtension' }];
export const NONSNAP_EXT = [{ tag: 'modExtensions' }, { tag: 'li', cls: 'NonSnapTurretExtension' }];
export const SPRAY_EXT = [{ tag: 'modExtensions' }, { tag: 'li', cls: 'TurretSprayDisciplineExtension' }];
export const SUPPRESSION_EXT = [{ tag: 'modExtensions' }, { tag: 'li', cls: 'TurretSuppressionImmunityExtension' }];
export const CHARGEBOOST_EXT = [{ tag: 'modExtensions' }, { tag: 'li', cls: 'TurretChargeBoostExtension' }];
export const CLAMPING_EXT = [{ tag: 'modExtensions' }, { tag: 'li', cls: 'TurretClampingExtension' }];
export const GUNDRAW_EXT = [{ tag: 'modExtensions' }, { tag: 'li', cls: 'GunDrawExtension' }];
export const FIREMODES_COMP = [{ tag: 'comps' }, { tag: 'li', cls: 'CompProperties_FireModes' }];
export const CHARGES_COMP = [{ tag: 'comps' }, { tag: 'li', cls: 'CompProperties_Charges' }];
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
  f('stats.flammability', 'building', [{ tag: 'statBases' }, { tag: 'Flammability' }], 'float', 'Flammability'),
  f('stats.aimingAccuracy', 'building', [{ tag: 'statBases' }, { tag: 'AimingAccuracy' }], 'float', 'Aiming Accuracy'),
  f('stats.shootingAccuracyTurret', 'building', [{ tag: 'statBases' }, { tag: 'ShootingAccuracyTurret' }], 'float', 'Shooting Accuracy (turret)'),
  f('stats.beauty', 'building', [{ tag: 'statBases' }, { tag: 'Beauty' }], 'float', 'Beauty'),

  // ---- Identity, placement and art -------------------------------------
  // size and interactionCellOffset are "x,y" / "(x,y,z)" tuples, so they stay
  // strings: the list type would split them on their commas.
  f('size', 'building', [{ tag: 'size' }], 'string', 'Building Size (x,y)'),
  f('designatorDropdown', 'building', [{ tag: 'designatorDropdown' }], 'string', 'Designator Group'),
  f('weaponDefName', 'building', [{ tag: 'building' }, { tag: 'turretGunDef' }], 'string', 'Turret Gun Def'),
  f('terrainAffordanceNeeded', 'building', [{ tag: 'terrainAffordanceNeeded' }], 'string', 'Terrain Affordance'),
  f('researchPrerequisites', 'building', [{ tag: 'researchPrerequisites' }], 'list', 'Research Prerequisites'),
  f('interactionCellOffset', 'building', [{ tag: 'interactionCellOffset' }], 'string', 'Interaction Cell Offset'),
  f('designationCategory', 'building', [{ tag: 'designationCategory' }], 'string', 'Designation Category'),
  f('fillPercent', 'building', [{ tag: 'fillPercent' }], 'float', 'Fill Percent'),
  f('pathCost', 'building', [{ tag: 'pathCost' }], 'int', 'Path Cost'),
  f('passability', 'building', [{ tag: 'passability' }], 'string', 'Passability'),
  f('textures.building', 'building', [{ tag: 'graphicData' }, { tag: 'texPath' }], 'string', 'Building Texture Path'),
  f('textures.icon', 'building', [{ tag: 'uiIconPath' }], 'string', 'Menu Icon Path'),
  f('graphics.shadowVolume', 'building', [{ tag: 'graphicData' }, { tag: 'shadowData' }, { tag: 'volume' }], 'string', 'Shadow Volume (x,y,z)'),
  f('graphics.shadowOffset', 'building', [{ tag: 'graphicData' }, { tag: 'shadowData' }, { tag: 'offset' }], 'string', 'Shadow Offset (x,y,z)'),
  f('placeWorkers', 'building', [{ tag: 'placeWorkers' }], 'list', 'Place Workers'),
  f('hasInteractionCell', 'building', [{ tag: 'hasInteractionCell' }], 'bool', 'Has Interaction Cell'),

  // ---- <building> sub-block --------------------------------------------
  f('building.aiCombatDangerous', 'building', [{ tag: 'building' }, { tag: 'ai_combatDangerous' }], 'bool', 'AI Treats As Dangerous'),
  f('building.turretBurstWarmupTime', 'building', [{ tag: 'building' }, { tag: 'turretBurstWarmupTime' }], 'float', 'Turret Burst Warmup (s)'),
  f('building.spawnedConceptLearnOpportunity', 'building', [{ tag: 'building' }, { tag: 'spawnedConceptLearnOpportunity' }], 'string', 'Spawned Concept'),
  f('building.buildingTags', 'building', [{ tag: 'building' }, { tag: 'buildingTags' }], 'list', 'Building Tags'),

  // ---- Comps & FCS -----------------------------------------------------
  f('comps.powerWatts', 'building', [...POWER_COMP, { tag: 'basePowerConsumption' }], 'float', 'Power (W)', { gate: 'comps.isPowered' }),
  f('comps.powerCompClass', 'building', [...POWER_COMP, { tag: 'compClass' }], 'string', 'Power Comp Class', { gate: 'comps.isPowered' }),
  f('comps.swapAltDef', 'building', [...SWAP_COMP, { tag: 'alternateDef' }], 'string', 'Alternate Mode Def', { gate: 'comps.hasModeSwap' }),
  f('comps.swapGizmoLabel', 'building', [...SWAP_COMP, { tag: 'gizmoLabel' }], 'string', 'Mode Swap Gizmo Label', { gate: 'comps.hasModeSwap' }),
  f('comps.swapGizmoDesc', 'building', [...SWAP_COMP, { tag: 'gizmoDesc' }], 'string', 'Mode Swap Gizmo Description', { gate: 'comps.hasModeSwap' }),
  f('comps.swapGizmoIcon', 'building', [...SWAP_COMP, { tag: 'gizmoIcon' }], 'string', 'Mode Swap Gizmo Icon', { gate: 'comps.hasModeSwap' }),
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

  // ---- Fire arc, suppression immunity ----------------------------------
  f('comps.fireArc.spanMin', 'building', [...FIREARC_COMP, { tag: 'spanRange' }, { tag: 'min' }], 'int', 'Fire Arc Min Span (deg)', { gate: 'comps.hasFireArc' }),
  f('comps.fireArc.spanMax', 'building', [...FIREARC_COMP, { tag: 'spanRange' }, { tag: 'max' }], 'int', 'Fire Arc Max Span (deg)', { gate: 'comps.hasFireArc' }),
  f('comps.fireArc.maxSpanDeviation', 'building', [...FIREARC_COMP, { tag: 'maxSpanDeviation' }], 'float', 'Fire Arc Max Deviation (deg)', { gate: 'comps.hasFireArc' }),
  f('comps.fireArc.lineLength', 'building', [...FIREARC_COMP, { tag: 'lineLength' }], 'float', 'Fire Arc Line Length', { gate: 'comps.hasFireArc' }),
  f('comps.preventOperatorSuppression', 'building', [...SUPPRESSION_EXT, { tag: 'preventOperatorSuppression' }], 'bool', 'Prevent Operator Suppression', { gate: 'comps.hasSuppressionImmunity' }),

  // ---- View transfer ---------------------------------------------------
  f('viewTransfer.transferViewOrigin', 'building', [...VIEW_TRANSFER_COMP, { tag: 'transferViewOrigin' }], 'bool', 'Transfer View Origin', { gate: 'viewTransfer.enabled' }),
  f('viewTransfer.ignoreSelfOcclusion', 'building', [...VIEW_TRANSFER_COMP, { tag: 'ignoreSelfOcclusion' }], 'bool', 'Ignore Self Occlusion', { gate: 'viewTransfer.enabled' }),

  // ---- Tracer line -----------------------------------------------------
  f('tracer.lineLength', 'building', [...TRACER_COMP, { tag: 'lineLength' }], 'float', 'Tracer Length (cells)', { gate: 'tracer.enabled' }),
  f('tracer.lineWidth', 'building', [...TRACER_COMP, { tag: 'lineWidth' }], 'float', 'Tracer Width', { gate: 'tracer.enabled' }),
  f('tracer.lineOffset', 'building', [...TRACER_COMP, { tag: 'lineOffset' }], 'string', 'Tracer Offset (x,y,z)', { gate: 'tracer.enabled' }),
  f('tracer.durationTicks', 'building', [...TRACER_COMP, { tag: 'durationTicks' }], 'int', 'Tracer Duration (ticks)', { gate: 'tracer.enabled' }),

  // ---- Mid-burst tracking ----------------------------------------------
  f('tracking.enableMidBurstTracking', 'building', [...TRACKING_EXT, { tag: 'enableMidBurstTracking' }], 'bool', 'Enable Mid-Burst Tracking', { gate: 'tracking.enabled' }),

  // ---- Non-snap rotation -----------------------------------------------
  f('nonSnap.speed', 'building', [...NONSNAP_EXT, { tag: 'speed' }], 'float', 'Turn Speed (deg/tick)', { gate: 'nonSnap.enabled' }),
  f('nonSnap.preferedAngleRange', 'building', [...NONSNAP_EXT, { tag: 'preferedAngleRange' }], 'float', 'Preferred Angle Range (deg)', { gate: 'nonSnap.enabled' }),
  f('nonSnap.angleWeightMultiplier', 'building', [...NONSNAP_EXT, { tag: 'angleWeightMultiplier' }], 'float', 'Angle Weight Multiplier', { gate: 'nonSnap.enabled' }),
  f('nonSnap.minAngleWeight', 'building', [...NONSNAP_EXT, { tag: 'minAngleWeight' }], 'float', 'Min Angle Weight', { gate: 'nonSnap.enabled' }),

  // ---- Spray discipline ------------------------------------------------
  f('spray.defaultEnableSprayDiscipline', 'building', [...SPRAY_EXT, { tag: 'defaultEnableSprayDiscipline' }], 'bool', 'Spray Discipline On By Default', { gate: 'spray.enabled' }),
  f('spray.shotsPerTarget', 'building', [...SPRAY_EXT, { tag: 'shotsPerTarget' }], 'int', 'Shots Per Target', { gate: 'spray.enabled' }),
  f('spray.cycleConeDegrees', 'building', [...SPRAY_EXT, { tag: 'cycleConeDegrees' }], 'float', 'Cycle Cone (deg)', { gate: 'spray.enabled' }),
  f('spray.allowToggle', 'building', [...SPRAY_EXT, { tag: 'allowToggle' }], 'bool', 'Allow Player Toggle', { gate: 'spray.enabled' }),

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

  // ---- Shoot verb: recoil, sound and targeting -------------------------
  f('ballistics.defaultProjectile', 'weapon', [...SHOOT_VERB, { tag: 'defaultProjectile' }], 'string', 'Default Projectile'),
  f('ballistics.recoilAmount', 'weapon', [...SHOOT_VERB, { tag: 'recoilAmount' }], 'float', 'Recoil Amount'),
  f('ballistics.recoilPattern', 'weapon', [...SHOOT_VERB, { tag: 'recoilPattern' }], 'string', 'Recoil Pattern'),
  f('ballistics.muzzleFlashScale', 'weapon', [...SHOOT_VERB, { tag: 'muzzleFlashScale' }], 'float', 'Muzzle Flash Scale'),
  f('ballistics.soundCast', 'weapon', [...SHOOT_VERB, { tag: 'soundCast' }], 'string', 'Shot SoundDef'),
  f('ballistics.soundCastTail', 'weapon', [...SHOOT_VERB, { tag: 'soundCastTail' }], 'string', 'Shot Tail SoundDef'),
  f('ballistics.circularError', 'weapon', [...SHOOT_VERB, { tag: 'circularError' }], 'float', 'Circular Error (cells)'),
  f('ballistics.indirectFirePenalty', 'weapon', [...SHOOT_VERB, { tag: 'indirectFirePenalty' }], 'float', 'Indirect Fire Penalty'),
  f('ballistics.requireLineOfSight', 'weapon', [...SHOOT_VERB, { tag: 'requireLineOfSight' }], 'bool', 'Require Line Of Sight'),
  f('ballistics.stopBurstWithoutLos', 'weapon', [...SHOOT_VERB, { tag: 'stopBurstWithoutLos' }], 'bool', 'Stop Burst Without LoS'),
  f('ballistics.ignorePartialLoSBlocker', 'weapon', [...SHOOT_VERB, { tag: 'ignorePartialLoSBlocker' }], 'bool', 'Ignore Partial LoS Blocker'),
  f('ballistics.forceNormalTimeSpeed', 'weapon', [...SHOOT_VERB, { tag: 'forceNormalTimeSpeed' }], 'bool', 'Force Normal Time Speed'),
  f('ballistics.hasStandardCommand', 'weapon', [...SHOOT_VERB, { tag: 'hasStandardCommand' }], 'bool', 'Has Standard Command'),
  f('ballistics.canTargetLocations', 'weapon', [...SHOOT_VERB, { tag: 'targetParams' }, { tag: 'canTargetLocations' }], 'bool', 'Can Target Locations'),

  // ---- Weapon identity -------------------------------------------------
  f('gun.soundInteract', 'weapon', [{ tag: 'soundInteract' }], 'string', 'Interact SoundDef'),
  f('gun.weaponTags', 'weapon', [{ tag: 'weaponTags' }], 'list', 'Weapon Tags'),
  f('gun.nightVisionEfficiency', 'weapon', [{ tag: 'statBases' }, { tag: 'NightVisionEfficiency_Weapon' }], 'float', 'Night Vision Efficiency'),
  f('textures.weapon', 'weapon', [{ tag: 'graphicData' }, { tag: 'texPath' }], 'string', 'Gun Texture Path'),

  // ---- AmmoSet ---------------------------------------------------------
  f('ammo.ammoSet', 'weapon', [...AMMO_COMP, { tag: 'ammoSet' }], 'string', 'AmmoSet'),
  f('ammo.magazineSize', 'weapon', [...AMMO_COMP, { tag: 'magazineSize' }], 'int', 'Magazine Size'),
  f('ammo.reloadTime', 'weapon', [...AMMO_COMP, { tag: 'reloadTime' }], 'float', 'Reload Time (s)'),

  // ---- Turret clamping (weapon modExtension) ---------------------------
  f('clamping.maxVerticalDeviation', 'weapon', [...CLAMPING_EXT, { tag: 'maxVerticalDeviation' }], 'float', 'Max Vertical Deviation', { gate: 'clamping.enabled' }),
  f('clamping.maxRotationDeviation', 'weapon', [...CLAMPING_EXT, { tag: 'maxRotationDeviation' }], 'float', 'Max Rotation Deviation', { gate: 'clamping.enabled' }),

  // ---- Casing draw (weapon modExtension) -------------------------------
  // CasingOffset is an "x,y" pair, so it stays a string: the list type would
  // split it on the comma and write two <li> elements.
  f('gunDraw.casingOffset', 'weapon', [...GUNDRAW_EXT, { tag: 'CasingOffset' }], 'string', 'Casing Offset (x,y)', { gate: 'gunDraw.enabled' }),
  f('gunDraw.casingAngleOffset', 'weapon', [...GUNDRAW_EXT, { tag: 'CasingAngleOffset' }], 'float', 'Casing Angle Offset (deg)', { gate: 'gunDraw.enabled' }),

  // ---- Fire modes (weapon comp) ----------------------------------------
  f('fireModes.aiUseBurstMode', 'weapon', [...FIREMODES_COMP, { tag: 'aiUseBurstMode' }], 'bool', 'AI Uses Burst Mode', { gate: 'fireModes.enabled' }),
  f('fireModes.aiAimMode', 'weapon', [...FIREMODES_COMP, { tag: 'aiAimMode' }], 'string', 'AI Aim Mode', { gate: 'fireModes.enabled' }),
  f('fireModes.aimedBurstShotCount', 'weapon', [...FIREMODES_COMP, { tag: 'aimedBurstShotCount' }], 'int', 'Aimed Burst Shot Count', { gate: 'fireModes.enabled' }),
  f('fireModes.noSingleShot', 'weapon', [...FIREMODES_COMP, { tag: 'noSingleShot' }], 'bool', 'No Single Shot Mode', { gate: 'fireModes.enabled' }),
  f('fireModes.noSnapshot', 'weapon', [...FIREMODES_COMP, { tag: 'noSnapshot' }], 'bool', 'No Snapshot Mode', { gate: 'fireModes.enabled' }),

  // ---- Charge speeds (weapon comp) -------------------------------------
  f('charges.speeds', 'weapon', [...CHARGES_COMP, { tag: 'chargeSpeeds' }], 'list', 'Charge Speeds', { gate: 'charges.enabled' }),
  f('chargeBoost.chargeOffset', 'weapon', [...CHARGEBOOST_EXT, { tag: 'chargeOffset' }], 'float', 'Charge Boost Offset', { gate: 'chargeBoost.enabled' }),

  // ---- Barrel extension: root ------------------------------------------
  f('barrel.drawSize', 'building', [...BARREL_EXT, { tag: 'barrelDrawSize' }], 'float', 'Barrel Draw Size', { gate: 'barrel.enabled' }),
  f('barrel.offset', 'building', [...BARREL_EXT, { tag: 'barrelOffset' }], 'string', 'Barrel Offset', { gate: 'barrel.enabled' }),
  f('barrel.drawOnTop', 'building', [...BARREL_EXT, { tag: 'drawOnTop' }], 'bool', 'Draw On Top', { gate: 'barrel.enabled' }),
  f('barrel.barrelAmount', 'building', [...BARREL_EXT, { tag: 'barrelAmount' }], 'int', 'Barrel Count', { gate: 'barrel.enabled' }),
  f('barrel.barrelSpacing', 'building', [...BARREL_EXT, { tag: 'barrelSpacing' }], 'float', 'Barrel Spacing', { gate: 'barrel.enabled' }),
  f('barrel.sequentialFiring', 'building', [...BARREL_EXT, { tag: 'sequentialFiring' }], 'bool', 'Sequential Alternating Fire', { gate: 'barrel.enabled' }),
  f('barrel.selectableBursts.counts', 'building', [...BARREL_EXT, { tag: 'selectableBurstCounts' }], 'list', 'Selectable Burst Counts', { gate: 'barrel.selectableBursts.enabled' }),
  f('barrel.maxRPMs', 'building', [...BARREL_EXT, { tag: 'maxRPMs' }], 'list', 'Max RPM Steps', { gate: 'barrel.enabled' }),
  f('barrel.graphic.texPath', 'building', [...BARREL_EXT, { tag: 'barrelGraphic' }, { tag: 'texPath' }], 'string', 'Barrel Texture Path', { gate: 'barrel.enabled' }),
  f('barrel.graphic.graphicClass', 'building', [...BARREL_EXT, { tag: 'barrelGraphic' }, { tag: 'graphicClass' }], 'string', 'Barrel Graphic Class', { gate: 'barrel.enabled' }),
  f('barrel.graphic.drawSize', 'building', [...BARREL_EXT, { tag: 'barrelGraphic' }, { tag: 'drawSize' }], 'string', 'Barrel Graphic Draw Size', { gate: 'barrel.enabled' }),
  f('barrel.underGraphic.texPath', 'building', [...BARREL_EXT, { tag: 'underBarrelGraphic' }, { tag: 'texPath' }], 'string', 'Under-Barrel Texture Path', { gate: 'barrel.enabled' }),
  f('barrel.underGraphic.graphicClass', 'building', [...BARREL_EXT, { tag: 'underBarrelGraphic' }, { tag: 'graphicClass' }], 'string', 'Under-Barrel Graphic Class', { gate: 'barrel.enabled' }),
  f('barrel.underGraphic.drawSize', 'building', [...BARREL_EXT, { tag: 'underBarrelGraphic' }, { tag: 'drawSize' }], 'string', 'Under-Barrel Draw Size', { gate: 'barrel.enabled' }),

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
  f('barrel.firing.flashOffset', 'building', [...BARREL_EXT, { tag: 'firingAnimation' }, { tag: 'flashOffset' }], 'float', 'Flash Offset', { gate: 'barrel.firing.enabled' }),
  f('barrel.firing.muzzleFlashEffect', 'building', [...BARREL_EXT, { tag: 'firingAnimation' }, { tag: 'muzzleFlashEffect' }], 'string', 'Muzzle Flash EffecterDef', { gate: 'barrel.firing.enabled' }),

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
  // muzzleVelocity, muzzleVelDuration, and muzzleParticleSize accept RimWorld range syntax such as "10~20"
  f('smoker.muzzle.velocity', 'building', [...SMOKER_COMP, { tag: 'muzzleVelocity' }], 'string', 'Muzzle Velocity (cells/s)', { gate: 'smoker.muzzle.enabled' }),
  f('smoker.muzzle.velDuration', 'building', [...SMOKER_COMP, { tag: 'muzzleVelDuration' }], 'string', 'Muzzle Velocity Duration (ticks)', { gate: 'smoker.muzzle.enabled' }),
  f('smoker.muzzle.particleSize', 'building', [...SMOKER_COMP, { tag: 'muzzleParticleSize' }], 'string', 'Muzzle Size Multiplier', { gate: 'smoker.muzzle.enabled' }),
  f('smoker.muzzle.offset', 'building', [...SMOKER_COMP, { tag: 'muzzleOffset' }], 'string', 'Muzzle Offset (x,y,z)', { gate: 'smoker.muzzle.enabled' }),
  f('smoker.muzzle.spawnDelay', 'building', [...SMOKER_COMP, { tag: 'muzzleSpawnDelay' }], 'int', 'Muzzle Spawn Delay (ticks)', { gate: 'smoker.muzzle.enabled' }),
  f('smoker.muzzle.spawnDuration', 'building', [...SMOKER_COMP, { tag: 'muzzleSpawnDuration' }], 'int', 'Muzzle Spawn Duration (ticks)', { gate: 'smoker.muzzle.enabled' }),
  f('smoker.directionCone', 'building', [...SMOKER_COMP, { tag: 'directionCone' }], 'float', 'Emission Direction Cone (deg)', { gate: 'smoker.enabled' }),

  f('smoker.heat.enabled', 'building', [...SMOKER_COMP, { tag: 'heatEnabled' }], 'bool', 'Heat Smoke Enabled', { gate: 'smoker.enabled' }),
  f('smoker.heat.fleckDef', 'building', [...SMOKER_COMP, { tag: 'heatFleckDef' }], 'string', 'Heat FleckDef', { gate: 'smoker.heat.enabled' }),
  f('smoker.heat.threshold', 'building', [...SMOKER_COMP, { tag: 'heatThreshold' }], 'int', 'Heat Threshold (shots)', { gate: 'smoker.heat.enabled' }),
  f('smoker.heat.decayRate', 'building', [...SMOKER_COMP, { tag: 'heatDecayRate' }], 'float', 'Heat Decay Rate (/s)', { gate: 'smoker.heat.enabled' }),
  f('smoker.heat.emissionRate', 'building', [...SMOKER_COMP, { tag: 'heatEmissionRate' }], 'float', 'Heat Emission Rate (/s)', { gate: 'smoker.heat.enabled' }),
  f('smoker.heat.offset', 'building', [...SMOKER_COMP, { tag: 'heatOffset' }], 'string', 'Heat Offset (x,y,z)', { gate: 'smoker.heat.enabled' }),
  f('smoker.heat.decayIncrease', 'building', [...SMOKER_COMP, { tag: 'decayIncrease' }], 'float', 'Heat Decay Increase', { gate: 'smoker.heat.enabled' }),
  f('smoker.heat.emissionIncrease', 'building', [...SMOKER_COMP, { tag: 'emissionIncrease' }], 'float', 'Heat Emission Increase', { gate: 'smoker.heat.enabled' }),
  f('smoker.heat.particleSize', 'building', [...SMOKER_COMP, { tag: 'heatParticleSize' }], 'float', 'Heat Size Multiplier', { gate: 'smoker.heat.enabled' }),
  f('smoker.heat.emissionPoints', 'building', [...SMOKER_COMP, { tag: 'heatEmissionPoints' }], 'int', 'Heat Emission Points', { gate: 'smoker.heat.enabled' }),
  f('smoker.heat.emissionSpacing', 'building', [...SMOKER_COMP, { tag: 'heatEmissionSpacing' }], 'float', 'Heat Emission Spacing (cells)', { gate: 'smoker.heat.enabled' }),

  f('smoker.shockwave.enabled', 'building', [...SMOKER_COMP, { tag: 'shockwaveEnabled' }], 'bool', 'Shockwave Enabled', { gate: 'smoker.enabled' }),
  f('smoker.shockwave.fleckDef', 'building', [...SMOKER_COMP, { tag: 'shockwaveFleckDef' }], 'string', 'Shockwave FleckDef', { gate: 'smoker.shockwave.enabled' }),
  f('smoker.shockwave.radius', 'building', [...SMOKER_COMP, { tag: 'shockwaveRadius' }], 'float', 'Shockwave Radius (cells)', { gate: 'smoker.shockwave.enabled' }),
  f('smoker.shockwave.density', 'building', [...SMOKER_COMP, { tag: 'shockwaveDensity' }], 'int', 'Shockwave Particle Density', { gate: 'smoker.shockwave.enabled' }),
  f('smoker.shockwave.fadeOutSpeed', 'building', [...SMOKER_COMP, { tag: 'shockwaveFadeOutSpeed' }], 'float', 'Particle Fade Out Speed', { gate: 'smoker.shockwave.enabled' }),
  f('smoker.shockwave.gradientDensity', 'building', [...SMOKER_COMP, { tag: 'shockwaveGradientDensity' }], 'bool', 'Gradient Density', { gate: 'smoker.shockwave.enabled' }),
  f('smoker.shockwave.gradientParticleSize', 'building', [...SMOKER_COMP, { tag: 'shockwaveGradientParticleSize' }], 'bool', 'Gradient Particle Size', { gate: 'smoker.shockwave.enabled' }),
  f('smoker.shockwave.offset', 'building', [...SMOKER_COMP, { tag: 'shockwaveOffset' }], 'string', 'Shockwave Offset (x,y,z)', { gate: 'smoker.shockwave.enabled' }),
  f('smoker.shockwave.particleSize', 'building', [...SMOKER_COMP, { tag: 'shockwaveParticleSize' }], 'float', 'Shockwave Size Multiplier', { gate: 'smoker.shockwave.enabled' }),
];

export const FIELD_BY_KEY = new Map(FIELDS.map((x) => [x.key, x]));

/**
 * Toggles that add or remove a whole `<li Class="...">` element rather than
 * editing a value inside one.
 */
export const COMPONENT_TOGGLES = [
  { key: 'comps.isManned', doc: 'building', container: 'comps', cls: 'CompProperties_Mannable', label: 'Mannable Turret',
    body: '<li Class="CompProperties_Mannable">\n  <manWorkType>Violent</manWorkType>\n</li>' },
  { key: 'comps.isPowered', doc: 'building', container: 'comps', cls: 'CompProperties_Power', label: 'Electrical Power',
    body: '<li Class="CompProperties_Power">\n  <compClass>CompPowerTrader</compClass>\n  <basePowerConsumption>200</basePowerConsumption>\n</li>' },
  { key: 'comps.hasFcs', doc: 'building', container: 'comps', cls: 'CompProperties_TurretFCS', label: 'FCS Comp',
    body: '<li Class="AbsolutelyMoreCannons.CompProperties_TurretFCS" />' },
  { key: 'comps.hasModeSwap', doc: 'building', container: 'comps', cls: 'CompProperties_TurretModeSwap', label: 'Mode Swap',
    body: '<li Class="AbsolutelyMoreCannons.CompProperties_TurretModeSwap">\n  <alternateDef></alternateDef>\n  <gizmoLabel>Switch Mode</gizmoLabel>\n</li>' },
  { key: 'comps.accuracy.enabled', doc: 'building', container: 'comps', cls: 'CompProperties_AccuracyOverride', label: 'Accuracy Override',
    body: '<li Class="AbsolutelyMoreCannons.CompProperties_AccuracyOverride">\n  <swayReduction>0.1</swayReduction>\n  <recoilReduction>0.1</recoilReduction>\n  <spreadReduction>0.1</spreadReduction>\n</li>' },
  { key: 'comps.enclosed.enabled', doc: 'building', container: 'comps', cls: 'CompProperties_EnclosedTurret', label: 'Enclosed Turret',
    body: '<li Class="AbsolutelyMoreCannons.CompProperties_EnclosedTurret">\n  <bulletProtection>1.0</bulletProtection>\n  <explosiveProtection>0.8</explosiveProtection>\n  <temperatureProtection>0.5</temperatureProtection>\n  <hidePawnGraphics>true</hidePawnGraphics>\n</li>' },
  { key: 'smoker.enabled', doc: 'building', container: 'comps', cls: 'CompProperties_TurretSmoker', label: 'Smoker Component',
    body: '<li Class="AbsolutelyMoreCannons.CompProperties_TurretSmoker">\n  <muzzleEnabled>false</muzzleEnabled>\n  <heatEnabled>false</heatEnabled>\n  <shockwaveEnabled>false</shockwaveEnabled>\n</li>' },
  { key: 'barrel.enabled', doc: 'building', container: 'modExtensions', cls: 'TurretBarrelExtension', label: 'Barrel Extension',
    body: '<li Class="AbsolutelyMoreCannons.TurretBarrelExtension">\n  <barrelDrawSize>1.0</barrelDrawSize>\n  <barrelOffset>(0,0,0.0)</barrelOffset>\n  <drawOnTop>false</drawOnTop>\n</li>' },

  { key: 'comps.hasTurretBarrel', doc: 'building', container: 'comps', cls: 'CompProperties_TurretBarrel', label: 'Turret Barrel Comp',
    body: '<li Class="AbsolutelyMoreCannons.CompProperties_TurretBarrel" />' },
  { key: 'comps.hasPreserveAmmo', doc: 'building', container: 'comps', cls: 'CompProperties_TurretPreserveAmmo', label: 'Preserve Ammo',
    body: '<li Class="AbsolutelyMoreCannons.CompProperties_TurretPreserveAmmo" />' },
  { key: 'comps.hasSprayDiscipline', doc: 'building', container: 'comps', cls: 'CompProperties_TurretSprayDiscipline', label: 'Spray Discipline Comp',
    body: '<li Class="AbsolutelyMoreCannons.CompProperties_TurretSprayDiscipline" />' },
  { key: 'comps.hasFireArc', doc: 'building', container: 'comps', cls: 'CompProperties_FireArc', label: 'Fire Arc',
    body: '<li Class="CombatExtended.CompProperties_FireArc">\n  <spanRange>\n    <min>30</min>\n    <max>180</max>\n  </spanRange>\n  <maxSpanDeviation>90</maxSpanDeviation>\n</li>' },
  { key: 'comps.hasSuppressionImmunity', doc: 'building', container: 'modExtensions', cls: 'TurretSuppressionImmunityExtension', label: 'Suppression Immunity',
    body: '<li Class="AbsolutelyMoreCannons.TurretSuppressionImmunityExtension">\n  <preventOperatorSuppression>true</preventOperatorSuppression>\n</li>' },
  { key: 'viewTransfer.enabled', doc: 'building', container: 'comps', cls: 'CompProperties_TurretViewTransfer', label: 'View Transfer',
    body: '<li Class="AbsolutelyMoreCannons.CompProperties_TurretViewTransfer">\n  <transferViewOrigin>true</transferViewOrigin>\n  <ignoreSelfOcclusion>true</ignoreSelfOcclusion>\n</li>' },
  { key: 'tracer.enabled', doc: 'building', container: 'comps', cls: 'CompProperties_TracerLine', label: 'Tracer Line',
    body: '<li Class="AbsolutelyMoreCannons.CompProperties_TracerLine">\n  <lineLength>25.0</lineLength>\n  <lineWidth>0.06</lineWidth>\n  <durationTicks>2</durationTicks>\n</li>' },
  { key: 'tracking.enabled', doc: 'building', container: 'modExtensions', cls: 'TurretTrackingExtension', label: 'Mid-Burst Tracking',
    body: '<li Class="AbsolutelyMoreCannons.TurretTrackingExtension">\n  <enableMidBurstTracking>true</enableMidBurstTracking>\n</li>' },
  { key: 'nonSnap.enabled', doc: 'building', container: 'modExtensions', cls: 'NonSnapTurretExtension', label: 'Non-Snap Rotation',
    body: '<li Class="CombatExtended.NonSnapTurretExtension">\n  <speed>0.5</speed>\n  <preferedAngleRange>30</preferedAngleRange>\n</li>' },
  { key: 'spray.enabled', doc: 'building', container: 'modExtensions', cls: 'TurretSprayDisciplineExtension', label: 'Spray Discipline',
    body: '<li Class="AbsolutelyMoreCannons.TurretSprayDisciplineExtension">\n  <defaultEnableSprayDiscipline>true</defaultEnableSprayDiscipline>\n  <shotsPerTarget>10</shotsPerTarget>\n  <cycleConeDegrees>10</cycleConeDegrees>\n</li>' },

  // Weapon-side components. `doc` routes the add/remove to the weapon ThingDef
  // and its file instead of the building's.
  { key: 'chargeBoost.enabled', doc: 'weapon', container: 'modExtensions', cls: 'TurretChargeBoostExtension', label: 'Charge Boost',
    body: '<li Class="AbsolutelyMoreCannons.TurretChargeBoostExtension">\n  <chargeOffset>1</chargeOffset>\n</li>' },
  { key: 'clamping.enabled', doc: 'weapon', container: 'modExtensions', cls: 'TurretClampingExtension', label: 'Turret Clamping',
    body: '<li Class="AbsolutelyMoreCannons.TurretClampingExtension">\n  <maxVerticalDeviation>2</maxVerticalDeviation>\n  <maxRotationDeviation>2</maxRotationDeviation>\n</li>' },
  { key: 'gunDraw.enabled', doc: 'weapon', container: 'modExtensions', cls: 'GunDrawExtension', label: 'Casing Draw',
    body: '<li Class="CombatExtended.GunDrawExtension">\n  <CasingOffset>0,0</CasingOffset>\n  <CasingAngleOffset>180</CasingAngleOffset>\n</li>' },
  { key: 'fireModes.enabled', doc: 'weapon', container: 'comps', cls: 'CompProperties_FireModes', label: 'Fire Modes',
    body: '<li Class="CombatExtended.CompProperties_FireModes">\n  <aiUseBurstMode>true</aiUseBurstMode>\n  <aiAimMode>AimedShot</aiAimMode>\n</li>' },
  { key: 'charges.enabled', doc: 'weapon', container: 'comps', cls: 'CompProperties_Charges', label: 'Charge Speeds',
    body: '<li Class="CombatExtended.CompProperties_Charges">\n  <chargeSpeeds>\n    <li>30</li>\n  </chargeSpeeds>\n</li>' },
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
