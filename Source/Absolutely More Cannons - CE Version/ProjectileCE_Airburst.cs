using System.Collections.Generic;
using CombatExtended;
using CombatExtended.Compatibility;
using CombatExtended.Utilities;
using ProjectileImpactFX;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace AbsolutelyMoreCannons
{
    /// <summary>
    /// Custom CE Projectile class that detonates in mid-air based on AirburstExtension settings.
    /// Supports Altitude-based detonation (for indirect fire) and Flak proximity detonation (for direct fire).
    /// Dynamically aligns fragment pitch cone with the shell's trajectory velocity vector.
    /// </summary>
    public class ProjectileCE_Airburst : ProjectileCE_Explosive
    {
        private AirburstExtension ext;
        private bool fuseTriggered = false;

        public override void PostMake()
        {
            base.PostMake();
            ext = def.GetModExtension<AirburstExtension>();
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref fuseTriggered, "fuseTriggered", false);
        }

        public override void Tick()
        {
            if (ext == null)
            {
                ext = def.GetModExtension<AirburstExtension>();
            }

            // Airburst check BEFORE calling base.Tick() to prevent CE from running ground-collision impact logic first
            if (!landed && !fuseTriggered && ext != null && FlightTicks >= ext.armingTicks)
            {
                Vector3 currentPos = ExactPosition;
                Vector3 currentVelocity = this.velocity;
                Vector3 predictedNextPos = currentPos + currentVelocity;

                bool shouldDetonate = false;

                // 1. Evaluate Airburst Trigger Mode
                if (ext.type == AirburstType.Altitude)
                {
                    // Altitude mode: Detonate when shell is descending and height crosses/drops to or below burstAltitude
                    float minHeight = Mathf.Min(currentPos.y, predictedNextPos.y);
                    if (minHeight <= ext.burstAltitude && (currentVelocity.y <= 0f || FlightTicks > 20))
                    {
                        shouldDetonate = true;
                    }
                }
                else if (ext.type == AirburstType.Flak)
                {
                    // Flak (Proximity) mode: Check intended target first (O(1))
                    if (intendedTargetThing != null && intendedTargetThing.Spawned)
                    {
                        float distSq = (intendedTargetThing.Position - Position).LengthHorizontalSquared;
                        if (distSq <= ext.proximityRadius * ext.proximityRadius)
                        {
                            shouldDetonate = true;
                        }
                    }

                    // If intended target wasn't close enough, check for hostile pawns in range
                    if (!shouldDetonate && Map != null)
                    {
                        IEnumerable<Pawn> nearbyPawns = Position.PawnsInRange(Map, ext.proximityRadius);
                        if (nearbyPawns != null)
                        {
                            foreach (Pawn p in nearbyPawns)
                            {
                                if (p != null && p.Spawned && !p.Dead && p != launcher && (launcher == null || GenHostility.HostileTo(p, launcher)))
                                {
                                    shouldDetonate = true;
                                    break;
                                }
                            }
                        }
                    }
                }

                // 2. Trigger mid-air detonation
                if (shouldDetonate)
                {
                    fuseTriggered = true;
                    Impact(null);
                    return; // Skip base.Tick() so CE doesn't move shell into ground collision
                }
            }

            base.Tick();
        }

        public override void Impact(Thing hitThing)
        {
            if (fuseTriggered)
            {
                DoAirburstImpact(hitThing);
            }
            else
            {
                base.Impact(hitThing);
            }
        }

        private void DoAirburstImpact(Thing hitThing)
        {
            if (!Position.IsValid || Map == null)
            {
                Destroy();
                return;
            }

            // 1. Clamor & Flecks
            GenClamor.DoClamor(this, 12f, ClamorDefOf.Impact);
            if (Controller.settings.EnableExtraEffects)
            {
                ImpactFleckThrower.ThrowFleck(ExactPosition, Position, Map, def.projectile as ProjectilePropertiesCE, def, hitThing, shotRotation);
            }

            Vector3 explodePos = ExactPosition;

            // 2. Visual Effects & Sounds
            if (def.projectile.explosionEffect != null)
            {
                Effecter effecter = def.projectile.explosionEffect.Spawn();
                effecter.Trigger(new TargetInfo(explodePos.ToIntVec3(), Map, false), new TargetInfo(explodePos.ToIntVec3(), Map, false));
                effecter.Cleanup();
            }

            ProjectilePropertiesCE projectileCE = def.projectile as ProjectilePropertiesCE;
            if (projectileCE != null)
            {
                float effectScale = projectileCE.detonateEffectsScaleOverride > 0 ? projectileCE.detonateEffectsScaleOverride : projectileCE.explosionRadius * 2;
                if (projectileCE.detonateMoteDef != null)
                {
                    MoteMaker.MakeStaticMote(DrawPos, Map, CE_ThingDefOf.Mote_BigExplode, effectScale);
                }
                if (projectileCE.detonateFleckDef != null)
                {
                    FleckCreationData dataStatic = FleckMaker.GetDataStatic(DrawPos, MapHeld, projectileCE.detonateFleckDef, effectScale);
                    MapHeld.flecks.CreateFleck(dataStatic);
                }
            }

            if (def.projectile.soundExplode != null)
            {
                def.projectile.soundExplode.PlayOneShot(new TargetInfo(explodePos.ToIntVec3(), Map, false));
            }

            // 3. Throw Airburst Fragments (Custom CompAirburstFragments or CE CompFragments fallback)
            ThrowAirburstFragments(explodePos);

            // 4. Explosive damage if radius > 0
            if (projectileCE != null && projectileCE.explosionRadius > 0f && DamageDef != null)
            {
                GenExplosionCE.DoExplosion(
                    explodePos.ToIntVec3(),
                    Map,
                    projectileCE.explosionRadius,
                    DamageDef,
                    launcher,
                    Mathf.FloorToInt(DamageAmount),
                    projectileCE.GetExplosionArmorPenetration(),
                    def.projectile.soundExplode
                );
            }

            Destroy();
        }

        private void ThrowAirburstFragments(Vector3 pos)
        {
            Vector3 flightVec = ExactPosition - LastPos;

            // 1. Try Custom AMC CompAirburstFragments (Trajectory-aligned 3D Cone)
            var airburstFragComp = this.TryGetComp<CompAirburstFragments>();
            if (airburstFragComp != null)
            {
                airburstFragComp.Throw(pos, Map, launcher, flightVec, shotRotation);
                return;
            }

            // 2. Fallback to standard CE CompFragments
            var ceFragComp = this.TryGetComp<CompFragments>();
            if (ceFragComp != null)
            {
                ceFragComp.Throw(pos, Map, launcher);
            }
        }
    }
}
