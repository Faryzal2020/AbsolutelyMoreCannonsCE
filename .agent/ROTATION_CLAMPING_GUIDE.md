# Rotation Clamping Implementation

## Overview
This implementation adds **configurable rotation clamping** for turret-fired projectiles in Combat Extended. The feature:
1. **Diagnostic logging** to determine the reference frame of `shotRotation`
2. **Configurable clamping** of lateral deviation (yaw) for turrets only
3. **Toggle-able** from mod settings

## How It Works

### Reference Frame Detection
The patch includes diagnostic logging that analyzes the `shotRotation` field's reference frame by comparing it to:
- **Turret Base Rotation**: The actual direction the turret is facing (RimWorld coordinates)
- **Target Direction**: The ideal direction to the target
- **shotRotation Value**: The CE field we're analyzing

The diagnostic output will show:
```
[AMC DIAGNOSTIC] ═══ Rotation Reference Frame Analysis ═══
[AMC DIAGNOSTIC] Turret: 128mm Pak 44
[AMC DIAGNOSTIC] Turret Base Rotation (RW coords): 45.000°
[AMC DIAGNOSTIC] shotRotation (CE field): 48.500°
[AMC DIAGNOSTIC] Target Direction (RW coords): 46.000°
[AMC DIAGNOSTIC] shotRotation - idealDir = 2.500°
[AMC DIAGNOSTIC] shotRotation - turretBase = 3.500°
[AMC DIAGNOSTIC] 
[AMC DIAGNOSTIC] INTERPRETATION:
[AMC DIAGNOSTIC] → shotRotation appears to be RELATIVE TO TURRET BASE
[AMC DIAGNOSTIC] → shotRotation is the OFFSET/DEVIATION from turret aim
```

### Clamping Logic
Based on analysis and previous code comments, the implementation:
1. **Assumes** `shotRotation` is a deviation angle (relative to turret base or target)
2. **Normalizes** the angle to -180° to +180° range
3. **Clamps** to the configured angle limit (default: ±5°)
4. **Logs** when clamping occurs (if logging is enabled)

## Settings

### New Settings Added
- **`enableRotationClamping`** (bool): Master toggle for rotation clamping
- **`rotationClampAngle`** (float): Maximum deviation angle in degrees (0-45°)

### UI Controls
Located in the mod settings under "Rotation Clamping":
- Checkbox to enable/disable the feature
- Slider to adjust the clamp angle (±0.0° to ±45.0°)
- Tooltip: "Clamps lateral deviation (yaw) of turret projectiles"
- Shows current value: `±X.X°`

## Technical Details

### Patched Method
- **Target**: `CombatExtended.Verb_LaunchProjectileCE.ShiftTarget`
- **Type**: Postfix patch
- **When**: Called after CE calculates spread/sway but before projectile launch
- **What it modifies**: The `shotRotation` field

### Files Modified
1. **AMCSettings.cs**: Added rotation clamping settings fields and UI
2. **HarmonyPatches.cs**: 
   - Made `HarmonyPatches` a partial class
   - Added patch registration for `ShiftTarget`
3. **HarmonyPatches_RotationClamping.cs** (NEW): Contains the rotation clamping patch implementation
4. **AMCLogger.cs**: Already filters to turret-only logging

## Usage Instructions

### For Users
1. Open Mod Settings → Absolutely More Cannons
2. Scroll to "Rotation Clamping" section
3. Enable "Enable Rotation Clamping (Turrets Only)"
4. Adjust the slider to set maximum deviation angle
5. Play the game - turret projectiles will now be clamped to the specified angle

### For Developers/Debugging
1. Enable both:
   - "Enable Projectile Launch Logs (Vertical Angle Only)"
   - "Enable Rotation Clamping (Turrets Only)"
2. Fire a turret
3. Check the log for:
   - `[AMC DIAGNOSTIC]` entries showing reference frame analysis
   - `[AMC] Rotation clamped: X.XXX° → Y.YYY°` entries showing when clamping occurs
   - `[AMC] <Turret> at (X, Z) | Vertical Angle: X.XXX°` entries for each shot

### Determining Reference Frame
To confirm the reference frame of `shotRotation`:
1. Enable projectile launch logs
2. Fire several shots from different turrets at different target angles
3. Look at the diagnostic output interpretation
4. If most shots show "RELATIVE TO TURRET BASE", then shotRotation is a deviation angle
5. If most show "ABSOLUTE", then it's in map coordinates

## Expected Behavior

### When Enabled
- Turret projectiles will have their lateral deviation clamped
- Very inaccurate shots will be brought back to within ±clampAngle
- Vertical angle (shotAngle) remains unaffected
- Only affects turrets using `Verb_LaunchProjectileCE`

### When Disabled
- Combat Extended's normal spread/sway/accuracy mechanics apply
- No modification to shotRotation

## Notes

1. **Only affects turrets**: The patch checks `IsTurret(caster)` to ensure only turret weapons are affected
2. **Non-invasive**: If disabled, CE operates normally
3. **Diagnostic mode**: Enable logging to analyze shotRotation's reference frame before assuming clamping behavior
4. **Adjustable**: Clamp angle can be adjusted from 0° (perfect accuracy) to 45° (very wide cone)

## Recommended Settings

- **Realistic**: ±2-3° (similar to modern artillery CEP)
- **Balanced**: ±5° (default, reduces extreme outliers)
- **Permissive**: ±10-15° (still allows significant spread)
- **Debug/Testing**: ±0.5° (very tight grouping, for testing reference frame)
