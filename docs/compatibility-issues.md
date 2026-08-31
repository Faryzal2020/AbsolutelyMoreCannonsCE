# Combat Extended (CE) Mod Compatibility Analysis & Testing Verification Guide

**Target Mod:** Absolutely More Cannons - CE Version 1.6 (AMC)  
**Date:** August 22, 2026 (Updated Post Refactoring)  

---

## Executive Summary

This document analyzes potential compatibility conflicts between **Absolutely More Cannons - CE Version 1.6 (AMC)** and popular Combat Extended (CE) compatible RimWorld mods. 

Following our recent architectural overhaul, AMC Harmony patches have transitioned from global execution to an **Opt-In, XML-Driven System**. Invasive mechanics—such as Indirect Charge Boosting, Rotation & Elevation Clamping, and Accuracy (Sway/Recoil) Reductions—are now strictly scoped to turrets and casters explicitly configured with AMC components (`CompTurretBarrel`, `CompTurretFCS`, `Comp_AccuracyOverride`) or XML extensions (`TurretBarrelExtension`, `TurretChargeBoostExtension`).

---

## AMC Core C# Subsystems Map

| Subsystem | Primary C# Files | Core Mechanics & Intercepts | Architecture Status |
| :--- | :--- | :--- | :--- |
| **Enclosed Manned Turret System** | `CompEnclosedTurret.cs`<br>`HarmonyPatches_EnclosedTurret.cs` | - Suppresses operator pawn rendering (`Pawn.DrawAt`, `PawnRenderer.RenderPawnAt`, `PawnRenderTree.Draw`).<br>- Shifts `DrawPos` to turret center.<br>- Custom damage mitigation (`PreApplyDamage`).<br>- Flame, temperature (21°C override), & CE suppression immunity. | **Opt-In per Turret.** Requires `CompEnclosedTurret` on target building. |
| **Fire Control System (FCS) & Accuracy Control** | `CompTurretFCS.cs`<br>`HarmonyPatches_AccuracyControl.cs`<br>`HarmonyPatches.cs` | - FCS module item requirement for turret operability (`Active`, `CanSetTarget`, `IsOperational`).<br>- Dynamically scales sway, recoil, spread, aim warmup time, & range (`ShiftVecReportFor`, `CanHitTargetFrom`).<br>- Ground target acquisition fallback for CIWS. | **Opt-In per Caster.** `GetSwayReductionForVerb` & `GetRecoilReductionForVerb` return 0% unless `Comp_AccuracyOverride` or `CompTurretFCS` is present. |
| **Rotation & Elevation Clamping** | `HarmonyPatches_RotationClamping.cs` | - Clamps `shotRotation` to max arc deviation relative to turret base.<br>- Enforces minimum upward `shotAngle` (elevation clamping). | **Opt-In per Turret.** Gated behind `IsAMCConfiguredTurret(caster)` (requires `CompTurretBarrel` or `TurretBarrelExtension`). |
| **Indirect Fire Charge Boost** | `HarmonyPatches_ChargeBoost.cs` | - Intercepts `CompCharges.GetChargeBracket` to shift charge index (+1 speed bracket), forcing steep ~73° plunging arcs. | **Opt-In per Weapon/Turret.** Requires `TurretChargeBoostExtension` or `CompProperties_TurretBarrel.chargeBoostOffset > 0`. |
| **Mid-Burst Dynamic Tracking** | `HarmonyPatches_TargetTracking.cs` | - Overrides `Verb_LaunchProjectileCE.LockRotationAndAngle` getter to return `false`.<br>- Ticks `TurretTop` rotation during burst firing (`Building_TurretGunCE.Tick`). | **Opt-In per Turret.** Requires `TurretTrackingExtension` or `CompTurretFCS.trackingAbility`. |
| **Preserve Ammo System** | `HarmonyPatches_PreserveAmmo.cs` | - Intercepts `Verb_LaunchProjectileCE.TryCastShot`.<br>- Aborts bursts against downed pawns if no standing hostiles remain in line of fire. | **Opt-In per Turret.** Requires `CompTurretPreserveAmmo` or `TurretPreserveAmmoExtension`. |
| **Muzzle Smoke & Visual Effects** | `CompTurretSmoker.cs`<br>`MapComponent_TurretSmokeManager.cs` | - Ticks custom smoke motes (`MoteSmokeMuzzle`) and particle physics across map ticks. | **Opt-In per Building.** Requires `CompTurretSmoker`. |
| **Impact Damage Radius** | `HarmonyPatches_DamageRadius.cs` | - Executes `GenExplosionCE.DoExplosion` without sound/visuals when `explosionRadius <= 0` and `damageRadius > 0`. | **Opt-In per Projectile.** Scoped to AMC `ProjectilePropertiesCE`. |

---

## Popular CE-Compatible Mods & Compatibility Status

---

### 1. Vehicle Framework & Vehicle Mods
*Popular Mods:* **Vehicle Framework (VF)**, **Vanilla Vehicles Expanded (VFE Vehicles)**, **Combat Extended Vehicles Integration**

#### Functional Overview
Adds vehicle entities (tanks, technicals, attack helicopters, gunboats) with mounted turrets, vehicle seats, crew passengers, and multi-pawn operation.

#### Technical Analysis & Verification
1. **Verb Bypass via CE Delegates:** Vehicle Framework does not use standard RimWorld verbs or CE's `Verb_LaunchProjectileCE`. Instead, `VehicleTurret.FireTurretCE` invokes dynamic CE reflection delegates (`LaunchProjectileCE`, `ProjectileAngleCE`) directly.
2. **Entity Isolation:** Vehicle turrets are `VehicleTurret` instances attached to `VehiclePawn` entities, not map `Building_Turret` buildings. With AMC's `IsAMCConfiguredTurret` check, all AMC Harmony patches return early when encountering vehicle turrets, ensuring zero C# crashes or alignment bugs.
3. **Custom Ammo Compatibility:** AMC's custom ammunition (including airburst HE shells and silent damage radius projectiles) works 100% when loaded into Vehicle Framework turrets. When VF fires an AMC projectile, `LaunchProjectileCE` spawns the AMC projectile, and all AMC impact/fragment physics trigger properly.

**Status:** 🟢 **RESOLVED / INERT (Zero Conflict)**

---

### 2. Save Our Ship 2 (SoS2)
*Popular Mods:* **Save Our Ship 2 (SoS2)**

#### Functional Overview
Adds orbital space combat, vacuum environments, heat mechanics, ship-to-ship turrets, space maps, and specialized target acquisition across ship tactical maps.

#### Technical Analysis & Verification
1. **Starship Mountability & Airtight Hull Patch (`Patches/SaveOurShip2.xml`):**
   - Added XML compatibility patch injecting `SaveOurShip2.PlaceWorker_OnShipHull` and `<isAirtight>true</isAirtight>` into AMC turret building bases (`AMCTurretBase`, `AMCArtilleryBase`).
   - Allows players to construct and mount AMC naval turrets, autocannons, and heavy guns directly on starship hulls and hardpoints in space maps while maintaining ship vacuum/pressurization integrity.
2. **Enclosed Turret Operator Vacuum Protection:**
   - AMC's `CompEnclosedTurret` and `Prefix_Pawn_AmbientTemperature` force a fixed 21°C environment for operators inside enclosed AMC turrets. Pawns manning enclosed AMC turrets on starships in space are fully protected from space vacuum and extreme temperatures (-100°C to +100°C+).
3. **FCS Module Loading Fix (`JobDriver_LoadTurretFCS.cs`):**
   - Resolved `InvalidCastException` during ordered pawn FCS loading on space maps by refactoring target getters from `pawn.jobs.curJob` to `TargetThingA as Building` and `TargetThingB` (RimWorld `JobDriver` pre-toil safety standard).
4. **CE Tactical Calculation & Suppression Fix (`HarmonyPatches_CETacticalManagerFix.cs`):**
   - Resolved Combat Extended `CompTacticalManager` and `CompSuppressable` `NullReferenceException` spam when pawns die or turn into corpses on space tactical maps.
5. **Tactical UI Stability:**
   - Harmony patches on `GenDraw.DrawRadiusRing` and `PlaceWorker_ShowTurretRadius` ensure long-range AMC turrets do not trigger UI frame buffer exceptions on SoS2 space tactical maps.

**Status:** 🟢 **RESOLVED / FULLY COMPATIBLE**

---

### 3. High-Tech Energy & Advanced Turrets
*Popular Mods:* **Rimatomics**, **Vanilla Furniture Expanded - Security / Ancients**

#### Functional Overview
Adds high-tech energy weapons, PPC power grids, TACS radar target acquisition, automated point-defense systems, and custom turret building classes (`Building_TurretPower`, `Building_PlasmaTurret`).

#### Compatibility Notes
* Rimatomics turrets (Marauder, Punisher, Obelisk) inherit from custom building classes rather than `Building_TurretGunCE`.
* Because AMC FCS operability and accuracy patches require explicit AMC components (`CompTurretFCS` / `Comp_AccuracyOverride`), non-AMC high-tech turrets bypass AMC logic cleanly.

**Status:** 🟢 **SAFE / ISOLATED**

---

### 4. Pawn Graphic & Rendering Mods
*Popular Mods:* **Humanoid Alien Races (HAR)**, **Biotech Genes/Morphs**, **PawnMorpher**, **Yayo's Animation / Apparel Overlays**, **NL Facial Animation**

#### Functional Overview
Custom pawn render trees, dynamic body morphing, dynamic facial animations, body attachments, custom race rendering routines, and custom drawing passes.

#### Potential Compatibility Issues
1. **Pawn Graphic Hiding Leakage:** AMC patches standard RimWorld render methods (`Pawn.DrawAt`, `PawnRenderer.RenderPawnAt`, `PawnRenderTree.Draw`). Mods like *NL Facial Animation* or *HAR* extra attachment nodes (tails, wings, headwear overlays) sometimes register independent `RenderNode` hooks outside standard `PawnRenderTree.Draw`.
2. **Visual Impact:** If a modded alien pawn mans an enclosed AMC turret, custom attachment graphics might remain visible over the turret unless explicitly hidden by the render node tree.

**Status:** 🟡 **MEDIUM (Visual Only)**

---

### 5. Mortar & Artillery Overhaul Mods
*Popular Mods:* **Mortar Accuracy**, **Vanilla Factions Expanded - Artillery / Empire**, **CE Artillery Extensions**

#### Functional Overview
Overhauls mortar mechanics, weather/wind deviation calculations, custom propellants, and multi-charge shell mechanics.

#### Compatibility Notes
* **Rotation & Elevation Clamping:** Now strictly scoped via `IsAMCConfiguredTurret`. Non-AMC mortars and artillery will not have their rotation or elevation clamped by AMC.
* **Charge Boosting:** Now strictly requires `TurretChargeBoostExtension` or `CompProperties_TurretBarrel.chargeBoostOffset > 0`. Non-AMC mortars will not receive unwanted charge offsets.

**Status:** 🟢 **RESOLVED / ISOLATED**

---

### 6. Engine & Performance Optimization Mods
*Popular Mods:* **RocketMan**, **Performance Optimizer**, **RimThreaded**

#### Functional Overview
Caches property getters (`IsOperational`, `CanSetTarget`, `DrawPos`), throttles building/pawn tick calls, multithreads map pawn ticks, and optimizes stat lookup tables.

#### Potential Compatibility Issues
1. **Getter Caching (RocketMan / Performance Optimizer):** Performance Optimizer frequently caches property getters like `Building_Turret.IsOperational`. AMC's `PatchTurretFCSOperability` uses Harmony postfixes on these getters. If Performance Optimizer caches `IsOperational = true` before an FCS module is removed, the turret might stay operational.
2. **Multithreading Race Conditions (RimThreaded):** AMC uses static `Dictionary` objects (`turretTickCounter`, `lastWarmupTicks`, `trackedProjectiles`). RimThreaded executing turret ticks concurrently across thread pools could trigger `ConcurrentOperationsException`.

**Status:** 🔴 **HIGH (For RimThreaded environments)**

---

