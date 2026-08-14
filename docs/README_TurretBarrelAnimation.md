# Turret Barrel Animation System

This mod adds an optional turret barrel animation system to CombatExtended-compatible turrets. It allows turrets to have animated barrels that can recoil, rotate, and animate when firing, creating more dynamic and visually appealing turret behavior.

## Features

- **Optional System**: Turrets work normally without barrel animations - the system is completely optional
- **Multiple Animation Types**:
  - **Recoil Animation**: Barrels recoil backward when firing
  - **Spinning Animation**: Barrels animate through texture frames to show spinning (like miniguns)
  - **Firing Animation**: Scale and position changes during firing
- **CombatExtended Compatible**: Fully integrated with CE's turret system
- **Flexible Configuration**: Configure via XML mod extensions or component properties

## How It Works

The system consists of three main components:

1. **TurretBarrelExtension**: XML-configurable mod extension for turret building defs
2. **CompTurretBarrel**: Component that handles animation logic and drawing
3. **Harmony Patches**: Integration with CE's turret firing and drawing systems

## Installation

1. Add the turret barrel animation classes to your mod's source code
2. Compile the mod with references to:
   - Assembly-CSharp.dll
   - CombatExtended.dll
   - 0Harmony.dll
   - UnityEngine assemblies
3. Load the mod after CombatExtended in the mod load order

## Texture Setup for Spinning Barrels

For spinning barrel animations, create multiple texture frames showing different "spin states":

1. **Texture Naming**: Use a base name with frame numbers, e.g.:
   - `TurretBarrelSpin_0.png` (idle/resting position)
   - `TurretBarrelSpin_1.png` (slight spin)
   - `TurretBarrelSpin_2.png` (medium spin)
   - `TurretBarrelSpin_3.png` (full spin)

2. **Graphic_Collection Setup**: In your XML, reference the base name:
   ```xml
   <barrelGraphic>
     <texPath>Things/Building/TurretBarrelSpin</texPath>
     <graphicClass>Graphic_Collection</graphicClass>
     <drawSize>(1,1)</drawSize>
   </barrelGraphic>
   ```
   RimWorld will automatically load `TurretBarrelSpin_0`, `TurretBarrelSpin_1`, etc.

3. **Frame Count**: Use 2-4 frames for smooth animation. More frames = smoother but requires more textures.

## Usage

### Method 1: Using Mod Extensions (Recommended)

Add a `TurretBarrelExtension` to your turret's building def:

```xml
<ThingDef ParentName="TurretBuildingBase">
  <defName>MyTurret_WithAnimatedBarrel</defName>

  <modExtensions>
    <li Class="AbsolutelyMoreCannons.TurretBarrelExtension">
      <!-- Barrel graphic configuration -->
      <barrelGraphic>
        <texPath>Things/Building/MyBarrelTexture</texPath>
        <graphicClass>Graphic_Single</graphicClass>
        <drawSize>(1,1)</drawSize>
      </barrelGraphic>

      <!-- Position offset from turret center -->
      <barrelOffset>(0,0,0.5)</barrelOffset>

      <!-- Size multiplier -->
      <barrelDrawSize>1.0</barrelDrawSize>

      <!-- Recoil animation settings -->
      <recoilAnimation>
        <maxDistance>0.3</maxDistance>
        <durationTicks>15</durationTicks>
        <affectsRotation>true</affectsRotation>
        <maxAngle>3</maxAngle>
      </recoilAnimation>

      <!-- Spinning animation (texture-based, for spinning barrels) -->
      <spinningAnimation>
        <enabled>true</enabled>
        <baseSpeed>0.2</baseSpeed>
        <firingMultiplier>2</firingMultiplier>
        <acceleration>0.03</acceleration>
        <deceleration>0.95</deceleration>
        <maxSpeed>0.8</maxSpeed>
        <minSpeed>0</minSpeed>
        <spinWhenIdle>false</spinWhenIdle>
      </spinningAnimation>

      <!-- Firing animation (scale/position changes) -->
      <firingAnimation>
        <enabled>true</enabled>
        <durationTicks>5</durationTicks>
        <maxScale>1.1</maxScale>
        <maxPositionOffset>0.05</maxPositionOffset>
      </firingAnimation>

      <!-- Drawing settings -->
      <drawLayerOffset>0.01</drawLayerOffset>
      <drawWhenDestroyed>true</drawWhenDestroyed>
      <inheritTurretRotation>true</inheritTurretRotation>
    </li>
  </modExtensions>

  <!-- Add the component -->
  <comps>
    <li Class="AbsolutelyMoreCannons.CompProperties_TurretBarrel" />
  </comps>
</ThingDef>
```

### Method 2: Using Component Properties

Configure everything through the component properties instead of mod extensions:

```xml
<ThingDef ParentName="TurretBuildingBase">
  <defName>MyTurret_ComponentBased</defName>

  <comps>
    <li Class="AbsolutelyMoreCannons.CompProperties_TurretBarrel">
      <barrelGraphic>
        <texPath>Things/Building/MyBarrelTexture</texPath>
        <graphicClass>Graphic_Single</graphicClass>
        <drawSize>(1,1)</drawSize>
      </barrelGraphic>
      <barrelOffset>(0,0,0.5)</barrelOffset>
      <barrelDrawSize>1.0</barrelDrawSize>

      <!-- Animation configurations go here -->
      <recoilAnimation>...</recoilAnimation>
      <rotationAnimation>...</rotationAnimation>
      <firingAnimation>...</firingAnimation>
    </li>
  </comps>
</ThingDef>
```

## Configuration Options

### Graphic Settings

- **barrelGraphic**: The texture and graphic class for the barrel
- **barrelOffset**: Position offset from turret center (x, y, z)
- **barrelDrawSize**: Size multiplier for the barrel graphic
- **drawLayerOffset**: Z-layer offset for drawing order
- **drawWhenDestroyed**: Whether to draw barrel when turret is destroyed
- **inheritTurretRotation**: Whether barrel inherits turret's rotation

### Recoil Animation

- **maxDistance**: Maximum recoil distance (cells)
- **durationTicks**: How long recoil lasts (ticks)
- **affectsRotation**: Whether recoil affects barrel rotation
- **maxAngle**: Maximum recoil angle (degrees)
- **recoilCurve**: Animation curve for recoil over time (SimpleCurve)

### Spinning Animation

- **enabled**: Whether spinning animation is active (requires Graphic_Collection with multiple frames)
- **baseSpeed**: Base animation speed in frames per tick (e.g., 0.2 = 1 frame every 5 ticks)
- **firingMultiplier**: Speed multiplier when firing
- **acceleration**: How quickly animation speeds up
- **deceleration**: How quickly animation slows down (0-1, lower = faster deceleration)
- **maxSpeed**: Maximum animation speed in frames per tick
- **minSpeed**: Minimum animation speed (set to 0 to allow full stop)
- **spinWhenIdle**: Whether to spin continuously even when not firing

### Firing Animation

- **enabled**: Whether firing animation is active
- **durationTicks**: Animation duration (ticks)
- **maxScale**: Maximum scale multiplier
- **maxPositionOffset**: Maximum position offset
- **scaleCurve**: Scale animation curve (SimpleCurve)
- **positionOffsetCurve**: Position animation curve (SimpleCurve)

## Animation Curves

Animation curves use RimWorld's `SimpleCurve` format:

```xml
<recoilCurve>
  <li>
    <x>0.0</x>  <!-- Time (0-1) -->
    <y>0.0</y>  <!-- Value -->
  </li>
  <li>
    <x>0.3</x>
    <y>1.0</y>
  </li>
  <li>
    <x>1.0</x>
    <y>0.0</y>
  </li>
</recoilCurve>
```

## Examples

See `Common/Defs/TurretBarrelAnimation_Examples.xml` for complete examples of:
- Simple recoiling barrel
- Spinning barrel turret
- Heavy artillery with dramatic recoil
- Component-based configuration

## Technical Details

### Component Lifecycle

1. **Initialize**: Graphics and configuration loaded
2. **CompTick**: Animation state updated each tick
3. **PostDraw**: Barrel rendered with current animation state
4. **Trigger Events**: Recoil/firing animations triggered by Harmony patches

### Harmony Integration

The system patches CE's turret methods:
- `Building_TurretGunCE.TryCastShot`: Triggers firing animation
- `Building_TurretGunCE.BurstComplete`: Triggers recoil animation
- `Building_TurretGunCE.DrawAt`: Ensures proper drawing order

### Performance Considerations

- Animations only update when active
- Graphics are cached and reused
- Minimal impact on turrets without barrel components
- Uses efficient matrix operations for drawing

## Compatibility

- **CombatExtended**: Fully compatible and integrated
- **Vanilla Turrets**: Works with any turret that inherits from `Building_TurretGunCE`
- **Other Mods**: Should be compatible with most turret-related mods

## Troubleshooting

### Barrel Not Appearing
- Check that `barrelGraphic` path is correct
- Ensure texture exists in the Textures folder
- Verify `CompTurretBarrel` component is added

### Animation Not Working
- Check that animation settings are properly configured
- Ensure `enabled` flags are set to `true`
- Verify Harmony patches are loading (check debug logs)

### Performance Issues
- Reduce animation complexity for high-count turrets
- Use simpler curves for better performance
- Disable unused animation types

## Future Enhancements

Potential future features:
- Multiple barrel support
- Sound integration
- Particle effects
- Advanced interpolation modes
- Barrel overheating effects
