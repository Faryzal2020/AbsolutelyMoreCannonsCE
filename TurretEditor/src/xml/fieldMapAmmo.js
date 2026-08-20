import { coerce, getPath, setPath } from './fieldMap.js';

export const AMMO_FIELDS = [
  // Item (AmmoDef)
  { key: 'label', doc: 'ammo', path: [{ tag: 'label' }], type: 'string' },
  { key: 'ammoClass', doc: 'ammo', path: [{ tag: 'ammoClass' }], type: 'string' },
  { key: 'stats.marketValue', doc: 'ammo', path: [{ tag: 'statBases' }, { tag: 'MarketValue' }], type: 'number' },
  { key: 'stats.mass', doc: 'ammo', path: [{ tag: 'statBases' }, { tag: 'Mass' }], type: 'number' },
  { key: 'stats.bulk', doc: 'ammo', path: [{ tag: 'statBases' }, { tag: 'Bulk' }], type: 'number' },
  { key: 'stats.stackLimit', doc: 'ammo', path: [{ tag: 'stackLimit' }], type: 'number' },
  { key: 'stats.cookOffFlashScale', doc: 'ammo', path: [{ tag: 'cookOffFlashScale' }], type: 'number' },
  { key: 'textures.ammo', doc: 'ammo', path: [{ tag: 'graphicData' }, { tag: 'texPath' }], type: 'string' },

  // Recipe (RecipeDef)
  { key: 'recipe.workAmount', doc: 'recipe', path: [{ tag: 'workAmount' }], type: 'number' },

  // Shared Payload (Applied to Direct AND Indirect Projectiles)
  { key: 'payload.damageDef', doc: 'shared_projectile', path: [{ tag: 'projectile' }, { tag: 'damageDef' }], type: 'string' },
  { key: 'payload.damageAmountBase', doc: 'shared_projectile', path: [{ tag: 'projectile' }, { tag: 'damageAmountBase' }], type: 'number' },
  { key: 'payload.armorPenetrationSharp', doc: 'shared_projectile', path: [{ tag: 'projectile' }, { tag: 'armorPenetrationSharp' }], type: 'number' },
  { key: 'payload.armorPenetrationBlunt', doc: 'shared_projectile', path: [{ tag: 'projectile' }, { tag: 'armorPenetrationBlunt' }], type: 'number' },
  { key: 'payload.explosionRadius', doc: 'shared_projectile', path: [{ tag: 'projectile' }, { tag: 'explosionRadius' }], type: 'number' },
  { key: 'payload.soundExplode', doc: 'shared_projectile', path: [{ tag: 'projectile' }, { tag: 'soundExplode' }], type: 'string' },
  { key: 'payload.applyNeighbors', doc: 'shared_projectile', path: [{ tag: 'projectile' }, { tag: 'applyDamageToExplosionCellsNeighbors' }], type: 'boolean' },
  { key: 'payload.aimHeightOffset', doc: 'shared_projectile', path: [{ tag: 'projectile' }, { tag: 'aimHeightOffset' }], type: 'number' },

  // Secondary Explosive
  { key: 'secondary.damageAmountBase', doc: 'shared_projectile', path: [{ tag: 'comps' }, { tag: 'li', cls: 'CompProperties_ExplosiveCE' }, { tag: 'damageAmountBase' }], type: 'number' },
  { key: 'secondary.explosiveDamageType', doc: 'shared_projectile', path: [{ tag: 'comps' }, { tag: 'li', cls: 'CompProperties_ExplosiveCE' }, { tag: 'explosiveDamageType' }], type: 'string' },
  { key: 'secondary.explosiveRadius', doc: 'shared_projectile', path: [{ tag: 'comps' }, { tag: 'li', cls: 'CompProperties_ExplosiveCE' }, { tag: 'explosiveRadius' }], type: 'number' },

  // Airburst Fuse Extension
  { key: 'airburst.armingTicks', doc: 'shared_projectile', path: [{ tag: 'modExtensions' }, { tag: 'li', cls: 'AirburstExtension' }, { tag: 'armingTicks' }], type: 'number' },
  { key: 'airburst.type', doc: 'shared_projectile', path: [{ tag: 'modExtensions' }, { tag: 'li', cls: 'AirburstExtension' }, { tag: 'type' }], type: 'string' },
  { key: 'airburst.proximityRadius', doc: 'shared_projectile', path: [{ tag: 'modExtensions' }, { tag: 'li', cls: 'AirburstExtension' }, { tag: 'proximityRadius' }], type: 'number' },
  { key: 'airburst.burstAltitude', doc: 'shared_projectile', path: [{ tag: 'modExtensions' }, { tag: 'li', cls: 'AirburstExtension' }, { tag: 'burstAltitude' }], type: 'number' },

  // Guidance
  { key: 'guidance.guidanceOnDescending', doc: 'shared_projectile', path: [{ tag: 'projectile' }, { tag: 'guidanceOnDescending' }], type: 'boolean' },
  { key: 'guidance.homingAcceleration', doc: 'shared_projectile', path: [{ tag: 'projectile' }, { tag: 'homingAcceleration' }], type: 'number' },
  { key: 'guidance.retargetRadius', doc: 'shared_projectile', path: [{ tag: 'projectile' }, { tag: 'retargetRadius' }], type: 'number' },
  { key: 'guidance.guidanceDelay', doc: 'shared_projectile', path: [{ tag: 'projectile' }, { tag: 'guidanceDelay' }], type: 'number' },
  { key: 'guidance.gravityFactor', doc: 'shared_projectile', path: [{ tag: 'projectile' }, { tag: 'gravityFactor' }], type: 'number' },
  { key: 'guidance.trajectoryWorker', doc: 'shared_projectile', path: [{ tag: 'projectile' }, { tag: 'trajectoryWorker' }], type: 'string' },

  // Direct Mode Specifics
  { key: 'direct.speed', doc: 'direct_projectile', path: [{ tag: 'projectile' }, { tag: 'speed' }], type: 'number' },
  { key: 'direct.dropsCasings', doc: 'direct_projectile', path: [{ tag: 'projectile' }, { tag: 'dropsCasings' }], type: 'boolean' },
  { key: 'direct.soundImpactAnticipate', doc: 'direct_projectile', path: [{ tag: 'projectile' }, { tag: 'soundImpactAnticipate' }], type: 'string' },

  // Indirect Mode Specifics
  { key: 'indirect.speed', doc: 'indirect_projectile', path: [{ tag: 'projectile' }, { tag: 'speed' }], type: 'number' },
  { key: 'indirect.flyOverhead', doc: 'indirect_projectile', path: [{ tag: 'projectile' }, { tag: 'flyOverhead' }], type: 'boolean' },
  { key: 'indirect.shellingTilesPerTick', doc: 'indirect_projectile', path: [{ tag: 'projectile' }, { tag: 'shellingProps' }, { tag: 'tilesPerTick' }], type: 'number' },
  { key: 'indirect.shellingRange', doc: 'indirect_projectile', path: [{ tag: 'projectile' }, { tag: 'shellingProps' }, { tag: 'range' }], type: 'number' },
  { key: 'indirect.shellingDamage', doc: 'indirect_projectile', path: [{ tag: 'projectile' }, { tag: 'shellingProps' }, { tag: 'damage' }], type: 'number' },
  { key: 'indirect.soundHitThickRoof', doc: 'indirect_projectile', path: [{ tag: 'projectile' }, { tag: 'soundHitThickRoof' }], type: 'string' },
];

export const AMMO_FIELD_BY_KEY = new Map(AMMO_FIELDS.map((f) => [f.key, f]));
