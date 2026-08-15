# Unmanned Turret Fire Control System (FCS) - Technical Documentation

## Overview

The **Fire Control System (FCS)** is a modular component system designed for unmanned turrets in *Absolutely More Cannons - CE Version*. Unmanned turrets do not have human gunners; instead, they rely on an installed physical FCS module to acquire targets, calculate ballistic fire solutions, and drive physical servomotors.

Without a loaded FCS module, an unmanned turret is classified as **inoperable** and cannot target or fire at enemies.

---

## Core Components & Data Architecture

```
                                  ┌───────────────────────────┐
                                  │  FCS Physical ItemDef     │
                                  │  (e.g., AMC_FCS_Spacer)   │
                                  └─────────────┬─────────────┘
                                                │ ModExtension
                                  ┌─────────────▼─────────────┐
                                  │ FCSPropertiesDefExtension │
                                  │  - swayMultiplier         │
                                  │  - recoilMultiplier       │
                                  │  - spreadMultiplier       │
                                  │  - aimTimeMultiplier      │
                                  │  - rangeMultiplier        │
                                  └─────────────┬─────────────┘
                                                │ Loaded into
┌───────────────────────────┐     ┌─────────────▼─────────────┐
│    Unmanned TurretDef    │─────►│      CompTurretFCS        │
│  (e.g., U20mmSPzMarder)   │     │  (innerContainer: Thing)  │
└───────────────────────────┘     └─────────────┬─────────────┘
                                                │ Intercepted by
                                  ┌─────────────▼─────────────┐
                                  │ Harmony Accuracy Patches  │
                                  └───────────────────────────┘
```

### 1. `CompProperties_TurretFCS` & `CompTurretFCS`
* **File**: `Source/Absolutely More Cannons - CE Version/CompTurretFCS.cs`
* **Function**: A `ThingComp` attached to unmanned turret `ThingDef`s.
* **Storage**: Encapsulates an internal `ThingOwner` container (`innerContainer`) holding exactly one physical FCS item.
* **Properties**:
  * `HasFCS`: `true` if an FCS item is currently loaded in the container.
  * `LoadedFCSItem`: References the active `Thing` inside `innerContainer`.
  * `ActiveStats`: Reads the `FCSPropertiesDefExtension` attached to `LoadedFCSItem.def`.
  * `targetFCSDef`: Set when the player selects a module to load; used by hauling job drivers.

### 2. `FCSPropertiesDefExtension`
* **File**: `Source/Absolutely More Cannons - CE Version/FCSPropertiesDefExtension.cs`
* **Function**: `DefModExtension` added to FCS `ThingDef`s in XML (`Defs/ThingDefs_Items/Items_FCS.xml`).
* **Stat Multipliers**:
  * `swayMultiplier` (float, default `1.0f`): Scales continuous weapon sway/wobble.
  * `recoilMultiplier` (float, default `1.0f`): Scales per-shot burst kickback.
  * `spreadMultiplier` (float, default `1.0f`): Scales mechanical projectile dispersion cone (`0.15` = 85% spread reduction).
  * `aimTimeMultiplier` (float, default `1.0f`): Scales turret lock-on warmup duration (`0.15` = 85% faster aim).
  * `rangeMultiplier` (float, default `1.0f`): Scales maximum engagement distance (`1.10` = +10% max range).

---

## FCS Installation & Hauling Workflow

1. **Player Assignment (Gizmo Menu)**:
   * Selecting an unmanned turret reveals the **FCS Command Gizmo** (`CompGetGizmosExtra`).
   * Clicking the gizmo opens `OpenFCSSelectionMenu()`, listing available map items (`AMC_FCS_Basic`, `AMC_FCS_Advanced`, `AMC_FCS_Spacer`).
   * Selecting an FCS assigns `comp.targetFCSDef`. Ejects any existing loaded FCS onto the ground if swapped.

2. **Colonist Hauling Job (`WorkGiver_LoadTurretFCS`)**:
   * Scans artificial buildings for player-owned turrets with a non-null `targetFCSDef` and missing FCS.
   * Locates the closest matching unforbidden FCS item on the map.
   * Creates an `AMC_Job_LoadTurretFCS` job.

3. **Job Execution (`JobDriver_LoadTurretFCS`)**:
   * Colonist walks to the FCS item on the ground/stockpile.
   * Picks up the item (`pawn.carryTracker.TryStartCarry`).
   * Walks to the target turret.
   * Performs a 100-tick installation toil with a progress bar.
   * Deposits the item into `CompTurretFCS.GetDirectlyHeldThings()`.

---

## Behavior Alterations & Combat Mechanics (Harmony Patches)

FCS stat alterations hook dynamically into RimWorld and Combat Extended (CE) core loops inside `HarmonyPatches_AccuracyControl.cs`.

### 1. Aim Time Reduction (`Postfix_TurretWarmupScale`)
* **Targets**: `Building_TurretGun.Tick` and `CombatExtended.Building_TurretGunCE.Tick`.
* **Mechanism**:
  * CE turrets inherit directly from `Building_Turret` (not `Building_TurretGun`).
  * When a turret acquires a target, `burstWarmupTicksLeft` is assigned a positive value.
  * The patch intercepts this frame and multiplies `burstWarmupTicksLeft` by `aimTimeMultiplier`:
    ```csharp
    int scaledWarmup = Mathf.Max(1, Mathf.RoundToInt(currentWarmup * aimMult));
    warmupField.SetValue(turret, scaledWarmup);
    ```

### 2. Spread & Sway Reduction (`Postfix_ShiftVecReportFor`)
* **Target**: `CombatExtended.Verb_LaunchProjectileCE.ShiftVecReportFor(LocalTargetInfo)`.
* **Mechanism**:
  * Called whenever CE calculates accuracy shift vectors before launching a bullet.
  * Fetches `CompTurretFCS` from the verb's `caster` (using recursive `ParentHolder` lookup).
  * Modifies the calculated `ShiftVecReport`:
    * `spreadDegrees *= spreadMultiplier`
    * `swayDegrees *= swayMultiplier`

### 3. Extended Range Engagement (`Postfix_CanHitTargetFrom`)
* **Target**: `Verse.Verb.CanHitTargetFrom(IntVec3 root, LocalTargetInfo targ)`.
* **Mechanism**:
  * If `rangeMultiplier > 1.0f`, calculates `extendedRange = baseRange * rangeMultiplier`.
  * If target distance is beyond base range but within `extendedRange` and has clear line of sight, `__result` is overridden to `true`, authorizing the turret to acquire and fire at beyond-normal range targets.

### 4. Direct Sway & Recoil Vector Reduction (`Postfix_GetSwayVec`, `Postfix_GetRecoilVec`)
* **Target**: `CombatExtended.Verb_LaunchProjectileCE.GetSwayVec` and `GetRecoilVec`.
* **Mechanism**:
  * Multiplies output sway and recoil vector angles by `1.0 - swayMultiplier` and `1.0 - recoilMultiplier`.

---

## Inspection Panel & Telemetry

### Inspect Panel Display (`CompInspectStringExtra`)
* When **Missing FCS**:
  ```
  <color=red>INOPERABLE: Missing Fire Control System (FCS)</color>
  ```
* When **FCS Loaded**:
  ```
  FCS Loaded: Spacer Fire Control System
    Sway: 85% reduction
    Spread: 85% reduction
    Recoil: 85% reduction
    Aim Time: 85% faster
    Range: +10% bonus
  ```

### Diagnostic Logging (`AMCLogger.LogFCS`)
* Toggleable via **Mod Options -> Absolutely More Cannons -> Log FCS Performance & Accuracy Telemetry** (`AMCSettings.logFCS`, default: `false`).
* Formatted Log Events:
  * `[AMC FCS Log] TURRET AIMING & TARGET ACQUIRED`: Logs base vs scaled warmup ticks.
  * `[AMC FCS Log] ACCURACY REPORT`: Logs base spread angle vs FCS-modified spread angle.
  * `[AMC FCS Log] EXTENDED RANGE TARGETING`: Logs target acquisition beyond normal max range.
  * `[AMC FCS Log] SHOT FIRED`: Logs projectile launch event and target entity.

---

## File Reference Map

| Component / Utility | File Path |
| :--- | :--- |
| **FCS Component & Gizmo UI** | `Source/Absolutely More Cannons - CE Version/CompTurretFCS.cs` |
| **FCS DefModExtension** | `Source/Absolutely More Cannons - CE Version/FCSPropertiesDefExtension.cs` |
| **Harmony Accuracy Patches** | `Source/Absolutely More Cannons - CE Version/HarmonyPatches_AccuracyControl.cs` |
| **Hauling WorkGiver** | `Source/Absolutely More Cannons - CE Version/WorkGiver_LoadTurretFCS.cs` |
| **Hauling JobDriver** | `Source/Absolutely More Cannons - CE Version/JobDriver_LoadTurretFCS.cs` |
| **Mod Settings & Toggles** | `Source/Absolutely More Cannons - CE Version/AMCSettings.cs` |
| **FCS Item Defs (XML)** | `Common/Defs/ThingDefs_Items/Items_FCS.xml` |
