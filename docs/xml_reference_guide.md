# Absolutely More Cannons (AMC) - CE Version 1.6
## Comprehensive XML Reference Documentation

This document serves as the complete technical specification and XML parameter reference for all custom C# classes, `DefModExtension`s, `ThingComp`s, projectile classes, and trajectory workers introduced in **Absolutely More Cannons - CE Version**.

---

## Table of Contents

1. [DefModExtensions (`<modExtensions>`)](#1-defmodextensions-modextensions)
   - [AirburstExtension](#airburstextension)
   - [FCSPropertiesDefExtension](#fcspropertiesdefextension)
   - [TurretBarrelExtension](#turretbarrelextension)
   - [TurretChargeBoostExtension](#turretchargeboostextension)
   - [TurretPreserveAmmoExtension](#turretpreserveammoextension)
   - [TurretSuppressionImmunityExtension](#turretsuppressionimmunityextension)
   - [TurretTrackingExtension](#turrettrackingextension)
2. [ThingComponents & CompProperties (`<comps>`)](#2-thingcomponents--compproperties-comps)
   - [CompProperties_AccuracyOverride](#compproperties_accuracyoverride)
   - [CompProperties_TurretBarrel](#compproperties_turretbarrel)
   - [CompProperties_TurretFCS](#compproperties_turretfcs)
   - [CompProperties_TurretModeSwap](#compproperties_turretmodeswap)
   - [CompProperties_TurretPreserveAmmo](#compproperties_turretpreserveammo)
   - [CompProperties_TurretSmoker](#compproperties_turretsmoker)
   - [CompProperties_AirburstFragments](#compproperties_airburstfragments)
3. [Custom Projectile & Trajectory Classes](#3-custom-projectile--trajectory-classes)
   - [ProjectilePropertiesCE](#projectilepropertiesce)
   - [ProjectileCE_Airburst](#projectilece_airburst)
   - [ProjectileCE_AMCFragment](#projectilece_amcfragment)
   - [Trajectory Workers](#trajectory-workers)
4. [Full XML Configuration Templates](#4-full-xml-configuration-templates)
   - [Example 1: Turret Def with Barrel Animation, Mode Swap & Smoker](#example-1-turret-def-with-barrel-animation-mode-swap--smoker)
   - [Example 2: Airburst Projectile with 3D Trajectory-Aligned Cone](#example-2-airburst-projectile-with-3d-trajectory-aligned-cone)
   - [Example 3: Guided VLS Rocket Projectile](#example-3-guided-vls-rocket-projectile)
   - [Example 4: FCS Module Item Definition](#example-4-fcs-module-item-definition)

---

## 1. DefModExtensions (`<modExtensions>`)

DefModExtensions are custom data structures attached to RimWorld `Def`s inside the `<modExtensions>` block.

---

### `AirburstExtension`
* **Class**: `AbsolutelyMoreCannons.AirburstExtension`
* **Target Def**: `ThingDef` (Projectiles)
* **Description**: Controls mid-air detonation logic for airburst shells, supporting both Altitude-based trigger (for indirect artillery) and Flak proximity trigger (for direct fire).

#### Parameters:
| Tag | Type | Default | Description |
| :--- | :--- | :--- | :--- |
| `<armingTicks>` | `int` | `10` | Minimum flight ticks required before the airburst fuse arms. Prevents premature detonation near the launcher muzzle. |
| `<type>` | `AirburstType` | `Altitude` | Detonation trigger mode. Valid options: `Altitude` (detonates when shell drops to target height) or `Flak` (detonates when target or hostile pawn is within radius). |
| `<burstAltitude>` | `float` | `3.0` | Vertical altitude threshold in meters (`ExactPosition.y`). When descending projectile reaches or falls below this height, it detonates. |
| `<proximityRadius>` | `float` | `4.0` | Detection radius in map cells for `Flak` (Proximity) mode. Detonates if the intended target or an enemy pawn enters this distance. |

#### XML Example:
```xml
<modExtensions>
  <li Class="AbsolutelyMoreCannons.AirburstExtension">
    <armingTicks>12</armingTicks>
    <type>Altitude</type>
    <burstAltitude>3.5</burstAltitude>
    <proximityRadius>4.0</proximityRadius>
  </li>
</modExtensions>
```

---

### `FCSPropertiesDefExtension`
* **Class**: `AbsolutelyMoreCannons.FCSPropertiesDefExtension`
* **Target Def**: `ThingDef` (FCS Items / Fire Control System modules)
* **Description**: Specifies performance and accuracy multipliers applied to unmanned turrets when this FCS item module is physically installed into the turret's internal container.

#### Parameters:
| Tag | Type | Default | Description |
| :--- | :--- | :--- | :--- |
| `<swayMultiplier>` | `float` | `1.0` | Weapon sway multiplier (`0.15` = 85% sway reduction, `0.5` = 50% reduction). |
| `<recoilMultiplier>` | `float` | `1.0` | Recoil kick multiplier (`0.15` = 85% recoil reduction). |
| `<spreadMultiplier>` | `float` | `1.0` | Mechanical shot dispersion multiplier (`0.15` = 85% tighter shot grouping). |
| `<aimTimeMultiplier>` | `float` | `1.0` | Target warmup aim time multiplier (`0.15` = 85% faster target acquisition lock-on). |
| `<rangeMultiplier>` | `float` | `1.0` | Maximum weapon range multiplier (`1.10` = +10% maximum engagement range). |
| `<trackingAbility>` | `bool` | `false` | When `true`, enables continuous mid-burst target tracking and re-aiming between burst shots. |

#### XML Example:
```xml
<modExtensions>
  <li Class="AbsolutelyMoreCannons.FCSPropertiesDefExtension">
    <swayMultiplier>0.15</swayMultiplier>
    <recoilMultiplier>0.15</recoilMultiplier>
    <spreadMultiplier>0.15</spreadMultiplier>
    <aimTimeMultiplier>0.15</aimTimeMultiplier>
    <rangeMultiplier>1.10</rangeMultiplier>
    <trackingAbility>true</trackingAbility>
  </li>
</modExtensions>
```

---

### `TurretBarrelExtension`
* **Class**: `AbsolutelyMoreCannons.TurretBarrelExtension`
* **Target Def**: `ThingDef` (Turret Building Defs)
* **Description**: Defines visual rendering, pivot offset, multi-barrel layouts, interactive gizmo controls, recoil, gatling texture-spinning, and muzzle flash animations for turret barrels.

#### Parameters:
| Tag | Type | Default | Description |
| :--- | :--- | :--- | :--- |
| `<barrelGraphic>` | `GraphicData` | `null` | Graphic data for the main animated barrel texture (`texPath`, `graphicClass`, `drawSize`). |
| `<underBarrelGraphic>` | `GraphicData` | `null` | Optional static graphic rendered underneath the barrel but above the turret base (rotates with turret head). |
| `<barrelOffset>` | `Vector3` | `(0, 0, 0)` | Pivot vector offset `(X, Y, Z)` from the turret base center to the barrel base. |
| `<barrelDrawSize>` | `float` | `1.0` | Visual scale multiplier for barrel rendering. |
| `<drawLayerOffset>` | `float` | `0.05` | Altitude layer offset for ordering relative to base and top. |
| `<drawOnTop>` | `bool` | `false` | When `true`, renders the barrel graphic after and on top of the turret top graphic. |
| `<drawWhenDestroyed>` | `bool` | `true` | When `true`, renders barrel graphic even when turret building is destroyed. |
| `<inheritTurretRotation>` | `bool` | `true` | Inherits facing rotation angle from main turret top. |
| `<barrelAmount>` | `int` | `1` | Number of parallel barrels drawn side-by-side. |
| `<barrelSpacing>` | `float` | `1.0` | Distance between multi-barrel instances measured in tiles. |
| `<sequentialFiring>` | `bool` | `false` | When `true`, cycles recoil/firing animations sequentially through barrels per shot rather than animating all together. |
| `<maxRPMs>` | `List<float>` | `[]` | Selectable fire-rate RPM options accessible on the player gizmo menu. |
| `<selectableBurstCounts>`| `List<int>` | `[]` | Selectable burst fire counts accessible on the player gizmo menu. |
| `<recoilAnimation>` | `RecoilAnimation` | *Sub-object* | Configuration for barrel kickback recoil motion (see below). |
| `<spinningAnimation>` | `SpinningAnimation` | *Sub-object* | Configuration for multi-frame rotary gatling spin animations (see below). |
| `<firingAnimation>` | `FiringAnimation` | *Sub-object* | Configuration for barrel scale pulse, muzzle flashes, and burst sound loops (see below). |

#### Sub-Class: `RecoilAnimation`
| Tag | Type | Default | Description |
| :--- | :--- | :--- | :--- |
| `<enabled>` | `bool` | `true` | Toggles recoil animation. |
| `<maxDistance>` | `float` | `0.2` | Maximum backward displacement distance in cells. |
| `<recoilDuration>` | `int` | `5` | Ticks spent moving backward during recoil kick. |
| `<returnDuration>` | `int` | `10` | Ticks spent returning forward to baseline position. |
| `<useRecoilCurve>` | `bool` | `true` | Uses cubic ease-out acceleration for instant kick & smooth deceleration. |
| `<useReturnCurve>` | `bool` | `false` | Uses cubic ease-out for return phase. |
| `<curve>` | `bool` | `false` | Legacy shorthand that sets both `useRecoilCurve` and `useReturnCurve`. |
| `<affectsRotation>` | `bool` | `false` | Enables subtle rotation angle distortion during recoil. |

#### Sub-Class: `SpinningAnimation`
| Tag | Type | Default | Description |
| :--- | :--- | :--- | :--- |
| `<enabled>` | `bool` | `false` | Toggles texture-based rotary barrel spinning. |
| `<animationMode>` | `string` | `"RPMBased"` | Spin mode: `"RPMBased"` (with spin-up/spin-down ramp) or `"Cycling"` (constant rate). |
| `<maxRPM>` | `float` | `3000.0` | Maximum rotations per minute. |
| `<spindownTime>` | `float` | `2.0` | Seconds required to decelerate from max RPM to full stop. |
| `<frameCount>` | `int` | `4` | Number of texture frames in `Graphic_Collection`. |
| `<barrelCount>` | `int` | `6` | Visual barrel count on rotary cluster used to scale frame transition frequency. |
| `<spinUpSound>` | `string` | `null` | `SoundDef` name played when spin-up starts. |
| `<spinDownSound>` | `string` | `null` | `SoundDef` name played when spin-down starts. |

#### Sub-Class: `FiringAnimation`
| Tag | Type | Default | Description |
| :--- | :--- | :--- | :--- |
| `<enabled>` | `bool` | `true` | Toggles firing animation scale/flash. |
| `<durationTicks>` | `int` | `5` | Pulse animation duration in ticks. |
| `<maxScale>` | `float` | `1.1` | Peak graphic scale multiplier when firing. |
| `<maxPositionOffset>` | `float` | `0.05` | Peak forward position offset during shot pulse. |
| `<drawFlash>` | `bool` | `false` | Renders a procedural muzzle flash light pulse. |
| `<flashColor>` | `Color` | `(1,0.8,0.4,1)` | Muzzle flash RGBA color. |
| `<flashSize>` | `float` | `1.0` | Flash diameter scale multiplier. |
| `<flashBrightness>` | `float` | `2.0` | Flash brightness multiplier. |
| `<flashOffset>` | `float` | `0.0` | Offset forward along aiming direction from barrel tip for flash center. |
| `<muzzleFlashEffect>`| `string` | `null` | `EffecterDef` name for muzzle light (e.g. `AMC_MuzzleFlashLight`). |
| `<burstSound>` | `string` | `null` | `SoundDef` name for sustained burst audio loop playing across full burst duration. |
| `<projectileSpawnOffset>`| `float` | `0.0` | Offset in tiles to shift projectile spawn point forward from turret center to visual barrel tip. |

---

### `TurretChargeBoostExtension`
* **Class**: `AbsolutelyMoreCannons.TurretChargeBoostExtension`
* **Target Def**: `ThingDef` (Projectiles or AmmoDefs)
* **Description**: Forces indirect fire artillery to use higher propellant charge levels, preventing shallow arcs for airburst artillery without modifying base CE charge tables.

#### Parameters:
| Tag | Type | Default | Description |
| :--- | :--- | :--- | :--- |
| `<chargeOffset>` | `int` | `1` | Number of charge levels added to baseline CE calculation (e.g., Charge 1 -> Charge 2). Clamped to maximum charge index available. |
| `<enabled>` | `bool` | `true` | Toggles charge boost state. |

---

### `TurretPreserveAmmoExtension`
* **Class**: `AbsolutelyMoreCannons.TurretPreserveAmmoExtension`
* **Target Def**: `ThingDef` (Turret Building Defs)
* **Description**: Configures default state and user permissions for the "Preserve Ammo" feature, which halts firing if a target is killed mid-burst.

#### Parameters:
| Tag | Type | Default | Description |
| :--- | :--- | :--- | :--- |
| `<defaultPreserveAmmo>` | `bool` | `true` | Default state when turret is built. |
| `<allowToggle>` | `bool` | `true` | Determines whether player can toggle Preserve Ammo on/off via gizmo. |

---

### `TurretSuppressionImmunityExtension`
* **Class**: `AbsolutelyMoreCannons.TurretSuppressionImmunityExtension`
* **Target Def**: `ThingDef` (Manned Turret Building Defs)
* **Description**: Prevents gunner pawns manning this turret from hunkering down or fleeing due to Combat Extended suppression.

#### Parameters:
| Tag | Type | Default | Description |
| :--- | :--- | :--- | :--- |
| `<preventOperatorSuppression>` | `bool` | `true` | When `true`, operator pawn ignores suppression mechanics while manning turret. |

---

### `TurretTrackingExtension`
* **Class**: `AbsolutelyMoreCannons.TurretTrackingExtension`
* **Target Def**: `ThingDef` (Turret Building Defs)
* **Description**: Enables continuous target leading and physical rotation updates during multi-shot burst fire.

#### Parameters:
| Tag | Type | Default | Description |
| :--- | :--- | :--- | :--- |
| `<enableMidBurstTracking>` | `bool` | `true` | When `true`, turret continuously re-aims at moving targets between shots within a single burst. |

---

## 2. ThingComponents & CompProperties (`<comps>`)

ThingComponents are active runtime components defined inside the `<comps>` list of `ThingDef`s.

---

### `CompProperties_AccuracyOverride`
* **Comp Class**: `AbsolutelyMoreCannons.Comp_AccuracyOverride`
* **Target Def**: `ThingDef` (Turrets)
* **Description**: Provides per-turret stat overrides for sway, recoil, and spread reduction that blend with global mod settings.

#### Parameters:
| Tag | Type | Default | Description |
| :--- | :--- | :--- | :--- |
| `<swayReduction>` | `float` | `0.0` | Fractional sway reduction (`0.2` = 20% reduction). |
| `<recoilReduction>` | `float` | `0.0` | Fractional recoil reduction (`0.3` = 30% reduction). |
| `<spreadReduction>` | `float` | `0.0` | Fractional spread reduction (`0.25` = 25% reduction). |

---

### `CompProperties_TurretBarrel`
* **Comp Class**: `AbsolutelyMoreCannons.CompTurretBarrel`
* **Target Def**: `ThingDef` (Turrets)
* **Description**: Component wrapper that handles barrel animation, rendering, and gizmo controls. Can override settings from `TurretBarrelExtension` or be configured directly inside `<comps>`.

#### Parameters:
*Supports all parameters from `TurretBarrelExtension`, plus:*
| Tag | Type | Default | Description |
| :--- | :--- | :--- | :--- |
| `<chargeBoostOffset>` | `int` | `0` | Optional charge level offset override for indirect fire. |

---

### `CompProperties_TurretFCS`
* **Comp Class**: `AbsolutelyMoreCannons.CompTurretFCS`
* **Target Def**: `ThingDef` (Unmanned Turrets)
* **Description**: Marker component for unmanned turrets. Enables the internal storage container for Fire Control System item modules and displays FCS gizmo/inspect status.

#### Parameters:
*No XML child parameters required.*
```xml
<li Class="AbsolutelyMoreCannons.CompProperties_TurretFCS" />
```

---

### `CompProperties_TurretModeSwap`
* **Comp Class**: `AbsolutelyMoreCannons.CompTurretModeSwap`
* **Target Def**: `ThingDef` (Turrets)
* **Description**: Adds a gizmo button allowing players to dynamically swap the turret between two operational modes (e.g. Direct Fire vs. Indirect Fire, AP vs. HE modes).

#### Parameters:
| Tag | Type | Default | Description |
| :--- | :--- | :--- | :--- |
| `<alternateDef>` | `string` | *Required* | `defName` of the target turret `ThingDef` to convert into upon mode swap. |
| `<gizmoLabel>` | `string` | `null` | Label text displayed on gizmo button. |
| `<gizmoDesc>` | `string` | `null` | Detailed tooltip description displayed on hover. |
| `<gizmoIcon>` | `string` | `null` | Texture path relative to `Textures/` directory for gizmo icon. |

---

### `CompProperties_TurretPreserveAmmo`
* **Comp Class**: `AbsolutelyMoreCannons.CompTurretPreserveAmmo`
* **Target Def**: `ThingDef` (Turrets)
* **Description**: Adds Preserve Ammo logic component to turrets.

#### Parameters:
| Tag | Type | Default | Description |
| :--- | :--- | :--- | :--- |
| `<defaultPreserveAmmo>` | `bool` | `true` | Initial preserve state upon construction. |

---

### `CompProperties_TurretSmoker`
* **Comp Class**: `AbsolutelyMoreCannons.CompTurretSmoker`
* **Target Def**: `ThingDef` (Turrets)
* **Description**: Multi-stage smoke and particle system handling muzzle puff, barrel heat smoke emissions, and radial ground shockwave puffs.

#### Parameters:

##### 1. Muzzle Smoke Parameters
| Tag | Type | Default | Description |
| :--- | :--- | :--- | :--- |
| `<muzzleEnabled>` | `bool` | `false` | Enables directional muzzle smoke on firing. |
| `<muzzleFleckDef>` | `FleckDef` | `Smoke` | Fleck definition spawned at muzzle. |
| `<muzzleParticleCount>` | `int` | `3` | Number of smoke particles spawned per shot. |
| `<muzzleSpawnDuration>` | `int` | `5` | Ticks over which particles are distributed. |
| `<muzzleOffset>` | `Vector3` | `(0,0,0)` | Offset `(X,Y,Z)` from barrel tip. |
| `<muzzleVelocity>` | `float` | `2.0` | Initial forward particle velocity. |
| `<muzzleVelDuration>` | `int` | `20` | Ticks before particle transitions to wind motion. |
| `<muzzleParticleSize>` | `string` | `"1.0"` | Particle scale (supports `"min~max"` format like `"1.2~2.5"`). |
| `<directionCone>` | `float` | `0.0` | Angular dispersion spread cone in degrees. |
| `<muzzleSpawnDelay>` | `int` | `2` | Delay in ticks after shot before spawning particles. |

##### 2. Heat Smoke Parameters
| Tag | Type | Default | Description |
| :--- | :--- | :--- | :--- |
| `<heatEnabled>` | `bool` | `false` | Enables heat smoke emission when gun is hot. |
| `<heatFleckDef>` | `FleckDef` | `Smoke` | Fleck definition for heat smoke. |
| `<heatThreshold>` | `int` | `4` | Accumulated shot count required before heat smoke activates. |
| `<heatDecayRate>` | `float` | `0.5` | Heat reduction per second. |
| `<heatOffset>` | `Vector3` | `(0,0,0)` | Offset from turret center. |
| `<heatEmissionRate>` | `float` | `2.0` | Particles per second when active. |
| `<heatParticleSize>` | `float` | `1.0` | Particle size scale. |
| `<heatEmissionPoints>` | `int` | `1` | Number of emission points along barrel length. |
| `<heatEmissionSpacing>`| `float` | `0.5` | Distance in cells between emission points. |

##### 3. Shockwave Smoke Parameters
| Tag | Type | Default | Description |
| :--- | :--- | :--- | :--- |
| `<shockwaveEnabled>` | `bool` | `false` | Enables ground shockwave ring smoke on firing. |
| `<shockwaveFleckDef>` | `FleckDef` | `Smoke` | Fleck definition for shockwave ring. |
| `<shockwaveRadius>` | `float` | `1.5` | Ring radius in map cells. |
| `<shockwaveDensity>` | `int` | `8` | Total particle count distributed around ring. |
| `<shockwaveOffset>` | `Vector3` | `(0,0,0)` | Vertical height offset. |
| `<shockwaveParticleSize>`| `float` | `1.0` | Particle size scale. |

---

### `CompProperties_AirburstFragments`
* **Comp Class**: `AbsolutelyMoreCannons.CompAirburstFragments`
* **Target Def**: `ThingDef` (Projectiles)
* **Description**: Computes a true 3D trajectory-aligned fragment dispersion cone, orienting fragment pitch with the shell's velocity vector.

#### Parameters:
| Tag | Type | Default | Description |
| :--- | :--- | :--- | :--- |
| `<fragments>` | `List<Spec>` | `[]` | List of `<liClass="AbsolutelyMoreCannons.AirburstFragmentSpec">` elements. |
| `<fragSpeedFactor>` | `float` | `1.0` | Global speed multiplier for all spawned fragments. |
| `<fragShadowChance>` | `float` | `0.2` | Probability (`0.0` to `1.0`) for fragment to cast shadow. |
| `<fragAngleRange>` | `FloatRange` | `-25~25` | Pitch angle spread in degrees relative to 3D flight trajectory vector. |
| `<fragXZAngleRange>` | `FloatRange` | `-25~25` | Yaw angle spread in degrees relative to 3D flight trajectory vector. |
| `<useEllipticalCone>` | `bool` | `false` | Constrains fragment spread to an elliptical cone rather than rectangular box. |
| `<usePolarDiskSampling>`| `bool` | `false` | When `true`, uses direct polar disk mapping for perfectly uniform density; otherwise uses rejection sampling. |
| `<airburstSound>` | `SoundDef` | `null` | Sound played upon mid-air detonation. |
| `<airburstFlashFleck>` | `FleckDef` | `null` | Fleck spawned for detonation flash. |
| `<airburstFlashScale>` | `float` | `4.0` | Flash scale multiplier. |
| `<airburstSmokeFleck>` | `FleckDef` | `null` | Fleck spawned for detonation smoke cloud. |
| `<airburstSmokeScale>` | `float` | `3.0` | Smoke scale multiplier. |
| `<airburstFlashEffect>`| `EffecterDef` | `null` | `EffecterDef` spawned at airburst location (e.g. `AMC_MuzzleFlashLight`). |
| `<airburstEffecter>` | `EffecterDef` | `null` | Secondary custom effecter definition. |

#### Sub-Element: `AirburstFragmentSpec`
```xml
<li Class="AbsolutelyMoreCannons.AirburstFragmentSpec">
  <thingDef>Fragment_Large</thingDef>
  <count>35</count>
  <damageAmountBase>45</damageAmountBase>
  <damageDef>Fragment</damageDef>
  <armorPenetrationSharp>12.0</armorPenetrationSharp>
  <armorPenetrationBlunt>40.0</armorPenetrationBlunt>
  <explosionRadius>0</explosionRadius>
  <speed>450</speed>
</li>
```
| Tag | Type | Default | Description |
| :--- | :--- | :--- | :--- |
| `<thingDef>` | `ThingDef` | *Required* | Projectile `ThingDef` spawned as fragment (e.g. `Fragment_Large`). |
| `<count>` | `int` | `1` | Number of fragments spawned. |
| `<damageAmountBase>` | `int` | `-1` | Damage amount override (-1 = use `thingDef` default). |
| `<damageDef>` | `DamageDef` | `null` | Damage type override (null = use `thingDef` default). |
| `<armorPenetrationSharp>`| `float` | `-1.0` | Sharp AP override in mm RHA (-1 = use default). |
| `<armorPenetrationBlunt>`| `float` | `-1.0` | Blunt AP override in MPa (-1 = use default). |
| `<explosionRadius>` | `float` | `0.0` | If > 0, fragment detonates into an explosion on impact. |
| `<explosionDamageDef>`| `DamageDef` | `null` | Damage type for explosive fragment impact. |
| `<speed>` | `float` | `-1.0` | Initial launch speed override in m/s (-1 = use default). |

---

## 3. Custom Projectile & Trajectory Classes

---

### `ProjectilePropertiesCE`
* **Class**: `AbsolutelyMoreCannons.ProjectilePropertiesCE`
* **Extends**: `CombatExtended.ProjectilePropertiesCE`
* **Usage**: Specified as `<projectile Class="AbsolutelyMoreCannons.ProjectilePropertiesCE">` inside projectile `ThingDef`s.

#### Parameters:
| Tag | Type | Default | Description |
| :--- | :--- | :--- | :--- |
| `<damageRadius>` | `float` | `0.0` | Radius in cells for silent area-of-effect suppression and fragment damage on impact. |
| `<guidanceDelay>` | `float` | `0.0` | Real-time seconds before homing / rocket steering activates. |
| `<guidanceDelayTicks>`| `int` | `-1` | Direct tick count delay before guidance activates (overrides `guidanceDelay` if >= 0). |
| `<guidanceOnDescending>`| `bool` | `false` | When `true`, guidance activates only after projectile reaches flight apex and starts descending (`velocity.y <= 0`). |
| `<retargetRadius>` | `float` | `0.0` | Search radius in map cells to acquire a new hostile target if the initial target dies mid-flight. |
| `<homingAcceleration>`| `float` | `0.0` | Homing steering rate limit in radians per tick. |
| `<vlsLaunchAngle>` | `float` | `0.0` | Steep vertical launch pitch angle override in degrees (e.g. `85.0`) for VLS missiles. |

---

### `ProjectileCE_Airburst`
* **Class**: `AbsolutelyMoreCannons.ProjectileCE_Airburst`
* **Extends**: `CombatExtended.ProjectileCE_Explosive`
* **Usage**: Set `<thingClass>AbsolutelyMoreCannons.ProjectileCE_Airburst</thingClass>` on projectile `ThingDef`. Intercepts tick execution to check `AirburstExtension` altitude/flak triggers and fires `CompAirburstFragments`.

---

### `ProjectileCE_AMCFragment`
* **Class**: `AbsolutelyMoreCannons.ProjectileCE_AMCFragment`
* **Extends**: `CombatExtended.ProjectileCE`
* **Usage**: Set `<thingClass>AbsolutelyMoreCannons.ProjectileCE_AMCFragment</thingClass>` on fragment projectile `ThingDef`s to enable per-instance AP overrides and impact explosions.

---

### Trajectory Workers
Specified using `<trajectoryWorkerClass>` inside projectile properties.

1. **`AbsolutelyMoreCannons.VLSTrajectoryWorker`**
   - Handles steep VLS upward launches (`vlsLaunchAngle`), guidance activation delays (`guidanceDelayTicks` / `guidanceOnDescending`), rocket motor acceleration, and automatic retargeting (`retargetRadius`).
2. **`AbsolutelyMoreCannons.DelayedSmartRocketTrajectoryWorker`**
   - Manages smart rockets with delayed motor ignition and retargeting.
3. **`AbsolutelyMoreCannons.DelayedHomingTrajectoryWorker`**
   - Manages homing bullets and guided artillery with delayed steering activation and retargeting.

---

## 4. Full XML Configuration Templates

### Example 1: Turret Def with Barrel Animation, Mode Swap & Smoker

```xml
<ThingDef ParentName="BuildingBase">
  <defName>AMC_76mmOtomelaraC</defName>
  <label>76mm OTO Melara Naval Gun</label>
  <thingClass>CombatExtended.Building_TurretGunCE</thingClass>

  <comps>
    <!-- Fire Control System Marker for Unmanned Turrets -->
    <li Class="AbsolutelyMoreCannons.CompProperties_TurretFCS" />

    <!-- Mode Swap Gizmo Component -->
    <li Class="AbsolutelyMoreCannons.CompProperties_TurretModeSwap">
      <alternateDef>AMC_76mmOtomelaraC_Indirect</alternateDef>
      <gizmoLabel>Switch to Indirect Fire</gizmoLabel>
      <gizmoDesc>Switch turret to high-arc indirect artillery bombardment mode.</gizmoDesc>
      <gizmoIcon>UI/Gizmos/IndirectFire</gizmoIcon>
    </li>

    <!-- Accuracy Override Component -->
    <li Class="AbsolutelyMoreCannons.CompProperties_AccuracyOverride">
      <swayReduction>0.20</swayReduction>
      <recoilReduction>0.30</recoilReduction>
      <spreadReduction>0.25</spreadReduction>
    </li>

    <!-- Multi-Stage Smoke Generator Component -->
    <li Class="AbsolutelyMoreCannons.CompProperties_TurretSmoker">
      <muzzleEnabled>true</muzzleEnabled>
      <muzzleParticleCount>4</muzzleParticleCount>
      <muzzleVelocity>3.5</muzzleVelocity>
      <muzzleParticleSize>1.2~2.8</muzzleParticleSize>
      <directionCone>15</directionCone>

      <heatEnabled>true</heatEnabled>
      <heatThreshold>3</heatThreshold>
      <heatEmissionRate>3.0</heatEmissionRate>

      <shockwaveEnabled>true</shockwaveEnabled>
      <shockwaveRadius>2.0</shockwaveRadius>
      <shockwaveDensity>10</shockwaveDensity>
    </li>
  </comps>

  <modExtensions>
    <!-- Barrel Graphic & Recoil Animation Extension -->
    <li Class="AbsolutelyMoreCannons.TurretBarrelExtension">
      <barrelGraphic>
        <texPath>Things/Building/NavalGuns/76mmOtomelaraC_Barrel</texPath>
        <graphicClass>Graphic_Single</graphicClass>
        <drawSize>(3.0, 3.0)</drawSize>
      </barrelGraphic>
      <barrelOffset>(0, 0, 0.4)</barrelOffset>

      <recoilAnimation>
        <enabled>true</enabled>
        <maxDistance>0.35</maxDistance>
        <recoilDuration>4</recoilDuration>
        <returnDuration>12</returnDuration>
        <useRecoilCurve>true</useRecoilCurve>
      </recoilAnimation>

      <firingAnimation>
        <enabled>true</enabled>
        <drawFlash>true</drawFlash>
        <flashSize>1.5</flashSize>
        <muzzleFlashEffect>AMC_MuzzleFlashLight</muzzleFlashEffect>
        <projectileSpawnOffset>1.2</projectileSpawnOffset>
      </firingAnimation>

      <maxRPMs>
        <li>80</li>
        <li>120</li>
      </maxRPMs>
      <selectableBurstCounts>
        <li>1</li>
        <li>3</li>
        <li>5</li>
      </selectableBurstCounts>
    </li>

    <li Class="AbsolutelyMoreCannons.TurretPreserveAmmoExtension">
      <defaultPreserveAmmo>true</defaultPreserveAmmo>
      <allowToggle>true</allowToggle>
    </li>
  </modExtensions>
</ThingDef>
```

---

### Example 2: Airburst Projectile with 3D Trajectory-Aligned Cone

```xml
<ThingDef ParentName="BaseBullet">
  <defName>Bullet_76mmOtomelara_Airburst</defName>
  <thingClass>AbsolutelyMoreCannons.ProjectileCE_Airburst</thingClass>

  <projectile Class="AbsolutelyMoreCannons.ProjectilePropertiesCE">
    <damageAmountBase>120</damageAmountBase>
    <armorPenetrationSharp>25</armorPenetrationSharp>
    <armorPenetrationBlunt>150</armorPenetrationBlunt>
    <speed>210</speed>
  </projectile>

  <comps>
    <li Class="AbsolutelyMoreCannons.CompProperties_AirburstFragments">
      <useEllipticalCone>true</useEllipticalCone>
      <usePolarDiskSampling>true</usePolarDiskSampling>
      <fragAngleRange>-20~20</fragAngleRange>
      <fragXZAngleRange>-20~20</fragXZAngleRange>

      <airburstFlashScale>5.0</airburstFlashScale>
      <airburstSmokeScale>4.0</airburstSmokeScale>
      <airburstFlashEffect>AMC_MuzzleFlashLight</airburstFlashEffect>

      <fragments>
        <li Class="AbsolutelyMoreCannons.AirburstFragmentSpec">
          <thingDef>Fragment_Large</thingDef>
          <count>40</count>
          <damageAmountBase>50</damageAmountBase>
          <armorPenetrationSharp>14</armorPenetrationSharp>
          <armorPenetrationBlunt>60</armorPenetrationBlunt>
          <speed>500</speed>
        </li>
      </fragments>
    </li>
  </comps>

  <modExtensions>
    <li Class="AbsolutelyMoreCannons.AirburstExtension">
      <armingTicks>15</armingTicks>
      <type>Altitude</type>
      <burstAltitude>3.0</burstAltitude>
    </li>
    <li Class="AbsolutelyMoreCannons.TurretChargeBoostExtension">
      <chargeOffset>1</chargeOffset>
    </li>
  </modExtensions>
</ThingDef>
```

---

### Example 3: Guided VLS Rocket Projectile

```xml
<ThingDef ParentName="BaseBullet">
  <defName>Bullet_VLS_GuidedMissile</defName>
  <thingClass>CombatExtended.ProjectileCE_Explosive</thingClass>

  <projectile Class="AbsolutelyMoreCannons.ProjectilePropertiesCE">
    <damageAmountBase>300</damageAmountBase>
    <explosionRadius>4.5</explosionRadius>
    <damageDef>Bomb</damageDef>
    <speed>40</speed>

    <!-- Guided Rocket Controls -->
    <trajectoryWorkerClass>AbsolutelyMoreCannons.VLSTrajectoryWorker</trajectoryWorkerClass>
    <vlsLaunchAngle>85</vlsLaunchAngle>
    <guidanceDelay>1.5</guidanceDelay>
    <guidanceOnDescending>true</guidanceOnDescending>
    <homingAcceleration>0.08</homingAcceleration>
    <retargetRadius>12.0</retargetRadius>
  </projectile>
</ThingDef>
```

---

### Example 4: FCS Module Item Definition

```xml
<ThingDef ParentName="ResourceBase">
  <defName>AMC_FCS_Spacer</defName>
  <label>Spacer Fire Control System</label>
  <description>An ultra-advanced AI fire control computer for unmanned turrets.</description>
  <graphicData>
    <texPath>Things/Item/FCS_Spacer</texPath>
    <graphicClass>Graphic_Single</graphicClass>
  </graphicData>

  <modExtensions>
    <li Class="AbsolutelyMoreCannons.FCSPropertiesDefExtension">
      <swayMultiplier>0.15</swayMultiplier>
      <recoilMultiplier>0.15</recoilMultiplier>
      <spreadMultiplier>0.15</spreadMultiplier>
      <aimTimeMultiplier>0.15</aimTimeMultiplier>
      <rangeMultiplier>1.10</rangeMultiplier>
      <trackingAbility>true</trackingAbility>
    </li>
  </modExtensions>
</ThingDef>
```
