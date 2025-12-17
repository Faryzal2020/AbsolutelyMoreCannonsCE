# Turret Mode Swap - Rotation Restoration Implementation

## Summary
Implemented rotation restoration for turret mode swapping, ensuring that after swapping between direct and indirect fire modes, the turret's rotation is set to aim at the restored target.

## Problem
When a turret mode swap occurs:
1. The new turret spawns with rotation of -999.0° (invalid initial value)
2. The turret doesn't automatically aim at the restored target
3. If the swap happens during a pause, the rotation needs to be applied after the turret top initializes

## Solution
Created a dedicated `CompDelayedRotation` component that:
1. Attaches to the newly spawned turret
2. Waits for 2 ticks to allow the turret top to initialize
3. Calculates the angle from turret to target using proper RimWorld coordinate system
4. Sets the `CurRotation` property on the turret's top

## Files Modified

### 1. `CompTurretModeSwap.cs`
**Changes:**
- Added logic to instantiate and attach `CompDelayedRotation` to newly spawned turrets
- The component is added dynamically to the turret's component list after spawning
- Schedules rotation update for 2 ticks after spawn to avoid the -999.0° initial value

**Key code section:**
```csharp
// Add delayed rotation component to the new turret
var delayedRotationComp = new CompDelayedRotation();
delayedRotationComp.parent = newTurret;
delayedRotationComp.Initialize(null);

// Add to the turret's components list
var compsField = newTurret.GetType().GetField("comps", BindingFlags.Instance | BindingFlags.NonPublic);
if (compsField != null)
{
    var comps = compsField.GetValue(newTurret) as List<ThingComp>;
    if (comps != null)
    {
        comps.Add(delayedRotationComp);
        delayedRotationComp.ScheduleRotation(forcedTarget, 2);
    }
}
```

### 2. `CompDelayedRotation.cs` (New File)
**Purpose:** Handles delayed rotation updates for turrets

**Key features:**
- Counts down ticks before applying rotation
- Calculates angle using RimWorld's coordinate system (north = 0°, clockwise)
- Sets rotation via reflection on the `CurRotation` property
- Includes save/load support via `PostExposeData`

**Coordinate conversion:**
```csharp
// RimWorld uses north = 0°, rotating clockwise
// Atan2 gives us east = 0°, rotating counter-clockwise
// Convert: RimWorld angle = 90 - Atan2 angle
float rimWorldAngle = 90f - angleDeg;
```

## Technical Details

### Timing
- Rotation is applied **2 ticks** after turret spawn
- This works whether the game is paused or running
- The delay ensures the turret top component is fully initialized

### Coordinate System
- **Input:** Target position (IntVec3 or Vector3)
- **Calculation:** `Atan2(z, x)` for angle in radians
- **Conversion:** RimWorld angle = 90° - Atan2 angle (in degrees)
- **Normalization:** Result clamped to 0-360° range

### Reflection Usage
The component uses reflection to:
1. Access the private `top` field of the turret
2. Set the `CurRotation` property on the turret top
3. Add itself to the turret's private `comps` list

## Testing Recommendations

1. **During Play:** Swap modes while the turret is actively tracking/firing
2. **While Paused:** Swap modes with game paused to verify 2-tick delay works
3. **Multiple Targets:** Test with targets at different angles (N, S, E, W)
4. **Save/Load:** Save game immediately after swap, reload to verify rotation persists

## Debug Logging

Look for these log messages:
```
[TurretModeSwap] Added CompDelayedRotation to schedule rotation
[CompDelayedRotation] Scheduled rotation for {turret} in 2 ticks
[CompDelayedRotation] Set turret rotation to {angle}° (aiming at {target})
[CompDelayedRotation] Applied rotation to {turret}
```

## Notes
- The component automatically handles paused and running game states
- No manual tick manipulation required
- Component is save/load compatible
- Works with both direct → indirect and indirect → direct swaps
