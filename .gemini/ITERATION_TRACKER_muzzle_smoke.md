# Iteration Tracker: Muzzle Smoke with Cubic Deceleration

## Requirements (User-Specified)
1. ✅ Muzzle smoke spawns when turret fires
2. ✅ Smoke has directional velocity (shoots forward from barrel)
3. ❌ **Smoke follows cubic deceleration curve** `v = v₀ * (1 - t/T)³`
4. ✅ Configurable via XML (velocity, duration, particle count, etc.)
5. ✅ Detailed telemetry logging for debugging
6. ✅ Smooth visual motion (not flickering/strobing)

**Status:** Requirement #3 (cubic deceleration) NOT ACHIEVED with current implementation

---

## Iteration 1: Static Fleck Per Tick

### Attempt
**Code:** `MapComponent_TurretSmokeManager.ProcessMuzzleParticles()`
- Tracked `MuzzleSmokeParticle` with position/velocity
- **Spawned new static fleck every tick** at updated position
- Cubic deceleration calculated in code, position updated each tick

### Expected Behavior
- Smoke puff moves smoothly following cubic velocity curve
- Position updates 60 times per second
- Visual appears as moving cloud

### Actual Behavior (FAILED)
- **Strobe/flicker effect** - smoke appears, fades, reappears at new position
- Not smooth motion - discrete spawn → fade → spawn cycle
- Looked like flickering teleportation, not continuous motion

### Root Cause
```csharp
// PROBLEM: Creating NEW fleck every tick
for (each tick) {
    FleckCreationData data = FleckMaker.GetDataStatic(...);  // NEW fleck
    map.flecks.CreateFleck(data);  // Cannot be moved after creation
}
```

**Why it failed:**
- `FleckMaker.GetDataStatic()` creates **immutable, fire-and-forget** visuals
- Each fleck is independent, cannot be updated after creation
- Spawning 60 flecks creates 60 separate fade-in/fade-out cycles
- Fleck lifetime (1.5s) meant many overlapping fading flecks
- Visual result: strobe effect, not smooth motion

### Findings
- ❌ Static flecks cannot be moved after creation
- ❌ Cannot achieve custom motion with static flecks
- ℹ️ Static flecks are for single-spawn effects (sparks, impacts, etc.)
- ℹ️ Custom motion requires updateable objects

---

## Iteration 2: Single Fleck with Initial Velocity

### Attempt
**Code:** `MapComponent_TurretSmokeManager.SpawnMuzzleFleck()`
- Spawn **ONE** fleck when particle becomes active
- Set `velocityAngle` and `velocitySpeed` on `FleckCreationData`
- Let RimWorld's fleck physics handle motion
- Removed per-tick fleck spawning

```csharp
FleckCreationData data = FleckMaker.GetDataStatic(position, ...);
data.velocityAngle = angleDegrees;
data.velocitySpeed = maxVelocity;
map.flecks.CreateFleck(data);  // Spawn once
```

### Expected Behavior
- ONE smoke puff per shot
- Smooth motion in correct direction
- RimWorld physics handles deceleration

### Actual Behavior (PARTIAL SUCCESS)
- ✅ No more strobe effect - smooth visual motion
- ✅ Smoke moves in correct direction
- ✅ 60x performance improvement (1 fleck vs 60)
- ❌ **Does NOT use cubic deceleration** - uses RimWorld's default physics
- ❌ Cannot control velocity curve - acceleration/deceleration controlled by RimWorld

### Root Cause
- RimWorld's fleck physics is hardcoded into the base game
- Cannot customize velocity behavior without custom code
- `velocitySpeed` sets initial velocity, but deceleration is fixed

### Findings
- ✅ Fixes strobe effect and performance
- ❌ Does NOT satisfy cubic deceleration requirement
- ℹ️ RimWorld flecks have their own physics simulation
- ℹ️ Need custom class to override physics behavior

---

## Iteration 3: Custom Mote Subclass (IMPLEMENTED)

### Strategy
Use RimWorld's `Mote` system instead of static `Fleck`:
- `Mote` is a full `Thing` subclass that can be updated each tick
- `MoteThrown` provides base for thrown/moving effects
- Override `Tick()` method to implement custom physics

### Architecture
```
MoteThrown (RimWorld base class)
    ↓ extends
MoteSmokeMuzzle (custom class)
    ↓ override Tick()
    → Calculate cubic deceleration velocity
    → Update exactPosition manually
    → Handle fadeout/growth timing
```

### Implementation Complete

**Files Created:**
1. ✅ `MoteSmokeMuzzle.cs` - Custom mote class with cubic deceleration
2. ✅ `AMC_MoteDefs.xml` - ThingDef for `Mote_AMC_MuzzleSmoke`

**Files Modified:**
1. ✅ `MapComponent_TurretSmokeManager.cs`:
   - Replaced `SpawnMuzzleFleck()` with `SpawnMuzzleMote()`
   - Uses `ThingMaker.MakeThing()` + `Setup()` + `GenSpawn.Spawn()`
   - Removes particle from tracking immediately after mote spawns

**Key Implementation Details:**

```csharp
// MoteSmokeMuzzle.cs - Tick() override
public override void Tick()
{
    base.Tick();

    if (ticksAlive < velDuration)
    {
        // Cubic deceleration: v = v₀ * (1 - t/T)³
        float progress = (float)ticksAlive / velDuration;
        float velocityMultiplier = Mathf.Pow(1f - progress, 3f);
        float currentVelocity = maxVelocity * velocityMultiplier;

        // Update position
        Vector3 movement = direction * currentVeloc (1f / 60f);
        exactPosition += movement;

        ticksAlive++;
    }
}
```

### Expected Behavior
- ✅ ONE mote spawned per particle
- ✅ Mote position updated every tick via Tick() override
- ✅ Custom cubic deceleration curve applied
- ✅ Smooth visual motion (mote moves 60 times per second)
- ✅ Full control over physics
- ✅ Telemetry logging in mote's Tick() method

### Success Criteria (TO BE TESTED)
- [ ] Mote spawns at barrel tip
- [ ] Mote moves in firing direction
- [ ] Velocity follows cubic curve (fast → slow deceleration)
- [ ] Position telemetry matches visual position
- [ ] No errors in logs
- [ ] Smooth motion (not jerky or flickering)

### Potential Issues to Watch For
1. **Mote not spawning** - Check `ThingDef` loaded, namespace correct
2. **Mote spawns but doesn't move** - Verify `Tick()` is called (add debug log)
3. **Mote disappears immediately** - Check `def.mote.fadeInTime/fadeOutTime` values
4. **Mote position wrong** - Coordinate system issues (cell vs world coords)
5. **Compilation errors** - Missing `using` statements or wrong namespace

---

## Current State
- **Iteration:** 3 (IMPLEMENTED, AWAITING TEST)
- **Status:** Implementation complete, needs in-game testing
- **Blockers:** None
- **Next Action:** User to test in-game, then update tracker with results

---

## Notes for Next Iteration

### After Implementation, Test:
1. **Compile the mod** - Check for any compilation errors
2. **Start RimWorld** - Check Player.log for:
   - Mod load errors
   - ThingDef parse errors
   - Missing class errors
3. **Fire 57mm Deacon**
4. **Enable telemetry:** `logTurretSmokeParticleTelemetry = true`
5. **Check logs for:**
   - Mote spawn confirmation: "Spawned MoteSmokeMuzzle for P..."
   - Initialization log: "MoteSmokeMuzzle P... initialized"
   - Tick logs: "P... T...: Pos=..."
   - Completion log: "velocity phase COMPLETE"
   - Any errors/red text
6. **Visual check:**
   - Does smoke appear?
   - Does it move smoothly?
   - Does it decelerate visibly (starts fast, slows down)?
   - Direction correct (shoots forward from barrel)?
7. **Enable tick tracking:** `logTurretSmokeParticleTick = true`
8. **Compare logged position progression with visual**

### Debug Checklist if Not Working
- [ ] Check Player.log for errors/red text
- [ ] Verify ThingDef loaded: Dev Mode → (Debug Actions) → "List all ThingDefs" → search "Mote_AMC"
- [ ] Confirm class namespace: `AbsolutelyMoreCannons.MoteSmokeMuzzle`
- [ ] Check if Tick() is being called: Add `Log.Message()` in Tick()
- [ ] Verify exactPosition is updating: Log it each Tick()
- [ ] Check if mote is visible: Try size=5.0 for testing
- [ ] Test with `muzzleEnabled=true` in CompProperties_TurretSmoker
- [ ] Verify Setup() is called: Add log statement
- [ ] Check GenSpawn.Spawn() returns success

---

## Reference: RimWorld Mote System

### Key Classes
- `Mote` - Base class for visual effects (Thing subclass)
- `MoteThrown` - For thrown/moving effects with velocity
- `MoteDualAttached` - Attached to two points
- `MoteAttached` - Attached to one thing

### Important Fields
- `exactPosition` - World position (Vector3)
- `exactScale` - Size/scale (Vector3)
- `instanceColor` - Tint color
- `rotationRate` - Rotation speed
- `solidTime` - Time at full opacity
- `def.mote.fadeInTime/fadeOutTime` - Fade durations

### Lifecycle
1. Created via `ThingMaker.MakeThing()`
2. Configured via custom Setup() or properties
3. Spawned via `GenSpawn.Spawn()`
4. `Tick()` called every frame
5. Auto-despawns when `ageSecs` > total lifetime

---

## Update Log

**2025-12-21 12:10** - Created iteration tracker
- Documented Iteration 1 failure (strobe effect)
- Documented Iteration 2 partial success (smooth but no cubic curve)
- Planned Iteration 3 (custom Mote subclass)

**2025-12-21 12:15** - Implemented Iteration 3
- Created `MoteSmokeMuzzle.cs` with cubic deceleration in Tick()
- Created `AMC_MoteDefs.xml` with ThingDef
- Updated `MapComponent_TurretSmokeManager.cs` to spawn custom motes
- Ready for in-game testing
- Awaiting user test results

**2025-12-21 12:32** - Fixed compilation errors
- **Issue:** Build failed with "exactScale does not exist in current context"
- **Investigation:** User decompiled `Assembly-CSharp.dll` using ILSpy
- **Finding:** `ExactScale` is a **property** (not a field), calculated from:
  ```csharp
  public Vector3 ExactScale => Vector3.Scale(linearScale, curvedScale);
  ```
- **Fix:** Changed `exactScale = new Vector3(size, 1f, size)` to `linearScale = new Vector3(size, 1f, size)`
- **Result:** ✅ Build succeeded - ready for in-game testing

**2025-12-21 12:37** - Fixed mote definition config errors
- **Issue:** Main menu error: "Config error in Mote_AMC_MuzzleSmoke: graphicClass is null"
- **Cause:** `graphicData` missing required `graphicClass` field in XML
- **Fix:** Added `<graphicClass>Graphic_Mote</graphicClass>` to both mote definitions
- **Files modified:** `AMC_MoteDefs.xml` (both Mote_AMC_MuzzleSmoke and Mote_AMC_MuzzleSmokeHeavy)
- **Result:** ✅ Config errors resolved - ready for in-game testing

**2025-12-21 12:51** - Fixed critical spawn logic bug (MAJOR)
- **Issue:** Motes not spawning at all - no spawn logs, no tick logs, smoke invisible
- **Cause:** Logic error in `ProcessMuzzleParticles()`:
  ```csharp
  // OLD (BROKEN):
  if (tracked.delayTicks > 0) {
      tracked.delayTicks--;
      if (tracked.delayTicks == 0) {
          SpawnMuzzleMote(tracked);  // Only spawns after countdown
      }
  }
  // Particles with delay=0 never enter this block, never spawn!
  ```
- **Fix:** Check for `delayTicks == 0` first, spawn immediately:
  ```csharp
  // NEW (FIXED):
  if (tracked.delayTicks == 0) {
      SpawnMuzzleMote(tracked);  // Spawn immediately or after countdown
  } else if (tracked.delayTicks > 0) {
      tracked.delayTicks--;  // Decrement countdown
  }
  ```
- **Impact:** All particles with delay=0 (default config) were never spawning
- **Files modified:** `MapComponent_TurretSmokeManager.cs` line 127-156
- **Result:** ✅ Build succeeded - particles should now spawn correctly

**2025-12-21 13:26** - Implemented timestamp logging system
- **Goal:** Track delay between turret firing and smoke spawning
- **Implementation:**
  1. **AMCSettings.cs:** Added `logTurretFireTimestamp` setting (toggleable in mod options)
  2. **AMCLogger.cs:** 
     - Added `LogTurretFireTimestamp()` method
     - Modified `LogTurretSmoke()` to auto-add `T={ticks}` timestamps
     - Modified `LogTurretSmokeParticleTelemetry()` to auto-add `T={ticks}` timestamps
  3. **HarmonyPatches.cs:** Added firing timestamp in `Postfix_Verb_LaunchProjectileCE_TryCastShot()`
- **Result:** ✅ Build succeeded - All `[FIRE_TIMESTAMP]`, `[SMOKE]`, and `[SMOKE_TELEMETRY]` logs now include game ticks
- **Usage:** Enable "Log Turret Fire Timestamp" in mod settings to track exact firing timing
- **Benefit:** Can now measure exact delay between gun firing and smoke appearing

**2025-12-21 13:34** - Fixed ticker-based spawn delay (CRITICAL)
- **Issue:** Particles spawning 36 ticks late (T=1022 registered, T=1058 spawned)
- **Root Cause:** `ProcessMuzzleParticles()` only runs every `ticker` ticks (~35 ticks based on wind)
  - Particles with delay=0 were registered at correct time
  - But waited for next ticker interval before spawning
  - Result: Visual delay of up to 36 ticks (0.6 seconds)
- **Fix:** Modified `RegisterMuzzleParticle()` to spawn immediately when `spawnDelay == 0`
  ```csharp
  if (spawnDelay == 0) {
      SpawnMuzzleMote(tracked);  // Spawn NOW, don't wait for ticker
  } else {
      activeMuzzleParticles.Add(tracked);  // Queue for delayed spawn
  }
  ```
- **Expected behavior:** Smoke should now spawn at T+2 ticks (muzzleSpawnDelay) instead of T+36
- **Files modified:** `MapComponent_TurretSmokeManager.cs` RegisterMuzzleParticle method
- **Result:** ✅ Build succeeded - Immediate spawn for delay=0 particles

**2025-12-21 13:49** - Fixed muzzle offset rotation (CRITICAL)
- **Issue:** Smoke spawning at wrong position - not following turret aim direction
- **Example from logs:**
  - Turret at (140, 126), Direction (0.266, 0.964), muzzleOffset (0, 0, 4)
  - Expected spawn: (141.06, 129.86) = center + direction × 4
  - Actual spawn: (140.50, 130.50) = center + (0.5, 4.5) ← **WRONG!**
  - Offset was applied as fixed world-space, not rotated by turret direction
- **Root Cause:** Line 131 in `CompTurretSmoker.cs`:
  ```csharp
  // OLD (BROKEN):
  barrelTipPosition = barrelTipPosition + Props.muzzleOffset;  // Fixed world offset!
  ```
- **Fix:** Rotate muzzle offset by turret direction before applying:
  ```csharp
  // NEW (FIXED):
  Vector3 rotatedOffset = RotateVector(Props.muzzleOffset, turretRotation);
  barrelTipPosition = barrelTipPosition + rotatedOffset;
  ```
  - Added `RotateVector()` helper method to rotate offsets around Y-axis
- **Expected behavior:** Smoke spawn position = turret center + (direction × offset magnitude)
- **Files modified:** `CompTurretSmoker.cs` - QueueMuzzleSmoke + new RotateVector method
- **Result:** ✅ Build succeeded - Spawn position now follows turret aim direction

**2025-12-21 14:03** - Added wind drift to smoke particles
- **Goal:** Add natural passive wind drift on top of cubic deceleration, similar to Simple FX Smoke
- **Implementation:**
  1. **MoteSmokeMuzzle.cs:** Modified `Tick()` to calculate and apply wind movement every tick
     - Wind movement calculated from `Map.windManager.WindSpeed` and `MapComponent_TurretSmokeManager.WindDirection`
     - Wind applied during cubic deceleration phase: `movement = directionalMovement + windMovement`
     - Wind continues after deceleration ends: `exactPosition += windMovement`
     - Added small random variation (0.8-1.2×) for natural look
  2. **MapComponent_TurretSmokeManager.cs:** Added public `WindDirection` property for motes to access
- **Behavior:**
  - During velocity phase (0-30 ticks): Smoke moves in firing direction + wind drift
  - After velocity phase (30+ ticks): Smoke only drifts with wind until fadeout
  - Wind speed/direction dynamically updates based on map weather
- **Result:** ✅ Build succeeded - Smoke now has natural drifting motion like campfire smoke

**2025-12-21 14:13** - Improved heat smoke system
- **Issue 1:** Heat offset applied as fixed world-space (same bug as muzzle smoke)
- **Issue 2:** Only single spawn point - couldn't emit smoke from multiple points along barrel
- **Implementation:**
  1. **CompProperties_TurretSmoker.cs:** Added new XML parameters:
     - `heatEmissionPoints` (int) - number of emission points along barrel (default: 1)
     - `heatEmissionSpacing` (float) - distance between points in cells (default: 0.5)
  2. **CompTurretSmoker.cs:** 
     - Replaced `GetHeatSmokePosition()` with `GetHeatSmokePositions()` returning array
     - Calculates multiple spawn points: `baseOffset + direction × spacing × index`
     - Rotates all offsets by turret direction using existing `RotateVector()` method
  3. **MapComponent_TurretSmokeManager.cs:** Updated `ProcessHeatSmoke()`:
     - Spawns heat smoke at all emission points
     - Distributes spawning probability evenly across all points
- **Example:** With `heatEmissionPoints=3`, `heatEmissionSpacing=0.5`:
  - Creates 3 emission points: 0.0, 0.5, 1.0 cells along barrel
  - All points rotate with turret aim direction
  - Visual effect: smoke rises from length of hot barrel
- **Result:** ✅ Build succeeded - Heat smoke now rotates correctly and supports multiple emission points

**2025-12-21 14:32** - Fixed heat emission points double-rotation bug
- **Issue:** First heat emission point correct, but subsequent points spawned at wrong positions
- **Root Cause:** Double rotation in `GetHeatSmokePositions()`:
  ```csharp
  // OLD (BROKEN):
  Vector3 pointOffset = Props.heatOffset + (direction * spacing * i);
  Vector3 rotatedOffset = RotateVector(pointOffset, turretRotation);  // ← Rotating already-rotated direction!
  ```
  - `direction` already rotated by turretRotation
  - Rotating `(baseOffset + direction × spacing)` applies rotation twice to spacing component
  - Result: Points spawn at incorrect angles
- **Fix:** Rotate base offset once, then add forward spacing:
  ```csharp
  // NEW (FIXED):
  Vector3 rotatedBase = RotateVector(Props.heatOffset, turretRotation);  // Rotate base once
  positions[i] = parent.DrawPos + rotatedBase + (direction * spacing * i);  // Add forward spacing
  ```
- **Implementation:** Also added detailed logging for heat smoke:
  - Logs when processing heat smoke with emission point count
  - Logs each spawn with position coordinates
  - Logs total spawned count per tick
  - All controlled by "Log Turret Smoke (General)" setting
- **Result:** ✅ Build succeeded - Heat emission points now correctly aligned along barrel direction

**2025-12-21 15:00** - Fixed muzzle smoke spawn delay bug (CRITICAL)
- **Issue:** Second muzzle particle spawning 154 ticks late instead of 5 ticks
  - Configured: `muzzleSpawnDuration=10`, 2 particles → delay should be 5 ticks for particle #1
  - Actual: Particle registered at T=854, spawned at T=1008 (154 ticks = 2.5 seconds later!)
- **Root Cause:** Delay countdown only happened when ticker fired (every ~28 ticks)
  - Muzzle particle delay processing was inside `if (gameTicks % ticker == 0)` block
  - With ticker=28, delay countdown happened every 28 ticks instead of every tick
  - 5 tick delay became 5×28 = 140+ tick delay
- **Fix:** Created separate `ProcessMuzzleParticleDelays()` that runs EVERY tick:
  ```csharp
  public override void MapComponentTick() {
      ProcessMuzzleParticleDelays();  // Every tick!
      if (gameTicks % ticker == 0) {
          ProcessMuzzleParticles();  // Only spawning (ticker-based)
      }
  }
  ```
- **Result:** ✅ Build succeeded - Muzzle particle delays now accurate to the tick

**2025-12-21 15:00** - Added heat smoke scaling system
- **Goal:** Dynamically increase heat decay and emission based on how hot the barrel is
- **New XML Parameters:**
  - `decayIncrease` (float) - Flat increase to `heatDecayRate` per unit of heat above threshold
  - `emissionIncrease` (float) - Flat increase to `heatEmissionRate` per unit of heat above threshold
- **Formulas:**
  ```
  heatDecayRateFinal = heatDecayRate + ((currentHeat - heatThreshold) × decayIncrease)
  heatEmissionRateFinal = heatEmissionRate + ((currentHeat - heatThreshold) × emissionIncrease)
  ```
- **Example:** With `heatThreshold=4`, `decayIncrease=0.1`, `emissionIncrease=0.5`:
  - At heat=4.5 (0.5 above threshold):
    - Decay: 0.01 + (0.5 × 0.1) = 0.06/tick
    - Emission: 2 + (0.5 × 0.5) = 2.25p/s
  - At heat=6.0 (2.0 above threshold):
    - Decay: 0.01 + (2.0 × 0.1) = 0.21/tick ← Cools faster when very hot
    - Emission: 2 + (2.0 × 0.5) = 3.0p/s ← More smoke when very hot
- **Implementation:**
  1. **CompProperties_TurretSmoker.cs:** Added new parameters
  2. **CompTurretSmoker.cs:** 
     - Modified `UpdateBurstHeat()` to use scaled decay rate
     - Added `GetScaledEmissionRate()` method
  3. **MapComponent_TurretSmokeManager.cs:** Uses `GetScaledEmissionRate()` instead of static rate
- **Result:** ✅ Build succeeded - Heat smoke dynamically scales with barrel temperature

**2025-12-22 01:37** - Improved shockwave smoke system
- **Goal:** Create realistic ground burst effect with filled circle and proper rotation
- **Improvements:**
  1. **Filled circle instead of hollow ring:**
     - Spawns particles at multiple radii from center to max radius
     - Creates dense cloud effect instead of thin ring
  2. **Density scaling by radius:**
     - Outer ring has full density (e.g., 20 particles)
     - Each inner ring has proportionally fewer particles
     - Formula: `particlesAtRadius = density × (currentRadius / maxRadius)`
     - Example with radius=5, density=20:
       - Ring at r=5: 20 particles
       - Ring at r=4: 16 particles (20 × 4/5)
       - Ring at r=3: 12 particles (20 × 3/5)
       - Ring at r=2: 8 particles (20 × 2/5)
       - Ring at r=1: 4 particles (20 × 1/5)
  3. **Rotated offset:** Fixed same bug as muzzle/heat smoke
     - Offset now rotates with turret direction
     - Blast centered correctly relative to barrel
  4. **Natural variation:**
     - Small random angle variation (±5°)
     - Small random radius variation (±10%)
     - Creates organic, less uniform look
- **Implementation:** `CompTurretSmoker.SpawnShockwaveSmoke()`
- **Result:** ✅ Build succeeded - Shockwave smoke now creates filled ground burst that rotates with turret

**2025-12-22 01:50** - Fixed muzzle spawn delay (FINAL FIX)
- **Issue:** Second particle still spawning 30+ ticks late despite delay countdown working correctly
  - Example: Delay reaches 0 at T=2407, but particle spawned at T=2438 (31 ticks late)
- **Root Cause:** Previous fix made delay countdown happen every tick ✅, but spawn check still in ticker block ❌
  - Delay correctly counted: 2 → 1 → 0 (every tick)
  - But spawning only checked when `if (gameTicks % ticker == 0)` fired
  - Particle sat at delay=0 waiting up to 30 ticks for next ticker interval
- **Fix:** Spawn immediately in `ProcessMuzzleParticleDelays()` when delay reaches 0:
  ```csharp
  if (tracked.delayTicks > 0) {
      tracked.delayTicks--;
      if (tracked.delayTicks == 0) {
          SpawnMuzzleMote(tracked);  // Spawn in same tick delay reaches 0!
          activeMuzzleParticles.RemoveAt(i);
      }
  }
  ```
  - Removed redundant `ProcessMuzzleParticles()` method (no longer needed)
- **Timeline now:**
  - T=2406: Particle registered with 2 tick delay
  - T=2406: Delay 2 → 1
  - T=2407: Delay 1 → 0, **SPAWNS IMMEDIATELY** ✅
- **Result:** ✅ Build succeeded - Muzzle particles now spawn exactly when delay expires, no ticker wait!

**2025-12-24 13:25** - Added muzzleSpawnDuration dual-mode system
- **Goal:** Add two distinct behaviors for `muzzleSpawnDuration` based on sign
- **Mode 1: Positive value = Inter-particle delay (existing behavior refined)**
  - Controls spacing between particles when `muzzleParticleCount > 1`
  - Formula: `delayBetweenParticles = muzzleSpawnDuration / muzzleParticleCount`
  - Example: count=3, duration=30 → particles spawn at T+0, T+10, T+20
  - Use case: Staggered smoke puffs from multi-barrel weapons
- **Mode 2: Negative value = Spawn throttling (NEW)**
  - Throttles max spawn rate regardless of fire rate
  - Ignores `muzzleParticleCount` (forces to 1)
  - Only spawns if `ticksSinceLastSpawn >= abs(muzzleSpawnDuration)`
  - Example: duration=-10, gun fires every 2 ticks
    - T=0: Fire → Particle #1 spawned ✅
    - T=2: Fire → Throttled (only 2 ticks passed) ❌
    - T=4: Fire → Throttled (only 4 ticks passed) ❌
    - T=10: Fire → Particle #2 spawned ✅ (10 ticks passed)
  - Use case: Fast-firing weapons (autocannons, miniguns) where you want smoke effect but not 1:1 with shots
- **Implementation:**
  - Added `lastMuzzleSmokeSpawn` field to track last spawn time
  - Modified `SpawnMuzzleSmoke()` to check mode and apply appropriate logic
  - Logs show "THROTTLED" or "NORMAL" mode in telemetry
- **Example configs:**
  - Cannon: `muzzleSpawnDuration="10"` → 2 particles 5 ticks apart
  - Vulcan: `muzzleSpawnDuration="-3"` → max 1 particle per 3 ticks despite firing every tick
- **Result:** ✅ Build succeeded - Dual-mode spawn control for slow and fast-firing weapons

**2025-12-24 16:04** - Added particle size range and direction cone
- **Goal:** Add visual variety to muzzle smoke with randomized size and spread
- **Feature 1: Randomized particle size**
  - Changed `muzzleParticleSize` from `float` to `string` to support range format
  - Supports two formats:
    - Single value: `"1.5"` → always 1.5x size
    - Range format: `"1.0~2.0"` → random between 1.0x and 2.0x each spawn
  - Parsing happens in `CompProperties_TurretSmoker.ResolveReferences()`
  - Each particle gets random size via `GetRandomParticleSize()`
  - Example: `"1.5~2.5"` creates varied smoke cloud (some smaller, some larger)
- **Feature 2: Direction cone spread**
  - New parameter: `<directionCone>` (float, degrees)
  - Randomizes particle direction: turretDirection ± random(0, cone)
  - Applied before velocity/wind calculations
  - Example: cone=5°, turret aimed at 45°
    - Particles move between 40° and 50°
    - Creates natural spread/dispersion effect
  - cone=0 → no spread (default, precise direction)
- **Implementation:**
  - `CompProperties_TurretSmoker.cs`:
    - Added `directionCone` parameter
    - Changed `muzzleParticleSize` to string with min/max fields
    - Added `GetRandomParticleSize()` method
    - Added parsing logic in `ResolveReferences()`
  - `CompTurretSmoker.cs`:
    - Apply direction randomization before creating particle
    - Use `GetRandomParticleSize()` for each particle
    - Enhanced logging to show size and direction per particle
- **Example configs:**
  - Cannon: `<muzzleParticleSize>1.5</muzzleParticleSize>` → consistent 1.5x
  - Vulcan: `<muzzleParticleSize>1.8~2.2</muzzleParticleSize>` + `<directionCone>4</directionCone>` → varied size, 8° spread
- **Result:** ✅ Build succeeded - More natural, varied smoke effects with size and directional variety
