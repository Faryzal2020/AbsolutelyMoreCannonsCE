# Turret XML Reference & Authoring Guide
## Absolutely More Cannons (AMC) - CE Version 1.6

This guide provides a comprehensive technical reference for creating, configuring, and maintaining turret XML definitions in **Absolutely More Cannons - CE Version**. It details the separation between direct and indirect fire modes, building and weapon components, mod extensions, single-type constraints, and parameters requiring visual evaluation by a human.

---

## Table of Contents
1. [Parameters/Fields Identical Between Direct and Indirect Modes](#1-parametersfields-identical-between-direct-and-indirect-modes)
2. [Optional Building Comps and ModExtensions](#2-optional-building-comps-and-modextensions)
3. [Weapon Comps for Turrets](#3-weapon-comps-for-turrets)
4. [Mandatory Parameters for Indirect Fire Turrets](#4-mandatory-parameters-for-indirect-fire-turrets)
5. [Mandatory Parameters for Direct Fire Turrets](#5-mandatory-parameters-for-direct-fire-turrets)
6. [Rules for Single-Type Turrets (Direct-Only or Indirect-Only)](#6-rules-for-single-type-turrets-direct-only-or-indirect-only)
7. [Visual/Graphic-Dependent Parameters (Must Be Evaluated by Human)](#7-visualgraphic-dependent-parameters-must-be-evaluated-by-human)

---

## 1. Parameters/Fields Identical Between Direct and Indirect Modes

When a turret possesses both **Direct** and **Indirect** modes (swapped via `CompTurretModeSwap` and grouped under `designatorDropdown`), both `ThingDef`s represent the **exact same physical weapon system**. Consequently, the following fields **must remain identical** between both variants:

### A. Building `ThingDef` Parameters
| XML Tag | Location | Rationale & Consistency Rule |
| :--- | :--- | :--- |
| `<graphicData>` / `<texPath>` | Building | The base pedestal/turret carriage texture is identical for both modes. |
| `<graphicData>` / `<drawSize>` | Building | Base structure draw dimensions must match exactly (e.g. `(15, 15)` or `(22, 22)`). |
| `<graphicData>` / `<shadowData>` | Building | Footprint shadow dimensions and offsets remain the same. |
| `<uiIconPath>` | Building | Architect menu icon should represent the same gun system. |
| `<size>` | Building | Physical tile footprint on the map grid (e.g. `(6,6)` or `(5,5)`). |
| `<fillPercent>` | Building | Cover percentage and physical height obstruction are identical. |
| `<passability>` & `<pathCost>` | Building | Movement obstruction of the chassis remains the same. |
| `<terrainAffordanceNeeded>` | Building | Weight class requirement (typically `Heavy`). |
| `<statBases>` / `<MaxHitPoints>` | Building | The structure's structural durability does not change when changing elevation. |
| `<statBases>` / `<WorkToBuild>` | Building | Labor required to construct the mount must match. |
| `<statBases>` / `<Mass>` & `<Bulk>` | Building | Weight and packaging volume are identical. |
| `<costList>` | Building | Both modes must cost the exact same materials (Steel, Plasteel, Components). |
| `<constructionSkillPrerequisite>` | Building | Same construction skill requirement. |
| `<researchPrerequisites>` | Building | Both unlocked by the same research project. |
| `<designatorDropdown>` | Building | Must point to the same `DesignatorDropdownGroupDef` so player sees a single grouped dropdown. |
| `<interactionCellOffset>` | Building | Operator pawn stands at the exact same relative spot (e.g. `(0, 0, -3)`). |
| `<turretTopDrawSize>` | `<building>` | Turret top visual scale multiplier matches across both modes. |
| `<turretBurstCooldownTime>` | `<building>` | Cooldown between bursts is dictated by the weapon's action/breech mechanism. |
| `<basePowerConsumption>` | `CompPowerTrader` | Power draw remains identical for automated or electric traversing drives. |

### B. Weapon `ThingDef` Parameters (`turretGunDef`)
| XML Tag | Location | Rationale & Consistency Rule |
| :--- | :--- | :--- |
| `<statBases>` / `<ShotSpread>` | Gun Def | Inherent mechanical rifling accuracy of the barrel is identical. |
| `<statBases>` / `<RangedWeapon_Cooldown>` | Gun Def | Weapon cooldown matching the building cooldown time. |
| `<soundInteract>` | Gun Def | Mechanical shell chambering / reload sound (e.g. `AMC_Large_Reload`). |
| `<verbs>` / `<soundCast>` | Gun Def | Gunshot acoustic signature depends on the caliber/propellant, not elevation. |
| `<verbs>` / `<soundCastTail>` | Gun Def | Reverberation sound tail (e.g. `GunTail_Heavy`). |
| `<verbs>` / `<muzzleFlashScale>` | Gun Def | Light flash output of the propellant detonation. |
| `<verbs>` / `<ticksBetweenBurstShots>` | Gun Def | Mechanical cyclic fire rate (RPM) within bursts. |
| `<verbs>` / `<burstShotCount>` | Gun Def | Burst capacity per firing command. |
| `<verbs>` / `<recoilPattern>` | Gun Def | Set to `Mounted` for all static turrets. |
| `<magazineSize>` | `CompProperties_AmmoUser` | Internal drum/magazine/ready-rack capacity does not change. |
| `<reloadTime>` | `CompProperties_AmmoUser` | Time required to reload fresh magazines/clips. |

---

## 2. Optional Building Comps and ModExtensions

These optional components and extensions can be added to the turret building `ThingDef` to enable advanced behaviors, visual effects, and survivability features.

### A. Optional Building Comps (`<comps>`)

#### 1. `CompProperties_TurretBarrel`
* **Class**: `AbsolutelyMoreCannons.CompProperties_TurretBarrel`
* **Target**: Turret Building Def
* **Purpose**: Runtime driver for animated barrel recoil, multi-barrel firing synchronization, muzzle flash lights, rotary spinning, and burst/RPM gizmos.
* **Usage**:
  ```xml
  <li Class="AbsolutelyMoreCannons.CompProperties_TurretBarrel" />
  ```

#### 2. `CompProperties_TurretModeSwap`
* **Class**: `AbsolutelyMoreCannons.CompProperties_TurretModeSwap`
* **Target**: Turret Building Def
* **Purpose**: Provides a player gizmo button allowing instantaneous dynamic swapping between Direct and Indirect modes without deconstructing.
* **Parameters**:
  * `<alternateDef>`: `defName` of the counterpart turret `ThingDef` to swap into.
  * `<gizmoLabel>`: Button text (e.g. `Switch to Indirect Fire`).
  * `<gizmoDesc>`: Tooltip description.
  * `<gizmoIcon>`: UI button icon path (`UI/Buttons/ArtyMode` or `UI/Buttons/DirectMode`).
* **Usage**:
  ```xml
  <li Class="AbsolutelyMoreCannons.CompProperties_TurretModeSwap">
    <alternateDef>Turret_120mmTAK120_indirect_Base</alternateDef>
    <gizmoLabel>Switch to Indirect Fire</gizmoLabel>
    <gizmoDesc>Change this turret into indirect fire mode (artillery arc)\nRequires 25+ cell range, fires over obstacles</gizmoDesc>
    <gizmoIcon>UI/Buttons/ArtyMode</gizmoIcon>
  </li>
  ```

#### 3. `CompProperties_TurretPreserveAmmo`
* **Class**: `AbsolutelyMoreCannons.CompProperties_TurretPreserveAmmo`
* **Target**: Turret Building Def
* **Purpose**: Automatically halts firing mid-burst if the current target dies or is downed, preventing wasted expensive heavy shells.
* **Parameters**:
  * `<defaultPreserveAmmo>`: (`bool`, default: `true`) Initial state upon building.
* **Usage**:
  ```xml
  <li Class="AbsolutelyMoreCannons.CompProperties_TurretPreserveAmmo" />
  ```

#### 4. `CompProperties_AccuracyOverride`
* **Class**: `AbsolutelyMoreCannons.CompProperties_AccuracyOverride`
* **Target**: Turret Building Def
* **Purpose**: Provides per-turret stat overrides for sway, recoil, and spread reduction factors.
* **Parameters**:
  * `<swayReduction>`: (`float`, 0.0 to 1.0) Sway factor reduction (e.g. `0.8` = 80% reduction).
  * `<recoilReduction>`: (`float`, 0.0 to 1.0) Recoil jump reduction.
  * `<spreadReduction>`: (`float`, 0.0 to 1.0) Shot dispersion cone reduction.
* **Usage**:
  ```xml
  <li Class="AbsolutelyMoreCannons.CompProperties_AccuracyOverride">
    <swayReduction>0.2</swayReduction>
    <recoilReduction>0.3</recoilReduction>
    <spreadReduction>0.25</spreadReduction>
  </li>
  ```

#### 5. `CompProperties_TurretSmoker`
* **Class**: `AbsolutelyMoreCannons.CompProperties_TurretSmoker`
* **Target**: Turret Building Def
* **Purpose**: Multi-stage smoke and particle simulation handling directional muzzle smoke, barrel heat dissipation smoke, and ground blast shockwave rings.
* **Sub-Modules**:
  * **Muzzle Smoke**: `muzzleEnabled`, `muzzleFleckDef`, `muzzleParticleCount`, `muzzleSpawnDuration`, `muzzleOffset`, `muzzleVelocity`, `muzzleVelDuration`, `directionCone`, `muzzleParticleSize`, `muzzleSpawnDelay`.
  * **Heat Smoke**: `heatEnabled`, `heatFleckDef`, `heatThreshold`, `heatDecayRate`, `decayIncrease`, `heatOffset`, `heatEmissionRate`, `emissionIncrease`, `heatParticleSize`, `heatEmissionPoints`, `heatEmissionSpacing`.
  * **Shockwave Smoke**: `shockwaveEnabled`, `shockwaveFleckDef`, `shockwaveRadius`, `shockwaveDensity`, `shockwaveOffset`, `shockwaveParticleSize`, `shockwaveFadeOutSpeed`, `shockwaveGradientDensity`, `shockwaveGradientParticleSize`.
* **Usage**:
  ```xml
  <li Class="AbsolutelyMoreCannons.CompProperties_TurretSmoker">
    <muzzleEnabled>true</muzzleEnabled>
    <muzzleFleckDef>AMC_MuzzleSmoke</muzzleFleckDef>
    <muzzleParticleCount>3</muzzleParticleCount>
    <muzzleSpawnDuration>5</muzzleSpawnDuration>
    <muzzleOffset>(0, 0, 7)</muzzleOffset>
    <muzzleVelocity>15</muzzleVelocity>
    <muzzleVelDuration>20~30</muzzleVelDuration>
    <directionCone>4</directionCone>
    <muzzleParticleSize>3~4</muzzleParticleSize>
    <muzzleSpawnDelay>1</muzzleSpawnDelay>

    <heatEnabled>true</heatEnabled>
    <heatFleckDef>AMC_HeatSmoke</heatFleckDef>
    <heatThreshold>15</heatThreshold>
    <heatDecayRate>0.34</heatDecayRate>
    <heatOffset>(0, 0, 3)</heatOffset>
    <heatEmissionRate>3.5</heatEmissionRate>
    <heatParticleSize>0.6</heatParticleSize>
    <heatEmissionPoints>6</heatEmissionPoints>
    <heatEmissionSpacing>0.5</heatEmissionSpacing>

    <shockwaveEnabled>true</shockwaveEnabled>
    <shockwaveFleckDef>AMC_ShockwaveSmoke</shockwaveFleckDef>
    <shockwaveRadius>5</shockwaveRadius>
    <shockwaveDensity>70</shockwaveDensity>
    <shockwaveOffset>(0, 0, 7)</shockwaveOffset>
    <shockwaveParticleSize>2</shockwaveParticleSize>
    <shockwaveFadeOutSpeed>0.5</shockwaveFadeOutSpeed>
  </li>
  ```

#### 6. `CompProperties_EnclosedTurret`
* **Class**: `AbsolutelyMoreCannons.CompProperties_EnclosedTurret`
* **Target**: Manned Turret Building Defs (`AMCTurretMannedBase` / `AMCArtilleryBase`)
* **Purpose**: Simulates an armored pillbox, bunker, or enclosed turret basket protecting the operator.
* **Parameters**:
  * `<bulletProtection>`: (0.0 to 1.0) Sharp/bullet damage mitigation (1.0 = total immunity).
  * `<explosiveProtection>`: (0.0 to 1.0) Shrapnel/bomb protection.
  * `<flameProtection>`: (0.0 to 1.0) Prevents pawn catching fire.
  * `<temperatureProtection>`: (0.0 to 1.0) Regulates internal cabin temperature to 21°C.
  * `<meleeProtection>`: (0.0 to 1.0) Blunt/cut protection from melee breaches.
  * `<hidePawnGraphics>`: (`bool`) If `true`, pawn is visually hidden inside the turret.
  * `<centerPawnPosition>`: (`bool`) Centers operator selection/name box over turret center.
* **Usage**:
  ```xml
  <li Class="AbsolutelyMoreCannons.CompProperties_EnclosedTurret">
    <bulletProtection>1.0</bulletProtection>
    <explosiveProtection>0.8</explosiveProtection>
    <temperatureProtection>0.5</temperatureProtection>
    <hidePawnGraphics>true</hidePawnGraphics>
  </li>
  ```

#### 7. `CompProperties_TurretViewTransfer`
* **Class**: `AbsolutelyMoreCannons.CompProperties_TurretViewTransfer`
* **Target**: Turret Building Def (Large / High-fill turrets)
* **Purpose**: Essential for tall turrets (`fillPercent >= 1.0` or large sizes like 5x5/6x6). Shifts the operator's line-of-sight origin to the turret center and ignores the turret's own physical structure so the gun does not block its own operator's view.
* **Parameters**:
  * `<transferViewOrigin>`: (`bool`, default: `true`) Moves sight origin to turret center.
  * `<ignoreSelfOcclusion>`: (`bool`, default: `true`) Bypasses turret structure obstruction.
* **Usage**:
  ```xml
  <li Class="AbsolutelyMoreCannons.CompProperties_TurretViewTransfer">
    <transferViewOrigin>true</transferViewOrigin>
    <ignoreSelfOcclusion>true</ignoreSelfOcclusion>
  </li>
  ```

#### 8. `CompProperties_TurretFCS`
* **Class**: `AbsolutelyMoreCannons.CompProperties_TurretFCS`
* **Target**: Unmanned / Automated Turret Building Defs
* **Purpose**: Marker component that enables the internal container inventory for Fire Control System (FCS) hardware cards/modules, allowing pawns to install upgrades that dramatically boost tracking, aim speed, and recoil handling.
* **Usage**:
  ```xml
  <li Class="AbsolutelyMoreCannons.CompProperties_TurretFCS" />
  ```

#### 9. `CompProperties_TurretSprayDiscipline`
* **Class**: `AbsolutelyMoreCannons.CompProperties_TurretSprayDiscipline`
* **Target**: Autocannon / Rotary Cannon Building Defs
* **Purpose**: Automates fire discipline for high-rate-of-fire guns. Caps burst length per target before retargeting, or cycles targets across an engagement cone.
* **Parameters**:
  * `<defaultEnableSprayDiscipline>`: (`bool`)
  * `<shotsPerTarget>`: (`int`) Maximum rounds fired at a single pawn before switching.
  * `<cycleConeDegrees>`: (`float`) Search cone width for next target.
  * `<allowToggle>`: (`bool`) Player toggleable gizmo.

#### 10. `CompProperties_TracerLine`
* **Class**: `AbsolutelyMoreCannons.CompProperties_TracerLine`
* **Target**: Turret Building Def
* **Purpose**: Draws a temporary laser/tracer line along the firing vector upon shot discharge.
* **Parameters**:
  * `<lineLength>`: Length in tiles.
  * `<lineWidth>`: Width in tiles.
  * `<lineOffset>`: Vector3 offset `(X, Y, Z)` from turret center.
  * `<durationTicks>`: Visible duration in ticks.
  * `<lineColor>`: RGBA color.

---

### B. Optional Building ModExtensions (`<modExtensions>`)

#### 1. `CombatExtended.NonSnapTurretExtension`
* **Class**: `CombatExtended.NonSnapTurretExtension`
* **Target**: Turret Building Def
* **Purpose**: Eliminates instant snapping rotation. Forces the turret to realistically traverse at a physical angular speed.
* **Parameters**:
  * `<speed>`: Rotation rate in degrees per tick (`0.3` = 18°/sec, `0.5` = 30°/sec, `1.0` = 60°/sec).
  * `<preferedAngleRange>`: Preferred forward engagement sector in degrees.
  * `<angleWeightMultiplier>`: AI target evaluation bias towards current facing angle.
  * `<minAngleWeight>`: Minimum target score divisor to avoid divide-by-zero.
* **Usage**:
  ```xml
  <li Class="CombatExtended.NonSnapTurretExtension">
    <speed>0.4</speed>
    <preferedAngleRange>30</preferedAngleRange>
    <angleWeightMultiplier>1.5</angleWeightMultiplier>
    <minAngleWeight>0.1</minAngleWeight>
  </li>
  ```

#### 2. `AbsolutelyMoreCannons.TurretBarrelExtension`
* **Class**: `AbsolutelyMoreCannons.TurretBarrelExtension`
* **Target**: Turret Building Def
* **Purpose**: Master graphic configuration for animated turret barrels. Defines textures, draw scaling, pivot offsets, recoil curves, muzzle flashes, rotary gatling spin animations, and gizmo selectors.
* **Key Parameters**:
  * `<barrelGraphic>`: Graphic data (`texPath`, `graphicClass`, `drawSize`) for the barrel sprite.
  * `<underBarrelGraphic>`: Graphic rendered under the barrel (crucial for elevated indirect mounts to hide trunnion gaps).
  * `<barrelOffset>`: Pivot offset `(X, Y, Z)` from turret center to trunnion.
  * `<barrelDrawSize>`: Draw scale multiplier.
  * `<barrelAmount>`: Parallel barrels drawn (1, 2, 4).
  * `<barrelSpacing>`: Lateral distance between multi-barrel instances.
  * `<sequentialFiring>`: Cycles recoil sequentially through barrels per shot.
  * `<recoilAnimation>`:
    * `<enabled>`: (`bool`)
    * `<maxDistance>`: Max kick displacement in tiles (e.g. `0.3` to `2.0`).
    * `<recoilDuration>`: Ticks moving backward.
    * `<returnDuration>`: Ticks returning forward.
    * `<useRecoilCurve>` / `<useReturnCurve>`: Cubic ease-out acceleration curves.
    * `<affectsRotation>`: Recoil kicks barrel angle slightly.
  * `<firingAnimation>`:
    * `<enabled>`: (`bool`)
    * `<durationTicks>`: Flash/pulse duration.
    * `<drawFlash>`: Spawns muzzle flash lighting.
    * `<flashColor>`, `<flashSize>`, `<flashBrightness>`, `<flashOffset>`.
    * `<muzzleFlashEffect>`: `EffecterDef` (e.g. `AMC_MuzzleFlashLight`).
    * `<flashIntensityCurve>`: Brightness decay curve over lifetime.
    * `<projectileSpawnOffset>`: Distance in tiles along aiming vector to projectile spawn point.
  * `<spinningAnimation>`: For rotary/gatling cannons (`maxRPM`, `spindownTime`, `frameCount`, `barrelCount`).
  * `<selectableBurstCounts>`: List of selectable burst options displayed on player gizmo.
  * `<maxRPMs>`: List of selectable RPM rates displayed on player gizmo.

#### 3. `AbsolutelyMoreCannons.TurretPreserveAmmoExtension`
* **Class**: `AbsolutelyMoreCannons.TurretPreserveAmmoExtension`
* **Target**: Turret Building Def
* **Parameters**: `<defaultPreserveAmmo>` (`bool`), `<allowToggle>` (`bool`).

#### 4. `AbsolutelyMoreCannons.TurretSuppressionImmunityExtension`
* **Class**: `AbsolutelyMoreCannons.TurretSuppressionImmunityExtension`
* **Target**: Manned Turret Building Def
* **Purpose**: Prevents gunner pawns from fleeing or cowering under CE suppression mechanics while manning the turret.
* **Parameters**: `<preventOperatorSuppression>true</preventOperatorSuppression>`.

#### 5. `AbsolutelyMoreCannons.TurretTrackingExtension`
* **Class**: `AbsolutelyMoreCannons.TurretTrackingExtension`
* **Target**: Turret Building Def
* **Purpose**: Enables continuous mid-burst rotation and target leading between individual shots of a burst.
* **Parameters**: `<enableMidBurstTracking>true</enableMidBurstTracking>`.

#### 6. `AbsolutelyMoreCannons.TurretClampingExtension`
* **Class**: `AbsolutelyMoreCannons.TurretClampingExtension`
* **Target**: Turret Building Def or Weapon Def
* **Purpose**: Constrains weapon horizontal traverse and vertical elevation angles.
* **Parameters**:
  * `<maxVerticalDeviation>` / `<maxElevationDeviation>`: Max elevation angle in degrees (-1 = unlimited).
  * `<maxRotationDeviation>` / `<maxHorizontalDeviation>`: Max horizontal traverse angle in degrees (-1 = 360° unlimited).

---

## 3. Weapon Comps for Turrets

Weapon `ThingDef`s (referenced by `<building><turretGunDef>`) use specific `ThingComp`s and mod extensions to handle ammunition, ballistic charges, and fire controls.

### A. Weapon Comps (`<comps>`)

#### 1. `CombatExtended.CompProperties_AmmoUser`
* **Class**: `CombatExtended.CompProperties_AmmoUser`
* **Applies To**: Both Direct and Indirect Weapons
* **Purpose**: Governs magazine capacity, reloading speed, and compatible ammunition sets.
* **Parameters**:
  * `<magazineSize>`: (`int`) Number of rounds stored internally.
  * `<reloadTime>`: (`float`) Reload duration in seconds.
  * `<ammoSet>`: `AmmoSetDef` defining loadable ammo types.
  * `<reloadOneAtATime>`: (`bool`, optional) Single-round chamber loading.
* **Usage**:
  ```xml
  <li Class="CombatExtended.CompProperties_AmmoUser">
    <magazineSize>52</magazineSize>
    <reloadTime>1</reloadTime>
    <ammoSet>AmmoSet_120mmTAK120_Shells</ammoSet>
  </li>
  ```

#### 2. `CombatExtended.CompProperties_FireModes`
* **Class**: `CombatExtended.CompProperties_FireModes`
* **Applies To**: Typically Direct Fire Weapons and Rapid-fire Autocannons
* **Purpose**: Configures AI burst firing behavior and player aiming modes (`AimedShot` vs `Snapshot`).
* **Parameters**:
  * `<aiUseBurstMode>`: (`bool`) Whether AI fires bursts.
  * `<aiAimMode>`: `AimedShot` or `Snapshot`.
  * `<aimedBurstShotCount>`: Burst shot count under aimed fire.
  * `<noSingleShot>`: (`bool`, optional) Disables single-fire gizmo.
  * `<noSnapshot>`: (`bool`, optional) Disables snapshot gizmo.
* **Usage**:
  ```xml
  <li Class="CombatExtended.CompProperties_FireModes">
    <aiUseBurstMode>TRUE</aiUseBurstMode>
    <aiAimMode>AimedShot</aiAimMode>
    <aimedBurstShotCount>1</aimedBurstShotCount>
  </li>
  ```

#### 3. `CombatExtended.CompProperties_Charges`
* **Class**: `CombatExtended.CompProperties_Charges`
* **Applies To**: **Indirect Fire (Artillery/Mortar) Weapons ONLY**
* **Purpose**: Provides propellant charge increments used by CE's ballistic calculator to solve high-arc parabolic trajectories across distance bands.
* **Parameters**:
  * `<chargeSpeeds>`: List of muzzle velocities (in m/s) corresponding to charge levels (Charge 1, Charge 2, etc.).
* **Usage**:
  ```xml
  <li Class="CombatExtended.CompProperties_Charges">
    <chargeSpeeds>
      <li>50</li>
      <li>75</li>
      <li>100</li>
      <li>125</li>
      <li>150</li>
    </chargeSpeeds>
  </li>
  ```

### B. Weapon ModExtensions (`<modExtensions>`)

#### 1. `CombatExtended.GunDrawExtension`
* **Class**: `CombatExtended.GunDrawExtension`
* **Purpose**: Adjusts shell casing ejection visual coordinates.
* **Parameters**:
  * `<CasingOffset>`: Ejection origin relative to gun center `(X, Y)`.
  * `<CasingAngleOffset>`: Angle offset for ejected casing.
  * `<DrawSize>`: Visual casing scale.

#### 2. `AbsolutelyMoreCannons.TurretClampingExtension`
* Can be placed on the weapon `ThingDef` to enforce physical gun cradle elevation limits.

---

## 4. Mandatory Parameters for Indirect Fire Turrets

Indirect fire weapons (howitzers, mortars, naval coastal bombardment guns) require a distinct configuration structure to function within Combat Extended without throwing runtime XML or calculation errors.

### A. Building `ThingDef` Requirements
1. **Parent Definition**:
   * Manned Turrets: Must inherit from `ParentName="AMCArtilleryBase"` (or `TurretBuilding_Base.xml` mortar base).
   * Unmanned Turrets: Must inherit from `ParentName="AMCArtilleryAutoBase"`.
2. **PlaceWorkers**:
   * **MANDATORY**: `<placeWorkers><li>PlaceWorker_NotUnderRoof</li></placeWorkers>`.
     * *Rationale*: Artillery fires shells high into the stratosphere. Building under roofs or overhead mountain will cause shells to immediately detonate on the ceiling.
   * **FORBIDDEN / MUST OMIT**: `PlaceWorker_ShowTurretRadius`.
     * *Rationale*: Indirect artillery has near-infinite/map-wide range (e.g. 18,000–25,000 cells). Adding this placeworker renders a solid white circle blanketing the entire screen, severely blinding the player during placement.
3. **Building Tags**:
   * **MANDATORY**: Must include `<buildingTags><li>Artillery</li></buildingTags>` (inherited from `AMCArtilleryBase`).
     * *Rationale*: Required for RimWorld AI mortar targeting routines and Settlement Bombardment (World Map shelling).
4. **Tutorial Concept**:
   * `<spawnedConceptLearnOpportunity>CE_MortarDirectFire</spawnedConceptLearnOpportunity>`.

### B. Weapon `ThingDef` Requirements
1. **Parent Definition**: Must inherit from `ParentName="BaseArtilleryWeapon"`.
2. **Weapon Tags**:
   * Must include `<weaponTags><li>TurretGun</li><li>Artillery_BaseDestroyer</li></weaponTags>`.
3. **Verb Class**:
   * **MANDATORY**: `<verbClass>CombatExtended.Verb_ShootMortarCE</verbClass>`.
4. **Verb Properties**:
   * `<requireLineOfSight>false</requireLineOfSight>`: **CRITICAL**. Allows shooting over walls, mountains, and fog of war.
   * `<stopBurstWithoutLos>false</stopBurstWithoutLos>`: Prevents burst cancellation when line of sight is obstructed.
   * `<minRange>`: **MANDATORY**. Must have a substantial minimum deadzone (typically `20` to `30` cells). Prevents steep high-angle shells from dropping onto the turret itself.
   * `<circularError>1</circularError>`: Dictates ballistic dispersion radius.
   * `<indirectFirePenalty>0.125</indirectFirePenalty>`: Accuracy penalty applied when firing into unspotted fog of war without spotter pawn assistance.
   * `<targetParams><canTargetLocations>true</canTargetLocations></targetParams>`: Permits targeting empty ground cells.
5. **Mandatory Comps**:
   * **`CombatExtended.CompProperties_Charges`**: **MANDATORY**. `Verb_ShootMortarCE` requires `chargeSpeeds` to compute trajectory angles. Without this comp, CE throws a `NullReferenceException` upon target acquisition.
   * **`CombatExtended.CompProperties_AmmoUser`**: Must point to an indirect ammo set (`AmmoSet_..._indirect_Shells`) containing shells compatible with charges.

---

## 5. Mandatory Parameters for Direct Fire Turrets

Direct fire turrets engage ground targets with flat, high-velocity trajectories requiring direct line of sight.

### A. Building `ThingDef` Requirements
1. **Parent Definition**:
   * Manned Turrets: `ParentName="AMCTurretMannedBase"` or `ParentName="AMCTurretBase"`.
   * Unmanned Turrets: `ParentName="AMCTurretAutoBase"`.
2. **PlaceWorkers**:
   * **MANDATORY**: Must include `PlaceWorker_TurretTop`.
   * **MANDATORY**: Must include `PlaceWorker_ShowTurretRadius` (enables the green/white maximum engagement radius circle).
   * **FORBIDDEN / MUST OMIT**: Must **NOT** include `PlaceWorker_NotUnderRoof` (direct fire turrets can be built in pillboxes, embrasures, and interior defensive bunkers).
3. **Building Tags**:
   * Must **NOT** include `<buildingTags><li>Artillery</li></buildingTags>` (unless intentionally designed as a dual-bombardment platform, as purely direct fire weapons will error if called for world map shelling).

### B. Weapon `ThingDef` Requirements
1. **Parent Definition**: Must inherit from `ParentName="BaseTurretGun"`.
2. **Verb Class**:
   * **MANDATORY**: `<verbClass>CombatExtended.Verb_ShootCE</verbClass>`.
3. **Verb Properties**:
   * `<requireLineOfSight>`: `true` (or omitted, defaults to `true`). Direct fire requires visual line of sight.
   * `<minRange>`: Small minimum barrel clearance range (typically `0` to `8` cells, depending on the turret's barrel length, because it cannot shoot at targets too close to its barrel).
   * `<ignorePartialLoSBlocker>true</ignorePartialLoSBlocker>`: Allows shooting over friendly barricades, embrasures, and low cover.
4. **Mandatory Comps**:
   * **`CombatExtended.CompProperties_AmmoUser`**: Points to a direct fire ammo set (`AmmoSet_..._Shells`).
   * **FORBIDDEN**: Must **NOT** contain `CombatExtended.CompProperties_Charges`! Direct fire verbs do not use propellant charge steps; adding charges causes CE UI errors.

---

## 6. Rules for Single-Type Turrets (Direct-Only or Indirect-Only)

When a turret does not possess mode-swapping (e.g. anti-tank guns like `Pak 40` / `BS-3` that are direct-only, or siege mortars like `Karl-Gerät` / `2A3 Kondor` that are indirect-only), follow these strict rules:

### A. Universal Rules for Single-Type Turrets
1. **NO Mode Swap Component**:
   * **STRICTLY FORBIDDEN**: Must **NOT** include `<li Class="AbsolutelyMoreCannons.CompProperties_TurretModeSwap">`. Including this component without an alternate definition will cause game crashes and null reference exceptions when clicked.
2. **NO Dropdown Grouping**:
   * Must **NOT** declare or use `<designatorDropdown>` unless sharing a menu with other caliber variants. Single-type turrets should display directly as independent icons in the architect category.

### B. Direct-Only Turrets (e.g., Flak 41, Pak 40, BS-3, Leo 1A5)
* Must use `AMCTurretMannedBase` / `AMCTurretAutoBase`.
* Must have `PlaceWorker_ShowTurretRadius`.
* Must **NOT** have `PlaceWorker_NotUnderRoof`.
* Must **NOT** have `<li>Artillery</li>` building tag.
* Must use `CombatExtended.Verb_ShootCE`.
* Must **NOT** have `CombatExtended.CompProperties_Charges`.

### C. Indirect-Only Turrets (e.g., Karl-Gerät 600mm, 406mm 2A3)
* Must use `AMCArtilleryBase` / `AMCArtilleryAutoBase`.
* Must have `PlaceWorker_NotUnderRoof`.
* Must **NOT** have `PlaceWorker_ShowTurretRadius`.
* Must have `<li>Artillery</li>` building tag.
* Must use `CombatExtended.Verb_ShootMortarCE`.
* Must have `CombatExtended.CompProperties_Charges` with `<chargeSpeeds>`.
* Must have `<requireLineOfSight>false</requireLineOfSight>`.

---

## 7. Visual/Graphic-Dependent Parameters (Must Be Evaluated by Human)

Because RimWorld uses a 2D pseudo-top-down perspective (rendered with an oblique angle) and texture sprites often feature varying amounts of transparent padding, **the following parameters cannot be calculated programmatically by AI and must be visually verified in-game by a human modder**:

### 1. `turretTopDrawSize` vs Base `drawSize`
* **Why Human Evaluation Is Required**: Texture canvases are often exported with whitespace/padding around the artwork. Even if the canvas is 1024x1024, the actual drawn gun may only occupy 60% of the canvas.
* **Human Verification**: Ensure the turret top sits naturally within the chassis ring mount without spilling out or looking unnaturally miniaturized.

### 2. `barrelOffset` `(Vector3)` in `TurretBarrelExtension`
* **Why Human Evaluation Is Required**: In 2D artwork, the physical trunnion/pivot axle is rarely at the exact coordinate center `(0,0,0)` of the PNG sprite.
* **Human Verification**: Rotate the turret 360° in dev mode. If the barrel wobbles or disconnects from the turret body when traversing, adjust `(X, Y, Z)` until the barrel rotates perfectly around its visual mounting trunnion.

### 3. `projectileSpawnOffset` (Direct vs Foreshortened Indirect)
* **Why Human Evaluation Is Required**:
  * In **Direct Fire**, the barrel is drawn long and horizontal. The projectile must spawn at the tip (e.g. `projectileSpawnOffset = 5.5`).
  * In **Indirect Fire**, the barrel is drawn angled upward toward the viewer, causing severe **perspective foreshortening** (the 2D sprite is physically shorter on the screen).
  * *Consequence*: If an AI uses the same spawn offset for both modes, the indirect shell will spawn 3 tiles floating in empty air ahead of the gun! A human must visually align spawn coordinates for both modes (e.g., `5.5` in direct, reduced to `2.5` in indirect).

### 4. `flashOffset` in `firingAnimation`
* **Why Human Evaluation Is Required**: Muzzle flash lights and flecks must originate precisely at the muzzle brake.
* **Human Verification**: Test-fire the gun and observe the flash center. Direct guns usually have `flashOffset = 0` to `-1`, while foreshortened indirect guns often need negative offsets (e.g. `-4`) to move the flash back onto the compressed barrel tip.

### 5. `muzzleOffset` in `CompProperties_TurretSmoker`
* **Why Human Evaluation Is Required**: Muzzle smoke puffs must exit directly from the muzzle brake apertures.
* **Human Verification**: In direct fire, the muzzle tip may be at `(0, 0, 7)`. In indirect mode, foreshortening compresses the tip closer to `(0, 0, 3.5)`. Verify that smoke clouds bloom outward from the barrel end, not the middle of the tube.

### 6. `heatOffset`, `heatEmissionPoints`, & `heatEmissionSpacing` in `CompProperties_TurretSmoker`
* **Why Human Evaluation Is Required**: Smoke should only rise off the **exposed hot metal barrel**, not from inside the armored turret cab or through thermal shrouds.
* **Human Verification**: Count how many tiles of exposed barrel are visible. Set `heatOffset` at the turret mantlet exit, adjust `heatEmissionPoints` (e.g. 3 to 6 points), and set `heatEmissionSpacing` (e.g. 0.3 to 0.5 cells) so wisps rise evenly along the metal length.

### 7. `shockwaveOffset` & `shockwaveRadius` in `CompProperties_TurretSmoker`
* **Why Human Evaluation Is Required**: Blast overpressure shockwaves must emanate from the muzzle blast zone and wrap around the gun without covering the crew cabin.
* **Human Verification**: Set `shockwaveOffset` equal to the muzzle position and scale `shockwaveRadius` proportionally to shell caliber (e.g., radius 3 for 76mm, radius 5 for 152mm).

### 8. `recoilAnimation/maxDistance` vs Mechanical Clearance
* **Why Human Evaluation Is Required**: Recoil kickback moves the barrel backward into the turret.
* **Human Verification**: If `maxDistance` is set too high (e.g. 1.0 or 2.0 tiles), the barrel sprite will clip through the rear armor plate of the turret or visually crush the operator standing behind the breech. A human must visually balance dramatic kickback against sprite clipping.

### 9. Requirement for `underBarrelGraphic` on Indirect Mounts
* **Why Human Evaluation Is Required**: When an artillery barrel elevates to a 60° angle in 2D, the space underneath the gun carriage becomes visible.
* **Human Verification**: If elevating the barrel leaves an ugly transparent hole exposing the empty ground or chassis under the trunnion, a human artist must provide an `<underBarrelGraphic>` (e.g. `Things/Building/M120mmTAK120_UnderBarrel_Indirect`) to act as the elevation cradle.

### 10. `interactionCellOffset` `(IntVec3)`
* **Why Human Evaluation Is Required**: Where the pawn stands relative to visual steps, catwalks, ladders, or breech controls.
* **Human Verification**: Check pawn placement in-game. Ensure pawns do not stand inside solid metal geometry, within the rotating gun ring, or directly under the recoil path of the breech.

### 11. `hidePawnGraphics` in `CompProperties_EnclosedTurret`
* **Why Human Evaluation Is Required**:
  * Open gun mounts (e.g. `Flak 41`, `Pak 40`, `LEIG 18`) have exposed seats where crew members should remain visible (`hidePawnGraphics = false`).
  * Armored turret houses (e.g. `Msta`, `TAK120`, `Leo 1A5`) enclose the crew inside armor (`hidePawnGraphics = true`).
  * AI cannot inspect sprite artwork to determine whether the turret is open-topped or sealed; a human must set this flag.

### 12. `CompProperties_TurretViewTransfer` Requirement
* **Why Human Evaluation Is Required**: Turrets with `fillPercent >= 1.0` or large structures (5x5, 6x6) block the operator pawn's sight line under vanilla cover rules.
* **Human Verification**: Test whether a manned turret can acquire targets approaching from behind or the sides of its interaction cell. If the operator's view is blocked by the turret's own bounding box, add `CompProperties_TurretViewTransfer`.

---

## 8. Summary Comparison Matrix

| Property | Direct Fire Mode | Indirect Fire Mode | Single-Type (Direct) | Single-Type (Indirect) |
| :--- | :--- | :--- | :--- | :--- |
| **Building Parent** | `AMCTurretMannedBase` / `AMCTurretAutoBase` | `AMCArtilleryBase` / `AMCArtilleryAutoBase` | `AMCTurretMannedBase` / `AMCTurretAutoBase` | `AMCArtilleryBase` / `AMCArtilleryAutoBase` |
| **Gun Parent** | `BaseTurretGun` | `BaseArtilleryWeapon` | `BaseTurretGun` | `BaseArtilleryWeapon` |
| **Verb Class** | `Verb_ShootCE` | `Verb_ShootMortarCE` | `Verb_ShootCE` | `Verb_ShootMortarCE` |
| **Line of Sight** | `requireLineOfSight = true` | `requireLineOfSight = false` | `requireLineOfSight = true` | `requireLineOfSight = false` |
| **Charge Speeds** | **Forbidden** | **Mandatory** (`chargeSpeeds`) | **Forbidden** | **Mandatory** (`chargeSpeeds`) |
| **Minimum Range** | Low (0–8 cells) | High (20–30 cells) | Low (0–8 cells) | High (20–30 cells) |
| **Building Tags** | Standard tags | `<li>Artillery</li>` | Standard tags | `<li>Artillery</li>` |
| **PlaceWorkers** | `ShowTurretRadius`, `TurretTop` | `NotUnderRoof`, `TurretTop` | `ShowTurretRadius`, `TurretTop` | `NotUnderRoof`, `TurretTop` |
| **Mode Swap Comp**| Points to Indirect Def | Points to Direct Def | **Forbidden** | **Forbidden** |
| **Dropdown Group**| Grouped in `designatorDropdown`| Grouped in `designatorDropdown`| **Omit** (Standalone) | **Omit** (Standalone) |
| **Spawn Offset** | Full barrel length | Foreshortened / Reduced | Full barrel length | Foreshortened / Reduced |
