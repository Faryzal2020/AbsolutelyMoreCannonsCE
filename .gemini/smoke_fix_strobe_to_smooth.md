# Muzzle Smoke Fix: From Strobe Effect to Smooth Motion

## The Problem

### What Was Happening
The muzzle smoke appeared as:
- Smoke spawns at one position
- Fades out quickly (~0.5 seconds)
- New smoke appears at a different position
- Fades out quickly
- Repeats in a strobing/flickering pattern

### Root Cause
The original implementation was **spawning a NEW fleck every single tick** (60 times per second):

```csharp
// OLD CODE (WRONG):
for (each tick while particle is active) {
    // Update particle position
    particle.position += movement;
    
    // Spawn a BRAND NEW fleck at the new position  ← PROBLEM!
    FleckCreationData data = FleckMaker.GetDataStatic(particle.position, ...);
    map.flecks.CreateFleck(data);  // Creates new fleck
}
```

**Result:**
- 60 separate flecks spawned over 1 second
- Each fleck has a 1.5 second lifetime (`solidTime=0.5s + fadeOutTime=1.0s`)
- Each fleck is static (no inherent motion)
- Creates a "spawn → fade → spawn → fade" strobe effect instead of smooth motion

### Why This Doesn't Work
RimWorld's fleck system uses **fire-and-forget visual effects**:
- Once created, a fleck cannot be moved or updated
- Each fleck is an independent visual entity
- Flecks handle their own lifespan and physics
- You cannot control a fleck's position after creation

## The Solution

### New Approach: Spawn Once, Let RimWorld Handle Motion

```csharp
// NEW CODE (CORRECT):
// When delay expires, spawn ONE fleck with velocity
if (tracked.delayTicks == 0) {
    // Calculate direction angle
    float angleRadians = Mathf.Atan2(direction.x, direction.z);
    float angleDegrees = angleRadians * Mathf.Rad2Deg;
    
    // Create ONE fleck with directional velocity
    FleckCreationData data = FleckMaker.GetDataStatic(position, ...);
    data.velocityAngle = angleDegrees;  // Set direction
    data.velocitySpeed = maxVelocity;   // Set initial speed
    map.flecks.CreateFleck(data);
    
    // Done! RimWorld's fleck system handles the rest
}
```

**Result:**
- **ONE** fleck spawned when particle becomes active
- Fleck has initial velocity in the correct direction
- RimWorld's fleck physics handles:
  - Natural deceleration (based on `FleckDef` settings)
  - Wind interaction
  - Growth and fade timing
- Smooth, realistic motion from barrel outward

## Technical Details

### Fleck Motion Parameters

**Velocity Angle:**
- RimWorld coordinate system: 0° = North, 90° = East, 180° = South, 270° = West
- Calculated from direction vector using `Atan2(x, z)`
- Example: direction `(-0.98, 0, 0.18)` → angle `-79.6°` (mostly West, slightly North)

**Velocity Speed:**
- Set to `particle.maxVelocity` (e.g., 5.0 cells/second for 57mm Deacon)
- RimWorld flecks decelerate naturally based on `FleckDef` air resistance
- No cubic deceleration curve needed - RimWorld's physics is sufficient

### Particle Tracking Changes

**Before (Wrong):**
```
Spawn fleck every tick → 60 flecks → strobe effect
```

**After (Correct):**
```
Tick 0:  Turret fires, queue particle with delay
Tick 2:  Delay expires → spawn ONE fleck with velocity
Tick 3+: (Fleck motion controlled by RimWorld)
         (Particle tracking continues for telemetry only)
Tick 62: Remove particle from tracking
         (Fleck continues until its natural lifespan ends)
```

### Telemetry Logging Updates

The custom velocity tracking is now **FOR TELEMETRY ONLY**:

```csharp
// Position tracking is THEORETICAL - shows what cubic deceleration would do
particle.position += direction * GetCurrentVelocity() * deltaTime;

// Log clearly indicates this is not actual fleck position
AMCLogger.LogTurretSmokeParticleTick(
    $"Pos(THEORETICAL)=... | Vel(CALC)=... | " +
    $"NOTE: Actual fleck position controlled by RimWorld physics");
```

**Why keep tracking?**
- Useful for debugging
- Shows what the intended motion curve would be
- Allows comparison between theoretical and actual behavior
- Can be disabled to save performance (particle removed immediately after spawn)

## Performance Impact

### Before (Spawning Every Tick)
- 1 particle × 60 ticks = **60 fleck creations**
- 5 particles × 60 ticks = **300 fleck creations**
- High performance cost + visual strobing

### After (Spawn Once)
- 1 particle = **1 fleck creation**
- 5 particles = **5 fleck creations**
- **60x fewer fleck spawns!**
- Smooth visual motion

## Visual Behavior

### Expected Visual Effect (After Fix)
1. Cannon fires
2. **One** smoke puff appears at barrel tip
3. Puff moves smoothly in firing direction
4. Puff decelerates naturally
5. Puff grows and fades over ~1.5 seconds
6. Clean, realistic muzzle smoke effect

### Configuration Impact

**From 57mmDeacon.xml:**
- `muzzleVelocity=5.0` → Initial speed of smoke puff
- `muzzleParticleSize=2.0` → Size multiplier (2x default)
- `muzzleParticleCount=1` → Number of puffs
- `muzzleSpawnDuration=10` → Stagger multiple puffs (if count > 1)

**The custom `muzzleVelDuration` and cubic deceleration** are now only used for telemetry logging, not actual visual motion.

## Testing in Game

To verify the fix works:

1. Fire the 57mm Deacon cannon
2. **Expected:** Single large smoke puff shoots forward from barrel, decelerates, and fades
3. **NOT expected:** Flickering/strobing smoke that appears in discrete positions

**Enable telemetry to see:**
```
[AMC] [SMOKE_TELEMETRY] Spawned fleck for P0 at (137.50, 115.70) | 
                        Angle=-79.6° | InitialVel=5.00c/s | FleckDef=AMC_MuzzleSmoke
```

**Disable tick tracking** (not needed now) to avoid verbose logs since they show theoretical position, not actual fleck position.

## Recommendations

### If You Want Multi-Puff Effect
Set `muzzleParticleCount` to higher value:
```xml
<muzzleParticleCount>3</muzzleParticleCount>
<muzzleSpawnDuration>15</muzzleSpawnDuration>
```
Creates 3 puffs staggered over 15 ticks → continuous smoke stream

### If You Want Faster/Slower Smoke
Adjust `muzzleVelocity`:
```xml
<muzzleVelocity>8.0</muzzleVelocity>  <!-- Faster ejection -->
<muzzleVelocity>2.0</muzzleVelocity>  <!-- Slower drift -->
```

### If You Want Longer-Lasting Smoke
Edit the FleckDef in `AMC_SmokeFleckDefs.xml`:
```xml
<FleckDef ParentName="AMC_SmokeBase">
    <defName>AMC_MuzzleSmoke</defName>
    <solidTime>1.0</solidTime>      <!-- Was 0.5 -->
    <fadeOutTime>2.0</fadeOutTime>  <!-- Was 1.0 -->
</FleckDef>
```

## Code Cleanup Opportunities

Since custom velocity tracking is now only for telemetry:

1. **MuzzleSmokeParticle.cs** - Could be simplified or removed if telemetry not needed
2. **`muzzleVelDuration` parameter** - Only affects telemetry tracking duration
3. **Cubic deceleration curve** - Interesting for analysis but doesn't affect visuals

Consider:
- Remove custom tracking entirely if telemetry not valuable
- OR keep for educational/debugging purposes
- OR implement custom Mote_Thrown subclass for true custom physics (more complex)

## Summary

**Problem:** Spawning flecks every tick created strobing effect  
**Solution:** Spawn ONE fleck with velocity, let RimWorld handle motion  
**Result:** Smooth, realistic muzzle smoke that moves directionally and fades naturally  
**Tradeoff:** Lost custom cubic deceleration (RimWorld uses simpler physics)  
**Benefit:** 60x performance improvement + correct visual behavior
