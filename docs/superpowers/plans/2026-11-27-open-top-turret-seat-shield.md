# Open-Top Turret Seat & Gun Shield Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Let manned open-top turrets seat their gunner on the rotating turret top, behind a
gun shield that physically intercepts CE projectiles. Show both in the TurretEditor.

**Architecture:**
- **Pure math:** a Unity-free geometry file (`OpenTopGeometry.cs`) holds the math and is
  unit-tested by a tiny console project.
- **Runtime:**
  - Harmony patches move the gunner to the seat at the lowest draw level (aim, hitbox,
    render).
  - They also override his CE vertical range and make explosions judge him by his seat
    cell.
  - CE `BlockerRegistry` callbacks make projectiles find him across the footprint and run
    the shield.
- **TurretEditor:** gets four field-map entries and an inspector overlay built from a
  testable pure module.

**Tech Stack:** C# net472 (Harmony 2, RimWorld 1.7 via Krafs ref, Combat Extended workshop
dll), .NET 10 SDK for the test console, Node ≥22.5 + React/Vite for TurretEditor,
RimTestHarness for in-game scenarios.

**Spec:** `docs/superpowers/specs/2026-11-27-open-top-turret-seat-shield-design.md`. Read it
first. Section numbers (§) below refer to it.

## Global Constraints

- **No commits.** The working tree has unrelated uncommitted user edits in the same files
  (`TurretEditor/src/xml/*.js`, XML defs). "Checkpoint" steps mean: run the checks and
  report. The user commits.
- **Build the mod (macOS):**
  ```bash
  cd "Source/Absolutely More Cannons - CE Version" && dotnet build "Absolutely More Cannons - CE Version.csproj" -c Release -p:RimWorldPath="$HOME/Library/Application Support/Steam/steamapps/common/RimWorld"
  ```
  - Output is `Assemblies/AbsolutelyMoreCannons.dll`.
  - Expected: `0 Error(s)`.
- CE source for reading: `~/Documents/CombatExtended/Source/CombatExtended/CombatExtended/`.
- **Decompile game types** with
  `~/.dotnet/tools/ilspycmd -t <Type> "<RimWorld>/RimWorld.app/Contents/MacOS/Assembly-CSharp.dll"`.
- **Behaviour must stay identical for turrets that don't set `seatOffset`.** All new
  behaviour is gated on `Props.HasSeat` (shield behaviour also needs `Props.HasShield`).
- **Seat-offset frame:** +Z is along the barrel and +X is to the barrel's right. The angle
  is the compass turret-top angle, clockwise (0 = North, 90 = East).
  - `x' = x·cosθ + z·sinθ`
  - `z' = −x·sinθ + z·cosθ`
  - This is identical to `rotateVecXZ` in `TextureInspector.jsx`.
- **Gunner fallback height:** `1.45` when `barrelHeight` ≤ 0.
- **Field names, verbatim:** `seatOffset`, `shieldArc`, `shieldHeight`, `shieldArmor`.
- **No `Vector3?` XML fields.** RimWorld field parsing doesn't support nullables. Use a NaN
  sentinel.
- Log new runtime messages with the prefix `[AMC OpenTop]`. Per-shot decision logs are only
  written when `Prefs.DevMode` is on.

## Review Focus

1. **The gunner leaves or dies mid-burst.** Projectiles already in flight must hit
   normally. No null refs if the turret despawns or is destroyed by the redirected impact
   itself.
   - Test: Task 5 scenario `despawn` case.
2. **The turret is minified, reinstalled, or rotated, and the game is saved and loaded.**
   The seat registry must rebuild. No stale `CompEnclosedTurret` in a map's list after a
   load.
   - Test: Task 6 scenario `reload` case.
3. **Friendly fire from the gunner's own turret, or its own bursts** (launcher is the
   turret or the gunner). This must never hit the shield or the gunner.
   - Test: Task 5 `ownfire` assertion.
4. **The DCA 40's interaction cell is outside the footprint** (0,0,-2), so the gunner's
   logical cell is not a turret cell. Hits, blasts and suppression must all follow the seat.
   - Test: Task 6 scenario `he` on the DCA 40.
5. **A fully enclosed turret such as `Turret_30mmBMP2_Base`** must keep today's behaviour:
   hidden pawn, flat protection, full suppression immunity.
   - Test: Task 6 scenario `enclosed` case.

---

### Task 1: Geometry core + console tests

**Files:**
- Create: `Source/Absolutely More Cannons - CE Version/OpenTopGeometry.cs`
- Create: `Source/OpenTopGeometry.Tests/OpenTopGeometry.Tests.csproj` (net10.0 console,
  `<Compile Include="../Absolutely More Cannons - CE Version/OpenTopGeometry.cs" />`, no
  package refs)
- Create: `Source/OpenTopGeometry.Tests/Program.cs`

**Interfaces:**
- Produces, in `namespace AbsolutelyMoreCannons`: `public static class OpenTopGeometry`
  (only `System` / `System.Math`; no Unity or Verse):
  - `const float DefaultGunnerTop = 1.45f;`
  - `static void RotateOffset(float x, float z, float angleDeg, out float rx, out float rz)`
  - `static float CompassAngle(float dx, float dz)`. Returns degrees in [0,360): `atan2(dx, dz)`.
  - `static float DeltaAngle(float a, float b)`. Returns the absolute smallest difference
    in [0,180].
  - `static bool InShieldArc(float shotDirX, float shotDirZ, float topAngleDeg, float shieldArc)`
    - `shieldArc` ≤ 0 → false; ≥ 360 → true.
    - Otherwise compare `CompassAngle(-shotDirX, -shotDirZ)` with `topAngleDeg`:
      `DeltaAngle(...) ≤ shieldArc/2`.
  - `static void GunnerVerticalRange(float barrelHeight, float pawnHeight, out float bottom, out float top)`
    - `top = barrelHeight > 0 ? barrelHeight : DefaultGunnerTop`
    - `bottom = max(0, top − pawnHeight)`
  - `static float RemainingPenetration(float penetration, float shieldArmor)`. Returns
    `penetration − shieldArmor`; > 0 means it penetrated.

- [ ] **Step 1: Write the failing tests in `Program.cs`.** Use a tiny `Check(bool, string)`
  helper; exit code 1 on any failure; print `ALL PASS` on success.
  ```csharp
  RotateOffset(0,1,90,out var x,out var z);  Check(Near(x,1)&&Near(z,0), "fwd at East -> +X");
  RotateOffset(1,0,90,out x,out z);          Check(Near(x,0)&&Near(z,-1),"right at East -> -Z (south)");
  RotateOffset(0.5f,-0.8f,0,out x,out z);    Check(Near(x,0.5f)&&Near(z,-0.8f),"0 deg identity");
  Check(Near(CompassAngle(1,0),90) && Near(CompassAngle(0,-1),180), "compass");
  Check(Near(DeltaAngle(350,10),20), "wraparound");
  // shot travelling south (0,-1) comes FROM the north
  Check(InShieldArc(0,-1, 0,120),  "front shot inside front arc");
  Check(!InShieldArc(0, 1, 0,120), "rear shot outside front arc");
  Check(InShieldArc(-1,0, 30,120), "shot from east, top at 30, arc 120 (delta 60) inside");
  Check(!InShieldArc(-1,0, 29,118),"delta 61 > 59 outside");
  Check(InShieldArc(0,1, 0,360),   "360 covers rear");
  Check(!InShieldArc(0,-1,0,0),    "arc 0 = no shield");
  GunnerVerticalRange(1.1f,1.75f,out var b,out var t); Check(Near(t,1.1f)&&Near(b,0),"clamped at ground");
  GunnerVerticalRange(-1f,1.0f,out b,out t);           Check(Near(t,1.45f)&&Near(b,0.45f),"fallback 1.45");
  Check(RemainingPenetration(12,8)>0 && RemainingPenetration(8,8)<=0, "penetration");
  ```
- [ ] **Step 2: Run the tests and confirm they fail.**
  Run: `dotnet run --project Source/OpenTopGeometry.Tests`
  Expected: compile error (`OpenTopGeometry` missing).
- [ ] **Step 3: Implement `OpenTopGeometry`** with the signatures above.
- [ ] **Step 4: Run the tests and confirm they pass.**
  Run: `dotnet run --project Source/OpenTopGeometry.Tests`
  Expected: `ALL PASS`, exit 0.
- [ ] **Step 5: Confirm the mod build excludes the test folder.**
  Run the mod build command. Expected: `0 Error(s)`.
- [ ] **Step 6: Checkpoint.**

### Task 2: Props fields, validation, seat registry, runtime helpers

**Files:**
- Modify: `Source/.../CompProperties_EnclosedTurret.cs`
- Modify: `Source/.../CompEnclosedTurret.cs`
- Create: `Source/.../OpenTopSeatUtility.cs`

**Interfaces:**
- Consumes: Task 1 `OpenTopGeometry`.
- Produces:
  - `CompProperties_EnclosedTurret` gains:
    - `public Vector3 seatOffset = new Vector3(float.NaN, 0f, float.NaN);`
    - `public float shieldArc = 0f, shieldHeight = 0f, shieldArmor = 0f;`
    - `public bool HasSeat => !float.IsNaN(seatOffset.x);`
    - `public bool HasShield => HasSeat && shieldArc > 0f;`
    - `public override IEnumerable<string> ConfigErrors(ThingDef parentDef)` yields:
      - `"[AMC OpenTop] seatOffset is set but hidePawnGraphics is true; the gunner stays hidden."`
        when `HasSeat && hidePawnGraphics`;
      - `"[AMC OpenTop] shield fields need seatOffset."` when `shieldArc > 0 && !HasSeat`.
  - `CompEnclosedTurret`:
    - `PostSpawnSetup` / `PostDeSpawn` register/unregister in `OpenTopSeatUtility` when
      `Props.HasSeat`.
    - `CompTick`, every tick while seated, sets `gunner.Rotation = Rot4.FromAngleFlat(OpenTopSeatUtility.TopAngle(Turret))`
      (§2 "Facing"). If the ManTurret driver overwrites it, re-apply in a postfix on
      `Pawn_RotationTracker.RotationTrackerTick` instead.
  - `public static class OpenTopSeatUtility`:
    - `static IReadOnlyList<CompEnclosedTurret> SeatedTurrets(Map map)`: registry, a
      `Dictionary<int mapId, List<CompEnclosedTurret>>`, cleared on game load via
      `PostSpawnSetup(respawningAfterLoad)` re-registration.
    - `static bool TryGetSeated(Pawn pawn, out Building turret, out CompEnclosedTurret comp)`:
      wraps `IsManningEnclosedTurret` and requires `comp.Props.HasSeat`.
    - `static Pawn SeatedGunner(CompEnclosedTurret comp)`: the manning pawn if it passes
      `TryGetSeated`, else null.
    - `static float TopAngle(Building turret)`: `turret.TryGetComp<CompTurretBarrel>()?.GetCurrentBarrelRotation()`,
      else `TurretTrackingUtility.GetAuthoritativeBarrelRotation(turret)` if not NaN, else
      `((Building_Turret)turret).Top`'s current rotation via the existing reflection used
      in `CompTurretBarrel`.
    - `static Vector3 SeatWorldPos(Building turret, CompEnclosedTurret comp)`:
      `turret.DrawPos` plus the rotated `seatOffset` (x,z), keeping `turret.DrawPos.y`.
    - `static IntVec3 SeatCell(Building turret, CompEnclosedTurret comp)`:
      `SeatWorldPos(...).ToIntVec3()`.
    - `static FloatRange GunnerVerticalRange(Pawn gunner, Building turret)`:
      `OpenTopGeometry.GunnerVerticalRange(CompTurretBarrel.GetCustomBarrelHeight(turret), CE_Utility.GetCollisionBodyFactors(gunner).y, …)`.

- [ ] **Step 1: Implement the fields, `HasSeat`/`HasShield` and `ConfigErrors`.**
- [ ] **Step 2: Implement `OpenTopSeatUtility` and the registry hooks in
  `CompEnclosedTurret`.**
- [ ] **Step 3: Build.** Expected: `0 Error(s)`.
- [ ] **Step 4: Checkpoint.**

### Task 3: In-game scenario script (starts failing)

**Files:**
- Create: `DevTools/open_top_scenario.mjs` (Node ≥22.5, no deps; talks to RimTestHarness
  over `POST http://127.0.0.1:<port>/<cmd>`)
- Modify: `Common/Defs/ThingDefs_Buildings/Autocannons/20mmFlak38.xml`
  - Add the comp: `hidePawnGraphics=false`, `seatOffset (0,0,-0.9)`, `shieldArc 120`,
    `shieldHeight 0.85`, `shieldArmor 6`, `flameProtection 0`, `temperatureProtection 0`,
    `explosiveProtection 0.3`.
- Modify: `Common/Defs/ThingDefs_Buildings/Autocannons/40mmDCA40.xml`
  - Set: `seatOffset (0.7,0,-0.7)`, `shieldArc 360`, `shieldHeight 0.95`, `shieldArmor 8`.
  - Remove `bulletProtection`.
  - Set `flameProtection 0`.

**Interfaces:**
- Produces: CLI
  `node DevTools/open_top_scenario.mjs --port=<p> --turret=<defName> --case=<seat|front|rear|ap|he|ownfire|despawn|reload|enclosed>`.
  Prints one JSON line `{case, pass, details}`; exit 0 on pass, 1 on fail.

**Shared setup, used by every case:**
1. `set_debug unlimitedPower true` and `dev_mode on=true`.
2. `find_cell clearRadius=10`.
3. `spawn_thing` the turret, rotated north, `faction=player`.
4. `turret_fcs`, then `turret_ammo` with the first AP ammo of its set.
5. `spawn_pawn Colonist faction=player` beside it, then
   `order_job jobDef=ManTurret targetId=<turret>`.
6. Tick until `query_things` shows `curJob=ManTurret`.
7. Shooters are `spawn_pawn kindDef=Mercenary_Gunner faction=player`, drafted, then
   `order_job jobDef=AttackStatic targetId=<gunner>`. Positions are 12 cells along the
   chosen compass direction.
8. Force the turret top to face north with
   `turret_order_attack x=<turret.x> z=<turret.z+20>`, then tick 120.

| case | setup | pass condition |
|---|---|---|
| `seat` | rear shooter, `telemetry_start`, tick 600 | ≥1 `impact` with `other_id`=gunner whose (x,z) lies within 0.75 of `turret pos + rotate(seatOffset, topRotation)` |
| `front` | shooter north | gunner health unchanged after 600 ticks; ≥1 impact with `other_id`=turret; `log contains="[AMC OpenTop] shield stop"` ≥1 |
| `rear` | shooter south | ≥1 impact with `other_id`=gunner; for 360° turrets, 0 instead |
| `ap` | second manned Flak 38 north, `turret_order_attack targetId=<gunner>` | log `[AMC OpenTop] shield pen` ≥1 and gunner took damage |
| `he` | same as `ap` with the HE ammo of the set | log `[AMC OpenTop] shield stop` or `blast seat` ≥1; gunner damage comes from a blast whose cell = seat cell (`[AMC OpenTop] blast seat` line) |
| `ownfire` | gunner's own turret firing north at a target cell, 600 ticks | 0 impacts on the gunner or the turret from the turret's own projectiles |
| `despawn` | rear shooter firing; at tick 300, order the gunner to `Goto` away | no new `error`-level log lines; projectiles in flight resolve |
| `reload` | `save_game`, then `load_save`, re-man, then the `front` assertions | as `front` |
| `enclosed` | `Turret_30mmBMP2_Base`, rear shooter | gunner is never hit (current fully enclosed behaviour); no `[AMC OpenTop]` lines |

- [ ] **Step 1: Write the script** implementing the cases above.
- [ ] **Step 2: Launch the game headless.**
  Run:
  ```bash
  cd ../RimTestHarness/runner && node runner.js launch --mods=ludeon.rimworld,brrainz.harmony,ceteam.combatextended,faryzal2020.amcce_copy,rimtestharness.harness
  ```
  Read `harness.port` from the session folder it prints.
- [ ] **Step 3: Run the `seat` and `front` cases and confirm they fail.**
  Run: `node DevTools/open_top_scenario.mjs --port=<p> --turret=Turret_20mmFlak38_Base --case=seat`,
  then the same with `--case=front`.
  Expected: `"pass":false` for both. The seat isn't implemented, and there are no shield
  log lines yet.
- [ ] **Step 4: Run the `enclosed` case and confirm it passes.**
  Run: `--case=enclosed` with `--turret=Turret_30mmBMP2_Base`.
  Expected: `"pass":true`. This is the baseline.
- [ ] **Step 5: Quit and checkpoint.** Run `node runner.js quit 0`.

### Task 4: Seat (draw position, aim, vertical range, collision reachability)

**Files:**
- Create: `Source/.../HarmonyPatches_OpenTopTurret.cs`
  - `public static void TryPatchOpenTop(Harmony harmony)`.
  - Use the same guarded `try { … } catch` style as `HarmonyPatches_EnclosedTurret`.
- Modify: `Source/.../HarmonyPatches.cs` to call `HarmonyPatches_OpenTopTurret.TryPatchOpenTop(harmony)`
  right after `HarmonyPatches_EnclosedTurret.TryPatchEnclosedTurrets(harmony)`.
- Modify: `Source/.../HarmonyPatches_EnclosedTurret.cs`, `Prefix_Pawn_DrawPos`. Return
  `true` (defer) when `comp.Props.HasSeat`.

**Interfaces:**
- Consumes: Task 2 `OpenTopSeatUtility`.
- Produces:
  - **Prefix on the `Pawn_DrawTracker.DrawPos` getter.** For `TryGetSeated` pawns, set
    `__result = SeatWorldPos(...)` with `y = AltitudeLayer.BuildingOnTop.AltitudeFor() + 0.05f`.
    - That y draws above the turret top and below the barrel, which is drawn at
      `ItemImportant + 0.1`.
    - Read the pawn via `AccessTools.FieldRefAccess<Pawn_DrawTracker, Pawn>("pawn")`.
  - **Postfix on CE `CollisionVertical.CalculateHeightRange`** (private static; resolve
    with `AccessTools.Method(typeof(CollisionVertical), "CalculateHeightRange")`). For
    seated gunners:
    - `heightRange = GunnerVerticalRange(...)`;
    - `shotHeight = top − (top−bottom)·(1−CollisionVertical.BodyRegionMiddleHeight)`.
  - **`BlockerRegistry.RegisterCheckForCollisionCallback(OpenTopShield.CheckCell)`**,
    registered here; the method is implemented in Task 5. For this task, `CheckCell` only
    does reachability:
    - for each seated gunner in `SeatedTurrets(map)` whose turret `OccupiedRect()` contains
      `cell`, and the launcher is neither the turret nor the gunner:
      - intersect `CE_Utility.GetBoundsFor(gunner)` with `projectile.ShotLine`;
      - accept only if the hit is within this tick's segment
        (`dist² ≤ (ExactPosition − LastPos).sqrMagnitude`, or `bounds.Contains(LastPos/ExactPosition)`);
      - the hull goes first: if `GetBoundsFor(turret).IntersectRay` gives a smaller
        distance, return false.
    - On a hit, call `OpenTopShield.ResolveHit(projectile, gunner, turret, comp, dist, fromCellCheck: true)`.
      Until Task 5 that just sets `ExactPosition = ShotLine.GetPoint(dist)`,
      `landed = true`, then `projectile.Impact(gunner)`, and returns true.

- [ ] **Step 1: Implement the patches and the reachability-only callback.**
- [ ] **Step 2: Build.** Expected: `0 Error(s)`.
- [ ] **Step 3: Run the `seat` case and confirm it passes.**
  Relaunch the harness. Run the `seat` case on `Turret_20mmFlak38_Base` and
  `Turret_40mmDCA40_Base`.
  Expected: `"pass":true` for both.
- [ ] **Step 4: Run the `enclosed` case.** Expected: still `"pass":true`.
- [ ] **Step 5: Quit and checkpoint.**

### Task 5: Shield interception, penetration, spark/smoke

**Files:**
- Create: `Source/.../OpenTopShield.cs`
- Modify: `Source/.../HarmonyPatches_OpenTopTurret.cs`. Also register
  `BlockerRegistry.RegisterBeforeCollideWithCallback(OpenTopShield.BeforeCollideWith)`.

**Interfaces:**
- Consumes: Tasks 1, 2 and 4.
- Produces: `public static class OpenTopShield`:
  - `static bool CheckCell(ProjectileCE p, IntVec3 cell, Thing launcher)`, from Task 4.
  - `static bool BeforeCollideWith(ProjectileCE p, Thing thing)`:
    - if `thing` is a seated gunner of a `HasShield` turret and the launcher is neither the
      turret nor the gunner:
      - `GetBoundsFor(gunner).IntersectRay(p.ShotLine, out dist)`;
      - return `ResolveHit(p, gunner, turret, comp, dist, fromCellCheck: false)`;
    - else return false.
  - `static bool ResolveHit(ProjectileCE p, Pawn gunner, Building turret, CompEnclosedTurret comp, float dist, bool fromCellCheck)`,
    implementing §3:
    1. `point = p.ShotLine.GetPoint(dist)`.
    2. Covered = `comp.Props.HasShield && OpenTopGeometry.InShieldArc(dir.x, dir.z, TopAngle(turret), shieldArc) && point.y ≤ shieldHeight`,
       where `dir = p.ShotLine.direction`.
    3. **Not covered:** if `fromCellCheck`,
       `p.ExactPosition = point; p.landed = true; p.Impact(gunner); return true;`.
       Otherwise return false so CE completes its normal hit.
    4. **Covered:**
       - `var dinfo = p.DamageInfo; bool explosive = IsExplosive(p);`
       - `p.ExactPosition = point; p.landed = true; p.Impact(turret);`
       - If `!explosive`:
         - `rem = OpenTopGeometry.RemainingPenetration(dinfo.ArmorPenetrationInt, comp.Props.shieldArmor)`.
         - If `rem > 0`:
           - build a new `DamageInfo` (same def, amount, angle, instigator, weapon) with
             `armorPenetration = rem`;
           - `SetBodyRegion(new CollisionVertical(gunner).GetCollisionBodyHeight(point.y), BodyPartDepth.Outside)`;
           - `gunner.TakeDamage(...)`;
           - log `shield pen`.
         - Else: spawn spark plus smoke at `point`, and log `shield stop`.
       - If `explosive`: log `shield stop`.
       - Return true.
  - `static bool IsExplosive(ProjectileCE p)`:
    `(p.def.projectile as ProjectilePropertiesCE)?.explosionRadius > 0 || p.TryGetComp<CompExplosiveCE>() != null`.
  - **Spark/smoke defs:** use `FleckDefOf.MicroSparks` (×3) and `AMC_MuzzleSmoke` at
    scale 0.4 via `FleckMaker.Static` / `FleckMaker.ThrowSmoke`. Swap in a different
    vanilla spark fleck if that name doesn't resolve.
  - **Dev logs:** `[AMC OpenTop] shield stop <proj> pen=<x> armor=<y>` and
    `[AMC OpenTop] shield pen <proj> rem=<z>`, written only when `Prefs.DevMode` is on.

- [ ] **Step 1: Implement `OpenTopShield`** and switch Task 4's `CheckCell` to
  `ResolveHit`.
- [ ] **Step 2: Build.** Expected: `0 Error(s)`.
- [ ] **Step 3: Run the shield cases on `Turret_20mmFlak38_Base`.**
  Cases: `front`, `rear`, `ap`, `ownfire`, `despawn`. Expected: all `"pass":true`.
- [ ] **Step 4: Run the shield cases on `Turret_40mmDCA40_Base`.**
  Cases: `front`, `rear` (expect 0 gunner hits: 360°), `ownfire`. Expected: all
  `"pass":true`.
- [ ] **Step 5: Run the `enclosed` case.** Expected: still `"pass":true`.
- [ ] **Step 6: Quit and checkpoint.**

### Task 6: Protection rules, seat-cell blasts, directional suppression

**Files:**
- Create: `Source/.../OpenTopContext.cs`:
  - `internal static class OpenTopContext { [ThreadStatic] public static bool InBlast; }`
- Modify: `Source/.../HarmonyPatches_OpenTopTurret.cs` (blast patches)
- Modify: `Source/.../HarmonyPatches_EnclosedTurret.cs`:
  - `Prefix_Pawn_PreApplyDamage`
  - `Prefix_CompSuppressable_AddSuppression`
  - `Postfix_CompSuppressable_CanReactToSuppression`

**Interfaces:**
- Consumes: Tasks 2, 4 and 5.
- Produces:
  - **`Prefix_Pawn_PreApplyDamage` when `comp.Props.HasShield`.** Classify with
    `GetProtectionFor`:
    - Bullet or explosive category and `!OpenTopContext.InBlast`: no flat reduction, so
      `return true` untouched. This covers projectile hits and fragments.
    - Explosive category and `InBlast`: apply `explosiveProtection` as today.
    - All other categories: as today.
    - When not `HasShield`, the behaviour is byte-for-byte unchanged.
  - **Blast patches.** Patch both on `DamageWorker` and on every subclass in
    `typeof(DamageWorker).AllSubclassesNonAbstract()` that declares its own override; check
    with `AccessTools.DeclaredMethod`.
    - **Prefix on `ExplosionDamageThing(Explosion, Thing t, List<Thing>, List<Thing>, IntVec3 cell)`:**
      - If `t` is a seated gunner and `cell != SeatCell`, return false (skip).
      - Otherwise set `OpenTopContext.InBlast = true`.
    - **Finalizer:** `InBlast = false`.
    - **Postfix on `ExplosionAffectCell(Explosion explosion, IntVec3 c, List<Thing> damagedThings, List<Thing> ignoredThings, bool canThrowMotes)`:**
      - for each seated gunner in `SeatedTurrets(explosion.Map)` with `SeatCell == c`,
        `gunner.Position != c` and `!damagedThings.Contains(gunner)`:
        - invoke `ExplosionDamageThing(explosion, gunner, damagedThings, ignoredThings, c)`
          on `__instance` through a cached `AccessTools.Method` (virtual dispatch);
        - log `[AMC OpenTop] blast seat` (dev mode only).
  - **Suppression, when `HasShield`:**
    - `Postfix_CanReactToSuppression` no longer forces false.
    - `Prefix_AddSuppression(ThingComp __instance, IntVec3 origin)` returns false only when
      `OpenTopGeometry.InShieldArc(seat.x − origin.x, seat.z − origin.z, TopAngle, shieldArc)`
      is true. Use `origin.ToVector3Shifted()` and `SeatWorldPos`.
    - Without a shield, unchanged.

- [ ] **Step 1: Implement the context flags and the `PreApplyDamage` change.**
- [ ] **Step 2: Implement the blast patches.**
  - Decompile `Verse.DamageWorker` first and confirm the two signatures above (verified
    2026-11-27 on 1.7).
  - List CE and vanilla overrides with `grep -rn "override void ExplosionDamageThing\|override void ExplosionAffectCell"`
    over the CE source, plus ilspycmd on `Assembly-CSharp.dll`.
- [ ] **Step 3: Implement directional suppression.**
- [ ] **Step 4: Build.** Expected: `0 Error(s)`.
- [ ] **Step 5: Run the `he` case** on `Turret_40mmDCA40_Base` and
  `Turret_20mmFlak38_Base`. Expected: `"pass":true`.
- [ ] **Step 6: Run the `reload` case** on `Turret_20mmFlak38_Base`. Expected:
  `"pass":true`.
- [ ] **Step 7: Re-run every earlier case**, all with `"pass":true`:
  `seat`, `front`, `rear`, `ap`, `ownfire`, `despawn` (Flak 38) and `enclosed` (BMP-2).
- [ ] **Step 8: Run the geometry tests.**
  Run: `dotnet run --project Source/OpenTopGeometry.Tests`. Expected: `ALL PASS`.
- [ ] **Step 9: Quit and checkpoint.**

### Task 7: TurretEditor fields

**Files:**
- Modify: `TurretEditor/src/xml/extract.js`, the `comps.enclosed` defaults. Add
  `seatOffset: null, shieldArc: null, shieldHeight: null, shieldArmor: null`. `null`
  means absent, so the C# defaults apply; this matches the other enclosed fields.
- Modify: `TurretEditor/src/xml/fieldMap.js`. Add after line 98:
  - `f('comps.enclosed.seatOffset', 'building', [...ENCLOSED_COMP, { tag: 'seatOffset' }], 'string', 'Seat Offset (x,0,z)', { gate: 'comps.enclosed.enabled' })`
  - `f('comps.enclosed.shieldArc', …'shieldArc' }], 'float', 'Shield Arc (deg, 360 = all round)', …)`
  - `f('comps.enclosed.shieldHeight', …'shieldHeight' }], 'float', 'Shield Height (above ground)', …)`
  - `f('comps.enclosed.shieldArmor', …'shieldArmor' }], 'float', 'Shield Armor (mm RHA)', …)`
- Modify: `TurretEditor/test/editing.test.js`. Extend the
  `'enclosed turret comp values round trip and toggle correctly'` test.
- Check: `src/mcp/projections.js`, `src/services/exportService.js`, `src/routes/openapi.js`.
  Add the four keys anywhere enclosed fields are hand-listed.

- [ ] **Step 1: Write the failing assertions.**
  In the PUT, add `seatOffset: '(0,0,-0.9)', shieldArc: 120, shieldHeight: 0.85, shieldArmor: 6`.
  Then assert:
  ```js
  assert.ok(after.includes('<seatOffset>(0,0,-0.9)</seatOffset>'));
  assert.ok(after.includes('<shieldArc>120</shieldArc>'));
  assert.equal(reread.turret.comps.enclosed.seatOffset, '(0,0,-0.9)');
  assert.equal(reread.turret.comps.enclosed.shieldArc, 120);
  assert.equal(reread.turret.comps.enclosed.shieldHeight, 0.85);
  assert.equal(reread.turret.comps.enclosed.shieldArmor, 6);
  ```
- [ ] **Step 2: Run the tests and confirm they fail.**
  Run: `cd TurretEditor && node --test --test-name-pattern="enclosed turret comp" test/editing.test.js`
  Expected: FAIL on `<seatOffset>`.
- [ ] **Step 3: Add the extract defaults and field-map entries**, plus any hand-listed
  spots.
- [ ] **Step 4: Run the tests and confirm they pass.**
  Run: `cd TurretEditor && npm test`. Expected: all pass.
- [ ] **Step 5: Checkpoint.**

### Task 8: Texture Inspector seat & shield overlay

**Files:**
- Create: `TurretEditor/ui/src/openTopOverlay.js`. Pure ESM, no React.
- Modify: `TurretEditor/ui/src/components/TextureInspector.jsx`.
  - Import `rotateVecXZ` from the new module and delete the local copy.
  - Add `seat: true, shield: true` to `DEFAULT_LAYERS`.
  - Draw the overlay after the interaction-cell marker, which is at about line 1072.
  - Add two checkboxes next to "Interaction Cell". Disable them when
    `openTopOverlay(...)` returns null.
- Create: `TurretEditor/test/open-top-overlay.test.js`.

**Interfaces:**
- Produces, in `ui/src/openTopOverlay.js`:
  - `export function rotateVecXZ(x, z, angleDeg)`: moved verbatim.
  - `export function openTopOverlay(turret, rotationDeg)`. Returns null unless
    `turret.comps?.enclosed?.enabled` and `seatOffset` parses to a vec3. Otherwise returns:
    `{ seat: {x, z}, shield: null | { fromDeg, toDeg, full }, label }`.
    - `seat` = `rotateVecXZ(off.x, off.z, rotationDeg)`.
    - `shield` is null when `shieldArc` ≤ 0.
    - `full = shieldArc >= 360`.
    - `fromDeg = rotationDeg − arc/2` and `toDeg = rotationDeg + arc/2`, in compass
      degrees.
    - `label` = `` `shield ${shieldHeight} / head ${top} · ${shieldArmor}mm` ``, where
      `top = barrel.barrelHeight > 0 ? barrel.barrelHeight : 1.45`.
  - Parse `seatOffset` with the same `(x,y,z)` rules as `parseVec3` in the inspector. Move
    `parseVec3` too, if that keeps the inspector import simple.
- **Drawing:**
  - **Seat:** a 1-tile-diameter circle at `toScreen(seat.x, seat.z)` in `#f59e0b`, labelled
    "Seat".
  - **Shield:** a translucent `#f59e0b33` wedge, or a ring when `full`, centred on the seat
    with radius 1 tile, plus `label` as text.
  - Canvas angles: compass θ maps to canvas angle `θ − 90°`, because +Z is up on screen.
    This is the conversion the inspector's barrel drawing already uses.

- [ ] **Step 1: Write the failing tests.**
  ```js
  const t = { comps: { enclosed: { enabled: true, seatOffset: '(0,0,-0.9)', shieldArc: 120, shieldHeight: 0.85, shieldArmor: 6 } }, barrel: { barrelHeight: 1.1 } };
  const o = openTopOverlay(t, 90);
  assert.ok(Math.abs(o.seat.x - -0.9) < 1e-6 && Math.abs(o.seat.z) < 1e-6);   // behind the gun when facing East
  assert.deepEqual([o.shield.fromDeg, o.shield.toDeg, o.shield.full], [30, 150, false]);
  assert.equal(o.label, 'shield 0.85 / head 1.1 · 6mm');
  assert.equal(openTopOverlay({ comps: { enclosed: { enabled: true, seatOffset: '(0,0,0)', shieldArc: 360 } }, barrel: {} }, 0).shield.full, true);
  assert.equal(openTopOverlay({ comps: { enclosed: { enabled: true, seatOffset: null } } }, 0), null);
  assert.equal(openTopOverlay({ comps: { enclosed: { enabled: false } } }, 0), null);
  ```
  The `(0,0,0)` / 360 case also shows the label's head falls back to 1.45.
- [ ] **Step 2: Run the tests and confirm they fail.**
  Run: `cd TurretEditor && node --test test/open-top-overlay.test.js`
  Expected: FAIL (module not found).
- [ ] **Step 3: Implement the module, then wire up the inspector layers and drawing.**
- [ ] **Step 4: Run the tests and build.**
  Run: `cd TurretEditor && npm test && npm run build`. Expected: all pass; the build
  succeeds.
- [ ] **Step 5: Visual check.**
  - Start the editor (`npm run serve`) and open it in the built-in browser.
  - Open the Texture Inspector on Flak 38 and DCA 40.
  - Sweep the rotation slider. The seat should orbit with the top, the wedge should stay
    centred on the barrel, and the DCA 40 should show a ring.
  - Take one screenshot for the user.
- [ ] **Step 6: Checkpoint.**

### Task 9: Content & docs

**Files:**
- Modify the XML for:
  - `Cannons/57mmDeacon.xml`
  - `RotaryCannons/20mmM61Vulcan.xml`
  - `Autocannons/37mmFlakM42.xml`
  - `Howitzers/75mmLEIG18.xml`
  - `Howitzers/150mmSIG33.xml`
  - `Howitzers/233mmBLMK1.xml`

  All paths are under `Common/Defs/ThingDefs_Buildings/`. Flak 38 and DCA 40 were already
  done in Task 3.
- Modify: `Common/Defs/ThingDefs_Buildings/Turret_XML_Documentation.md` and
  `docs/xml_reference_guide.md`. Add one subsection, "Open-top seat & gun shield", with
  the four fields, units, frame convention, and the 1.45 fallback.

**Placeholder values** (the user tunes these in game):

| Turret | Group | `shieldArc` | `shieldArmor` |
|---|---|---|---|
| DCA 40 | all-round | 360 | 8 |
| Deacon | all-round | 360 | 10 |
| Vulcan | all-round | 360 | 6 |
| Flak 38 | front | 120 | 6 |
| Flak M42 | front | 120 | 6 |
| leIG 18 | front | 100 | 4 |
| sIG 33 | front | 100 | 5 |
| BL Mk1 | front | 100 | 6 |

Every open-top turret also gets:
- `seatOffset (0,0,-0.9)`, or `(0.7,0,-0.7)` for the 360° ones;
- `shieldHeight` = its `barrelHeight` − 0.25;
- `hidePawnGraphics false`, `flameProtection 0`, `temperatureProtection 0`,
  `explosiveProtection 0.3`;
- `bulletProtection` removed.

Existing comps keep any fields not listed here.

- [ ] **Step 1: Edit the six defs** using the values above.
- [ ] **Step 2: Check the defs load cleanly.**
  Launch the harness and run `node runner.js log level=error contains=AMC`. Expected: no
  `[AMC OpenTop]` config errors and no XML errors for the edited defs.
- [ ] **Step 3: Run the `seat` and `front` cases on every edited front-shield turret.**
  The turrets are Flak M42, leIG 18, sIG 33 and BL Mk1. Expected: `"pass":true`.
- [ ] **Step 4: Run the `front` case on Deacon and Vulcan.** Expected: `"pass":true`.
- [ ] **Step 5: Check the editor reads the new values.**
  Run `cd TurretEditor && npm test`, then `POST /api/extract`. Expected: all pass; the
  edited turrets show their values in the editor.
- [ ] **Step 6: Write the docs subsections.**
- [ ] **Step 7: Quit and checkpoint.**

### Task 10: Final verification

- [ ] **Step 1: Run the geometry tests.**
  Run: `dotnet run --project Source/OpenTopGeometry.Tests`. Expected: `ALL PASS`.
- [ ] **Step 2: Build the mod.** Expected: `0 Error(s)`.
- [ ] **Step 3: Run the TurretEditor tests.**
  Run: `cd TurretEditor && npm test`. Expected: all pass.
- [ ] **Step 4: Run every scenario case.**
  Use a fresh headless launch.
  - Flak 38 cases: `seat`, `front`, `rear`, `ap`, `he`, `ownfire`, `despawn`, `reload`.
  - DCA 40 cases: `seat`, `front`, `rear`, `he`.
  - BMP-2: `enclosed`.

  Expected: all `"pass":true`. Report any failure with its JSON line.
- [ ] **Step 5: Windowed look check, for the user.**
  - Run: `node runner.js launch --windowed=1280x720 ...`, then build the Flak 38 scenario
    setup.
  - Take a screenshot at top angles 0/90/180/270. The gunner should sit at the seat facing
    the barrel.
  - Send the screenshots to the user.
  - This is the height-accurate run; headless CE bounds are approximate.
- [ ] **Step 6: Report** to the user: which files changed, what's verified, and what
  remains for in-game tuning (the placeholder values). The user commits.
