using CombatExtended;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace AbsolutelyMoreCannons
{
    /// <summary>
    /// CE BlockerRegistry callbacks for open-top turrets: make projectiles reach a seated gunner anywhere over the
    /// turret footprint (his logical cell is the interaction spot), and run the gun-shield interception.
    /// </summary>
    public static class OpenTopShield
    {
        /// <summary>
        /// CheckForCollision callback (start of every projectile cell check). When the cell is inside a seat turret's
        /// footprint, test the seated gunner's bounds against this tick's flight segment. The hull wins when it is
        /// hit nearer along the ray.
        /// </summary>
        public static bool CheckCell(ProjectileCE p, IntVec3 cell, Thing launcher)
        {
            Map map = p?.Map;
            if (map == null) return false;
            var turrets = OpenTopSeatUtility.SeatedTurrets(map);
            if (turrets.Count == 0) return false;

            for (int i = 0; i < turrets.Count; i++)
            {
                CompEnclosedTurret comp = turrets[i];
                Building turret = comp.Turret;
                if (turret == null || !turret.OccupiedRect().Contains(cell)) continue;
                Pawn gunner = OpenTopSeatUtility.SeatedGunner(comp);
                if (gunner == null || launcher == gunner || launcher == turret) continue;

                Bounds bounds = CE_Utility.GetBoundsFor(gunner);
                Ray ray = p.ShotLine;
                if (!bounds.IntersectRay(ray, out float dist)) continue;
                bool inSegment = bounds.Contains(p.LastPos) || bounds.Contains(p.ExactPosition)
                                 || dist * dist <= (p.ExactPosition - p.LastPos).sqrMagnitude;
                if (!inSegment) continue;

                // Hull first: low shots hit the turret before they reach the gunner.
                if (TryHullFirst(p, turret, dist)) return true;

                return ResolveHit(p, gunner, turret, comp, dist, fromCellCheck: true);
            }
            return false;
        }

        /// <summary>BeforeCollideWith callback: CE has decided the projectile hits <paramref name="thing"/>.</summary>
        public static bool BeforeCollideWith(ProjectileCE p, Thing thing)
        {
            if (!(thing is Pawn gunner) || p == null) return false;
            if (!OpenTopSeatUtility.TryGetSeated(gunner, out Building turret, out CompEnclosedTurret comp)) return false;
            if (p.launcher == gunner || p.launcher == turret) return false;
            if (!CE_Utility.GetBoundsFor(gunner).IntersectRay(p.ShotLine, out float dist)) return false;
            // CE sorts candidates by distance to the thing's centre, not along the ray, so it can pick the seated
            // gunner before the hull it must pass through first.
            if (TryHullFirst(p, turret, dist)) return true;
            if (!comp.Props.HasShield) return false;
            return ResolveHit(p, gunner, turret, comp, dist, fromCellCheck: false);
        }

        /// <summary>
        /// If the turret hull lies on the ray before the gunner, the round hits the hull (normal CE impact on the turret).
        /// </summary>
        private static bool TryHullFirst(ProjectileCE p, Building turret, float gunnerDist)
        {
            if (!CE_Utility.GetBoundsFor(turret).IntersectRay(p.ShotLine, out float hullDist) || hullDist >= gunnerDist) return false;
            p.ExactPosition = p.ShotLine.GetPoint(hullDist);
            p.landed = true;
            p.Impact(turret);
            return true;
        }

        /// <summary>
        /// Resolves a projectile reaching a seated gunner. Returns true when handled (projectile impacted).
        /// </summary>
        public static bool ResolveHit(ProjectileCE p, Pawn gunner, Building turret, CompEnclosedTurret comp, float dist, bool fromCellCheck)
        {
            Ray ray = p.ShotLine;
            Vector3 point = ray.GetPoint(dist);
            CompProperties_EnclosedTurret props = comp.Props;

            bool covered = props.HasShield
                           && OpenTopGeometry.InShieldArc(ray.direction.x, ray.direction.z, OpenTopSeatUtility.TopAngle(turret), props.shieldArc)
                           && point.y <= props.shieldHeight;

            if (!covered)
            {
                if (!fromCellCheck) return false; // CE completes its normal hit on the gunner
                p.ExactPosition = point;
                p.landed = true;
                p.Impact(gunner);
                return true;
            }

            // The shield is in the way: the round strikes the turret.
            Map map = p.Map;
            DamageInfo dinfo = p.DamageInfo;
            bool explosive = IsExplosive(p);
            string projName = p.def?.defName ?? "?";
            BodyPartHeight partHeight = new CollisionVertical(gunner).GetCollisionBodyHeight(point.y);

            p.ExactPosition = point;
            p.landed = true;
            p.Impact(turret);

            if (explosive)
            {
                DevLog($"shield stop {projName} gunner={gunner.thingIDNumber} (explosive; blast resolves from the seat cell)");
                return true;
            }

            float rem = OpenTopGeometry.RemainingPenetration(dinfo.ArmorPenetrationInt, props.shieldArmor);
            // ArmorPenetrationInt is CE's *blunt* penetration for non-sharp damage; blunt-only rounds never get through.
            bool sharp = dinfo.Def?.armorCategory == DamageArmorCategoryDefOf.Sharp;
            if (OpenTopGeometry.PenetratesShield(sharp, dinfo.ArmorPenetrationInt, props.shieldArmor) && !gunner.Dead && gunner.Spawned)
            {
                // Copy the projectile's own DamageInfo (no ctor call: the mod compiles against the 1.5 reference
                // but runs on 1.7) and lower its penetration by the shield's armour.
                DamageInfo through = dinfo;
                DamageInfoPenetration(ref through) = rem;
                through.SetBodyRegion(partHeight, BodyPartDepth.Outside);
                gunner.TakeDamage(through);
                DevLog($"shield pen {projName} gunner={gunner.thingIDNumber} pen={dinfo.ArmorPenetrationInt:F1} armor={props.shieldArmor:F1} rem={rem:F1}");
            }
            else
            {
                if (map != null)
                {
                    FleckMaker.ThrowMicroSparks(point, map);
                    FleckMaker.ThrowMicroSparks(point, map);
                    FleckMaker.ThrowSmoke(point, map, 0.4f);
                }
                DevLog($"shield stop {projName} gunner={gunner.thingIDNumber} pen={dinfo.ArmorPenetrationInt:F1} armor={props.shieldArmor:F1}");
            }
            return true;
        }

        private static readonly AccessTools.StructFieldRef<DamageInfo, float> DamageInfoPenetration =
            AccessTools.StructFieldRefAccess<DamageInfo, float>("armorPenetrationInt");

        public static bool IsExplosive(ProjectileCE p)
        {
            if (p?.def?.projectile is ProjectilePropertiesCE props && props.explosionRadius > 0f) return true;
            return p?.TryGetComp<CompExplosiveCE>() != null;
        }

        internal static void DevLog(string msg)
        {
            if (Prefs.DevMode)
            {
                Log.Message("[AMC OpenTop] " + msg);
            }
        }
    }
}
