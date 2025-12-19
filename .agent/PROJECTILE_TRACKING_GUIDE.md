# Projectile Tracking Debug Feature

## Overview
Added a new **Temporary Debug Logging** toggle in the mod settings that enables comprehensive projectile tracking for CE turrets. When enabled, the mod logs detailed information about each projectile every tick from launch to destruction.

## How to Use

### 1. Enable the Feature
- Open **Mod Settings** → **Absolutely More Cannons**
- Scroll to the **Diagnostic Logging** section
- Check **"Enable Temporary Debug Logs"**
- The setting is saved automatically

### 2. Fire Your Turrets
- Any projectile launched from a CE turret will now be tracked
- Check the **Dev Console** or **Player.log** for detailed logs

## What Gets Logged

### At Launch (═══ PROJECTILE LAUNCHED ═══)
```
ID: #[hash] (projectile name)
Launcher: [turret name] at (x, z)
Initial YAW: [degrees]° (map-relative, North=0°)
Initial PITCH: [degrees]° (elevation angle)
Shot Height: [value]
Shot Speed: [value]
```

### Every Tick (PROJECTILE TICK #[id])
```
Tick [N] (Flight: [ticks])
Pos: (x, y, z)
Dist: [distance from origin]
Vel: (vx, vy, vz) | Speed: [magnitude]
YAW: [degrees]° (map-relative, signed)
PITCH: [degrees]° (signed, +up/-down)
ShotAngle: [if available]°
ShotRotation: [if available]°
```

### At Destruction (═══ PROJECTILE DESTROYED ═══)
```
ID: #[hash] (projectile name)
Launcher: [turret name]
Lifetime: [ticks] ([seconds] seconds)
Total Distance: [tiles] tiles
Average Speed: [tiles/sec] tiles/sec
Final Position: (x, y, z)
Final YAW: [degrees]° | Final PITCH: [degrees]°
Final Velocity: (vx, vy, vz)
```

## Angle Reference Frame

### YAW (Horizontal/Azimuth Angle)
- **Reference**: Map-relative, North = 0°
- **Sign Convention**: SIGNED (-180° to +180°)
  - 0° = North
  - +90° = East
  - ±180° = South
  - -90° = West
- **Calculated from**: Velocity vector's horizontal component (X, Z)

### PITCH (Vertical Angle)
- **Reference**: Horizontal plane
- **Sign Convention**: SIGNED (-90° to +90°)
  - +90° = Straight up
  - 0° = Horizontal
  - -90° = Straight down
- **Calculated from**: Velocity vector's vertical component (Y) vs horizontal magnitude

## Technical Details

### Files Modified
1. **AMCSettings.cs** - Added `logTemporaryDebug` boolean toggle
2. **AMCLogger.cs** - Added `LogTemporaryDebug()` method
3. **HarmonyPatches_ProjectileTracking.cs** (NEW) - Harmony patches for projectile tracking

### Harmony Patches Applied
- `ProjectileCE.Launch` (Postfix) - Start tracking projectile
- `ProjectileCE.Tick` (Postfix) - Log state every tick
- `ProjectileCE.Destroy` (Prefix) - Log final state and cleanup

### Performance Considerations
- **Only turret projectiles** are tracked (pawns/other launchers are ignored)
- Tracking uses a lightweight dictionary with projectile hash as key
- Failed reflection attempts are silently caught to avoid log spam
- **Disable when not debugging** - logs every tick can be verbose!

## Example Log Output
```
[AMC] [TEMP_DEBUG] ═══ PROJECTILE LAUNCHED ═══
  ID: #12345678 (he shell)
  Launcher: Pak 40 75mm AT gun at (100.50, 50.25)
  Initial YAW: 45.00° (map-relative, North=0°)
  Initial PITCH: 5.23° (elevation angle)
  Shot Height: 0.85
  Shot Speed: 120.5
═══════════════════════════

[AMC] [TEMP_DEBUG] PROJECTILE TICK #12345678 (he shell): Tick 1 (Flight: 0) | Pos: (100.52, 0.87, 50.27) | Dist: 0.03 | Vel: (85.21, 10.98, 85.21) | Speed: 120.92 | YAW: 45.02° (map-relative, signed) | PITCH: 5.20° (signed, +up/-down) | ShotAngle: 5.23° | ShotRotation: 45.00° |

[AMC] [TEMP_DEBUG] PROJECTILE TICK #12345678 (he shell): Tick 2 (Flight: 1) | Pos: (100.54, 0.88, 50.29) | Dist: 0.05 | Vel: (85.21, 10.78, 85.21) | Speed: 120.88 | YAW: 45.02° (map-relative, signed) | PITCH: 5.10° (signed, +up/-down) | ShotAngle: 5.23° | ShotRotation: 45.00° |

... [continues every tick] ...

[AMC] [TEMP_DEBUG] ═══ PROJECTILE DESTROYED ═══
  ID: #12345678 (he shell)
  Launcher: Pak 40 75mm AT gun
  Lifetime: 120 ticks (2.00 seconds)
  Total Distance: 45.32 tiles
  Average Speed: 22.66 tiles/sec
  Final Position: (145.32, 0.12, 95.57)
  Final YAW: 44.98° | Final PITCH: -12.35°
  Final Velocity: (78.12, -17.23, 78.15)
═════════════════════════════
```

## Use Cases
- Debug projectile trajectory issues
- Verify ballistics calculations
- Analyze angle deviations
- Track velocity changes over flight
- Measure actual vs expected range/speed
- Investigate collision/impact behavior

## Notes
- Logs use `[TEMP_DEBUG]` prefix for easy filtering
- All angles clearly marked as SIGNED and map-relative
- Works with all CE projectile types (shells, bullets, rockets, etc.)
- Compatible with modded projectiles that extend ProjectileCE
