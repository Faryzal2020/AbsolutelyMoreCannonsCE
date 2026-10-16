# Howitzer smoke / barrel / animation placeholders (2026-10-16)

Every howitzer base def (direct and indirect) now carries `CompProperties_TurretBarrel`, `CompProperties_TurretSmoker` and a `TurretBarrelExtension` with `recoilAnimation` and `firingAnimation`. Values were copied verbatim, not tuned. Each inserted block is tagged in the XML with `<!-- PLACEHOLDER: copied unadjusted, needs visual tuning (values from ...) -->`, so `grep PLACEHOLDER Common/Defs/ThingDefs_Buildings/Howitzers/*.xml` lists them.

Game loads to the main menu with zero errors (RimWorld 1.7.4980, CE, Muzzle Flash active).

## What was added where

| Turret | Barrel comp | Smoker | Barrel extension | Barrel textures |
|---|---|---|---|---|
| 2S1 Gvozdika | added | added | added | existing `_Barrel`, `_Barrel_Indirect`, `_UnderBarrel_Indirect` (artist-made, top already has barrel cut out) |
| 155mm GCT | added | added | added | existing `_Barrel`, `_Barrel_Indirect` (artist-made) |
| 7.5 cm le.IG 18 | added | added | added | existing `_Barrel`, `_Barrel_Indirect` (artist-made) |
| 2S19 Msta | already had | added | already had | existing |
| 15 cm sIG 33 | added | added | added | **placeholder copies** of `_Top` / `_Top_Indirect` |
| 2S3 Akatsiya | added | added | added | **placeholder copies** of `_Top` / `_Top_Indirect` |
| 9.2in BL Mk.1 | added | added | added | **placeholder copies** of `_Top` / `_Top_Indirect` |
| Karl-Gerät 040 | added | added | added | **placeholder copies** of `_Top` / `_Top_Indirect` |
| 2A3 Kondensator (indirect only) | added | added | added | **placeholder copy** of `_Top_Indirect` |

Placeholder-copy turrets draw the full top sprite twice (top layer plus barrel layer), so the whole gun visually recoils. The artist needs to cut the barrel out of the `_Top` texture and leave only the tube in `_Barrel`, as was done for the Gvozdika, GCT, le.IG 18 and Msta.

## Parameters that are still placeholder

Source of copied values: barrel extension from `152mmMsta.xml`, smoker from `75mmPak40.xml`.

### TurretBarrelExtension (per base def)

| Parameter | Current value | Why it needs a human |
|---|---|---|
| `barrelGraphic/drawSize` | (3,3) | copied; irrelevant while `barrelDrawSize` overrides scale, but check |
| `barrelOffset` | (0,0,0.0) | trunnion pivot; rotate 360° in dev mode and fix wobble (doc §7.2) |
| `barrelDrawSize` | set to each turret's `turretTopDrawSize` (13 / 11 / 14 / 18 / 10 / 28 / 12 / 7) | only correct if the barrel texture canvas matches the top canvas; true for the copies, verify for the artist-made ones |
| `underBarrelGraphic` | only on Gvozdika indirect | other elevated mounts may expose a gap under the breech (doc §7.9) |
| `recoilAnimation/maxDistance` | 2 (direct) / 1 (indirect) | Msta is a 22-tile sprite; on a 7-tile le.IG 18 this will clip through the carriage (doc §7.8) |
| `recoilAnimation/recoilDuration` / `returnDuration` | 30 / 100 ticks | fine for a 152mm; too slow for the 75mm, too fast for the 600mm |
| `firingAnimation/durationTicks` | 10 | copied |
| `firingAnimation/flashSize` | 30 (direct) / 35 (indirect) | sized for the Msta; scale with caliber and sprite |
| `firingAnimation/flashBrightness` | 3.0 | copied |
| `firingAnimation/flashOffset` | -1 (direct) / -4 (indirect) | must sit on the muzzle; foreshortened indirect barrels differ per sprite (doc §7.4) |
| `firingAnimation/flashIntensityCurve` | Msta curve | copied |
| `projectileSpawnOffset` | **not set** | shell spawns at default; doc §7.3 says direct vs indirect need different values |

### CompProperties_TurretSmoker (per base def, same values in both modes)

| Parameter | Current value | Why it needs a human |
|---|---|---|
| `muzzleOffset` | (0,0,3) | Pak 40 muzzle; must be moved to each gun's muzzle, and shortened in indirect mode (doc §7.5) |
| `muzzleParticleCount` / `muzzleParticleSize` | 2 / 1.5 | 75mm-sized puff on a 600mm gun |
| `muzzleVelocity` / `muzzleVelDuration` / `muzzleSpawnDuration` / `muzzleSpawnDelay` | 15 / 30 / 5 / 1 | copied |
| `heatOffset` | (0,0,1.5) | should start at the mantlet exit of each sprite (doc §7.6) |
| `heatEmissionPoints` / `heatEmissionSpacing` | 3 / 0.5 | count exposed barrel tiles per sprite (doc §7.6) |
| `heatThreshold` / `heatDecayRate` / `decayIncrease` | 4 / 0.9 / 0.12 | Pak 40 fires 9/min; a 1.5/min Karl will never reach threshold 4 |
| `heatEmissionRate` / `emissionIncrease` / `heatParticleSize` | 2 / 0.4 / 0.6 | copied |
| `shockwaveRadius` | 4 | doc §7.7: scale with caliber (3 for 76mm, 5 for 152mm, larger for 233–600mm) |
| `shockwaveOffset` | (0,0,4) | should equal the muzzle position |
| `shockwaveDensity` / `shockwaveParticleSize` | 40 / 1.5 | copied |
| `directionCone`, `shockwaveFadeOutSpeed`, gradient fields | not set (defaults) | optional |

### Not touched

- `turretTopDrawSize`, `interactionCellOffset`, `hidePawnGraphics`, `CompProperties_TurretViewTransfer` (doc §7.1, §7.10–12) were left as they were.
- Weapon defs were not changed; the barrel system only lives on the building defs.
