# Open-top turret seat & gun shield — design

Date: 2026-11-27
Status: draft, awaiting approval

## Intent

`CompEnclosedTurret` currently models a turret the gunner is fully inside. The pawn is
hidden or pinned to the turret centre, and protection is a flat per-damage-type percentage
from every direction.

Open-mount guns don't work that way. These include all-round-shielded open tops (DCA 40,
AEC Deacon, M61 Vulcan) and front-shield-only guns (Flak 38, Flak M42, leIG 18, sIG 33,
BL Mk1, …). Their gunner sits on or behind the mount, turns with it, and is protected
only by a gun shield up to a certain height.

This change extends the comp so that:

1. The gunner is at a configurable **seat** position on the turret top that turns with the
   turret. This covers being drawn there, aimed at there, hit there, and caught by blasts
   there.
2. A **gun shield** with an arc, a height and an armour value physically intercepts CE
   projectiles headed for the gunner.
   - Intercepted rounds hit the turret.
   - Rounds that penetrate the shield go on to damage the gunner.
   - Rounds that fail to penetrate throw sparks and smoke.

## Goals

- Seat position set in XML, relative to the turret top, rotating with it.
  - The pawn is drawn there and faces the barrel.
  - The pawn is targeted, hit and caught in blasts there.
- The gunner's vertical hitbox is tied to the gun's `barrelHeight`, not to the cell it
  logically stands in.
- A shield defined by:
  - arc: degrees, centred on the barrel; 360 means all round;
  - height: absolute, above ground;
  - sharp armour: CE mm RHA.
- Behaviour when the shield intercepts a round (as described by the user):
  1. The round impacts the **turret** through CE's normal impact, so the turret takes CE
     damage. AP does less structural damage than HE, and HE detonates at the shield.
  2. Penetration check against `shieldArmor`:
     - **Penetrates:** the gunner takes the damage directly, with no second trajectory or
       collision check.
     - **Doesn't penetrate:** spark and smoke at the impact point, and no damage to the
       gunner.
- Rounds outside the arc, above the shield height, or plunging from above hit the gunner
  normally.
- TurretEditor:
  - The new fields are editable in the normal turret editor (field map).
  - The Texture Inspector shows a seat marker and a shield wedge that follow the preview's
    rotation slider.
- Content: the turrets listed under §7 are configured with the new fields.

## Non-goals

- Dragging the seat or shield in the Texture Inspector. It's a preview only; editing
  happens through the normal field editor.
- An in-game dev gizmo for nudging the seat.
- Multiple crew seats, or a separate shield graphic layer.
- Shields facing anywhere other than centred on the barrel, and multi-section shields
  (e.g. a thicker front plate with thinner sides).
- Unmanned (`U…`) turret variants. They have no gunner.

## Anti-goals (failure even if it "works")

- **Any behaviour change for existing fully enclosed turrets** (BMP-2, Marder, etc.).
  Every new field defaults to "off", and a turret that doesn't set them behaves exactly as
  today.
- **Visual and combat mismatch:** the gunner is drawn on the seat but is aimed at, hit or
  blasted somewhere else, such as the interaction cell.
- **Double-dipping:** a round already handled by the shield is reduced again by the flat
  `bulletProtection` or `explosiveProtection`.

## Constraints and findings

- **Aim point vs hitbox.**
  - CE's hitbox (`CE_Utility.GetBoundsFor`) reads `thing.DrawPos`.
  - CE's aim point (`Verb_LaunchProjectileCE`, around line 1357) reads
    `GenThing.TrueCenter()`, which for a pawn returns `pawn.Drawer.DrawPos`
    (`Pawn_DrawTracker`). That bypasses our current `Pawn.DrawPos` prefix.
  - The seat override must therefore sit on `Pawn_DrawTracker.DrawPos`, so that drawing,
    labels, aim and hitbox all agree.
- **Collision candidates.**
  - `ProjectileCE.CheckCellForCollision` only collects pawns in the cell being checked and
    the cells next to it along the shot.
  - The gunner's logical `Position` stays at the interaction cell, which on the DCA 40
    (0,0,-2) is outside the 4×4 footprint.
  - Shots crossing the footprint may therefore never test the gunner, so this needs a fix
    (§2).
- **Vertical bounds.**
  - `CollisionVertical` stacks a pawn's box on top of the cover in its logical cell. The
    Flak 38 interaction cell is inside the footprint, so its box starts at the turret's
    0.7. The DCA 40's cell is outside, so its box starts at ground level.
  - The seated gunner's vertical range must be overridden (§2).
  - Turret hulls are `fillPercent` 0.7 (`TurretBuilding_Base.xml`), giving a CE height
    range of 0–0.7.
- **`barrelHeight`** lives on `TurretBarrelExtension` (default −1 = unset) and is resolved
  by `CompTurretBarrel.GetCustomBarrelHeight(caster)`. Current values:
  - DCA 40: 1.2
  - Deacon: 1.45
  - Vulcan: 2
  - Flak 38: 1.1
  - Flak M42: 1.7
  - leIG 18: 1.0
  - sIG 33: 1.2
  - BL Mk1: 1.85
- **CE extension points** (`CombatExtended.Compatibility.BlockerRegistry`, present in
  workshop dll `2890901044`):
  - `RegisterBeforeCollideWithCallback(Func<ProjectileCE, Thing, bool>)` fires inside
    `TryCollideWith` after CE has decided the projectile hits `thing`, but before
    `ExactPosition` is set and before `Impact`. Returning `true` means "handled". CE does
    nothing further, so the callback must set the position, set `landed` and call `Impact`
    itself.
  - `RegisterCheckForCollisionCallback(Func<ProjectileCE, IntVec3, Thing, bool>)` fires at
    the start of each cell check.
- **Public members on `ProjectileCE`:**
  - `Impact(Thing)` (virtual), `DamageInfo` (includes armour penetration), `ShotLine`
    (Ray), `launcher`, `landed`, and `ExactPosition` (public setter).
  - `CanCollideWith` is protected. Compute the impact point ourselves with
    `GetBoundsFor(gunner).IntersectRay(ShotLine, out dist)` and `ShotLine.GetPoint(dist)`.
- **CE fragments** are separate projectiles with the original shooter as `launcher`. Their
  damage def names contain "Fragment", which `GetProtectionFor` classes as explosive.
- **Explosions** affect pawns by the pawn's logical `Position` cell.
- **References:**
  - CE source: `~/Documents/CombatExtended/Source`.
  - The mod builds against the workshop CE dll.
- **Offset convention:** use the same turret-top-local frame as existing offsets
  (`muzzleOffset`, smoke offsets).
  - +Z points along the barrel and +X to the barrel's right.
  - The frame is rotated by the turret top angle, clockwise from North.
  - The Texture Inspector's `rotateVecXZ` already implements this.

## Approaches considered

1. **CE `BlockerRegistry` collision hook (chosen).**
   - Shield logic runs at the moment CE decides a round hits the gunner, with the exact
     shot ray.
   - Redirecting into `projectile.Impact(turret)` reuses CE's own damage, explosion and
     logging.
   - It's CE's supported extension point; the SOS2 compat shields use it too.
2. **Harmony patch on `ProjectileCE.TryCollideWith`.**
   - Same control as option 1 but no extra capability.
   - Breaks more easily when CE updates.
3. **Damage-time filter in our existing `Pawn.PreApplyDamage` patch.**
   - The least code.
   - Bullets still visibly hit the gunner, the turret takes nothing, and HE doesn't burst
     at the shield. This fails the stated behaviour.

The user chose option 1.

## Design

### 1. New XML fields (`CompProperties_EnclosedTurret`)

| Field | Type | Default | Meaning |
|---|---|---|---|
| `seatOffset` | `Vector3?` (x,0,z) | unset | Seat position in cells, turret-top-local frame. Unset means today's behaviour (`centerPawnPosition`); none of §2–§5 applies. |
| `shieldArc` | float (deg) | 0 | Total angular width of the shield, centred on the barrel. 0 means no shield; 360 means all round. |
| `shieldHeight` | float | 0 | Top edge of the shield, in CE height units above ground (same units as `barrelHeight` and `fillPercent`; ≈ metres). |
| `shieldArmor` | float (mm RHA) | 0 | Sharp armour of the shield, compared with the round's sharp penetration. |

The shield fields only take effect when `seatOffset` is set. `seatOffset` requires
`hidePawnGraphics=false`; if both are set, log a config warning once and keep the pawn
hidden.

### 2. Seat: position, aim, hitbox, collision reachability, blasts

- **Position.**
  - Patch `Pawn_DrawTracker.DrawPos`. For a seated gunner, return
    `turret.DrawPos + rotate(seatOffset, turretTopAngle)`.
  - Use the same stable turret-top angle the barrel code reads (`CompTurretBarrel`'s
    `curRotationInt` lookup), so the pawn doesn't jitter mid-burst.
  - Set Y so the pawn draws above the turret top and below the barrel.
  - The existing `Pawn.DrawPos` prefix keeps its current role for turrets without
    `seatOffset`. With a seat, it defers to the draw tracker.
  - Because `GenThing.TrueCenter` reads the draw tracker, CE aims at the seat
    automatically.
- **Facing:** the gunner's `Rotation` is `Rot4.FromAngleFlat(turretTopAngle)`, re-applied
  while seated.
- **Vertical hitbox (user's direction):**
  - The gunner's head (top of box) is at the gun's barrel level:
    `top = GetCustomBarrelHeight(turret)`, falling back to **1.45** when unset.
  - The box keeps the pawn's normal CE collision height:
    `bottom = max(0, top − pawnCollisionHeight)`.
  - Override `CollisionVertical` for seated gunners so it ignores the cover in their
    logical cell.
  - CE's crouch logic doesn't apply while seated.
- **Collision reachability.**
  - Register a `CheckForCollision` cell callback. When the cell is inside the footprint of
    a turret whose seated gunner has `seatOffset`, test the gunner's bounds against
    `ShotLine` within this tick's segment, using the same rule as CE's `CanCollideWith`.
  - **The hull comes first:** if the turret building's own bounds are hit nearer along
    the ray, the callback does nothing and CE resolves the hull hit normally. This is how
    low shots hit the turret before they reach the shield or the gunner.
  - On a gunner hit, run §3, which falls through to a normal `Impact(gunner)` when the
    shield doesn't cover it.
  - Skip when `projectile.launcher` is the gunner or the turret.
  - Cache "turrets with seated gunners" per map so the callback stays cheap; it runs for
    every projectile cell.
- **Blasts are measured from the seat cell.** Patch explosion cell handling (vanilla
  `DamageWorker.ExplosionAffectCell` or CE's equivalent; the builder confirms which one CE
  explosions use) so that for a seated gunner:
  - an explosion covering the **seat cell** affects the gunner;
  - an explosion covering only his logical `Position` cell does not.

### 3. Shield (`BeforeCollideWith` callback)

When `thing` is a seated gunner whose turret has `shieldArc > 0`, and the launcher is not
the gunner or the turret:

1. Find the impact point `p` on the gunner's bounds along `ShotLine`.
2. **Covered horizontally** if `shieldArc ≥ 360`, or the angle between the reversed shot
   direction (horizontal) and the turret-top facing is ≤ `shieldArc / 2`.
3. **Covered vertically** if `p.y ≤ shieldHeight`.
   - Plunging fire enters through the top of the box, above the shield, so it fails this
     check naturally.
4. If it's not covered on both, return `false` and CE hits the gunner normally.
5. If it's covered:
   1. `dinfo = projectile.DamageInfo`, captured before impact.
   2. Set `ExactPosition = p`, set `landed = true`, and call `projectile.Impact(turret)`.
      - CE applies turret damage, explosions, secondary damage and logging.
      - Return `true`.
   3. **Exploding projectiles** (explosion radius > 0 or `CompExplosiveCE`): no
      penetration step. The blast at the shield reaches the gunner through §2's seat-cell
      rule.
   4. **Otherwise** (bullets and fragments), compare `dinfo.ArmorPenetrationInt` with
      `shieldArmor`:
      - **Greater:** apply a new `DamageInfo` to the gunner.
        - Same def, amount, angle, instigator and weapon.
        - Armour penetration = pen − `shieldArmor`.
        - Body region from the gunner's `CollisionVertical.GetCollisionBodyHeight(p.y)`.
        - Call `TakeDamage` directly. The gunner's own armour applies normally.
      - **Not greater:** spark fleck plus a small smoke puff at `p`.

### 4. Interaction with existing protection (`Prefix_Pawn_PreApplyDamage`)

When the turret has `shieldArc > 0`:

- **Projectile hits on the gunner skip the flat percentages.** This covers bullets,
  fragments, and the penetrating hit from §3. They skip `bulletProtection` and
  `explosiveProtection` alike, because the shield is that protection.
- **Blast damage** (from an explosion, not a projectile impact) still uses
  `explosiveProtection`.
- Flame, melee, temperature, custom and general protection work as today.
- How to tell projectile hits from blast damage is the builder's choice, for example a
  short-lived flag set around the projectile impact paths.

### 5. Suppression

- **Today:** every enclosed gunner is fully immune to suppression.
- **With a shield configured:** keep that immunity only for suppression coming from inside
  the shield arc (CE's `RegisterUnsuppresableFromCallback`, or our existing patch made
  directional). Suppression from outside the arc applies normally.
- 360° shields stay fully immune.

### 6. TurretEditor

- **`src/xml/extract.js`:** add defaults to `comps.enclosed`: `seatOffset: null`,
  `shieldArc: 0`, `shieldHeight: 0`, `shieldArmor: 0`.
- **`src/xml/fieldMap.js`:** four new fields, gated on `comps.enclosed.enabled`, following
  the existing enclosed fields. `seatOffset` uses the same vec3 string type as other offset
  fields.
- **`ui/src/components/TextureInspector.jsx`:** two new layers, on by default when the comp
  has the values:
  - **Seat:** a pawn-sized circle (about 1 tile) labelled "Seat" at the turret centre plus
    `rotateVecXZ(seatOffset, rotation)`.
  - **Shield:** a translucent wedge (or ring, for 360°) around the seat spanning
    ±`shieldArc/2` from the barrel direction, about 1 tile radius.
    - Labelled with shield height, gunner top and armour, e.g. `shield 1.0 / head 1.2 · 8mm`.
    - The label lets you see at a glance how much of the gunner the shield covers.
- **MCP / OpenAPI / export:** pick the fields up from the field map where those layers are
  generated from it. The builder checks `projections.js` and `exportService.js` for
  hand-listed enclosed fields.
- **Tests:** extend `test/mcp-building-comps.test.js` (or its sibling) to round-trip the new
  fields.

### 7. Content

Starting values are placeholders for the user to tune in game.

**All-round shield (`shieldArc` 360):**
- DCA 40 (has the comp)
- AEC Deacon (has the comp)
- M61 Vulcan (add the comp)

**Front shield only** (`shieldArc` around 90–120, by the shield's visual width):
- Flak 38
- Flak M42
- leIG 18
- sIG 33
- BL Mk1

All need the comp added, with `hidePawnGraphics=false`.

For every one of them:
- Remove or ignore `bulletProtection`; it no longer applies to projectiles once a shield
  is set.
- Set `flameProtection` and `temperatureProtection` to sensible open-top values. The
  defaults of 1.0 mean a fireproof, climate-controlled gunner.

**Docs:** update `Common/Defs/ThingDefs_Buildings/Turret_XML_Documentation.md` and
`docs/xml_reference_guide.md`.

## Verification

- C# build succeeds.
- **In game (at least DCA 40 and Flak 38):**
  - The gunner is drawn on the seat, turns with the turret through 360°, and enemy shots
    fly at the seat.
  - Front-shield gun: rifle fire from the front gives sparks and no gunner damage, while
    rifle fire from behind hits the gunner.
  - 360° gun: rifle fire from any side gives sparks; fire from above the shield height
    (elevated or plunging) hits.
  - Low rounds hit the hull rather than the gunner.
  - An AP round above `shieldArmor` damages both the turret and the gunner.
  - An HE round detonates at the shield and the blast is judged from the seat cell.
  - A fully enclosed turret (e.g. the BMP-2) behaves exactly as before.
- TurretEditor test suite passes. The inspector shows the seat and wedge rotating with the
  slider.

## Calls made (for the user to check)

- **`barrelHeight` sets the top of the gunner's hitbox** (head at barrel level). The box
  extends down by the pawn's normal body height, clamped at the ground.
  - On low guns like the leIG 18 (1.0), the gunner effectively crouches behind the
    shield.
- **`shieldHeight` is absolute** (above ground, same units as `barrelHeight`), not a
  fraction of the gunner. Now that the gunner's height is tied to the gun, absolute is
  easier to reason about.
- **Penetration:** `shieldArmor` is compared with sharp penetration only. Rounds with
  blunt-only damage never penetrate.
- **Exploding rounds** skip the penetration step and rely on the blast, judged from the
  seat cell.
- **Projectile hits** on a shielded gunner skip the flat `bulletProtection` and
  `explosiveProtection`; blast damage still uses `explosiveProtection`.
- **Suppression immunity** becomes directional for shielded guns: immune only to fire
  from inside the arc.
- **Draw order:** the pawn draws above the turret top and below the barrel.
- **The hull is checked before the gunner** along a shot's path.

## Left to the builder

- The exact hook for keeping the pawn's facing.
- Fleck/effect defs for spark and smoke.
- Caching strategy for the cell callback.
- Which explosion method to patch.
- The mechanism for telling projectile hits from blast damage.
- Whether the MCP/export layers need hand edits.

## Addendum (2026-11-27): `barrelHeight` is the barrel pivot

- **Meaning:** `TurretBarrelExtension.barrelHeight` is the height of the barrel **pivot** (trunnion). It is the same for a gun's direct- and indirect-fire variants.
- **Where shells spawn:** at `barrelHeight` vertically, `projectileSpawnOffset` forward horizontally. The spawn point is not rotated by barrel pitch.
- **Firing solution:** CE computes it (pitch and distance, via the existing `ShiftTarget` postfix in `HarmonyPatches_TargetTracking.cs`) from that spawn point.
- **Data fix:** the 15 indirect variants that had a hand-raised `barrelHeight` were reset to their direct gun's value. The 2A3 and Karl Gerät have no direct variant and were left as they were.
- **`gunnerHeight` removed:** the optional override was only needed because of the raised indirect values. The gunner's head height is `barrelHeight`, else 1.45.
- **Verification:** `DevTools/firing_accuracy.mjs` measures along-track bias in game.
