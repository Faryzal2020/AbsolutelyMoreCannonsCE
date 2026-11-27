using System;
using System.Collections.Generic;
using CombatExtended;
using CombatExtended.Compatibility;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace AbsolutelyMoreCannons
{
    /// <summary>
    /// Harmony patches and CE BlockerRegistry hooks for open-top turret seats (CompProperties_EnclosedTurret.seatOffset).
    /// Spec: docs/superpowers/specs/2026-11-27-open-top-turret-seat-shield-design.md
    /// </summary>
    public static class HarmonyPatches_OpenTopTurret
    {
        private static readonly AccessTools.FieldRef<Pawn_DrawTracker, Pawn> DrawTrackerPawn =
            AccessTools.FieldRefAccess<Pawn_DrawTracker, Pawn>("pawn");

        public static void TryPatchOpenTop(Harmony harmony)
        {
            // 1. Seat draw position at the lowest level: GenThing.TrueCenter (CE aim point) and Pawn.DrawPos
            //    (CE hitbox, rendering, labels) both read Pawn_DrawTracker.DrawPos.
            try
            {
                var getter = AccessTools.PropertyGetter(typeof(Pawn_DrawTracker), "DrawPos");
                harmony.Patch(getter, prefix: new HarmonyMethod(typeof(HarmonyPatches_OpenTopTurret), nameof(Prefix_DrawTracker_DrawPos)));
                LogStartup("Patched Pawn_DrawTracker.DrawPos for seat placement.");
            }
            catch (Exception ex)
            {
                Log.Warning($"[AMC OpenTop] DrawPos patch skipped: {ex.Message}");
            }

            // 1b. Seated gunner faces the barrel. The ManTurret driver keeps facing the turret from the interaction
            //     cell, so set the rotation after the tracker's own update (authoritative for rendering).
            try
            {
                foreach (string name in new[] { "UpdateRotation", "ProcessPostTickVisuals" })
                {
                    var m = AccessTools.Method(typeof(Pawn_RotationTracker), name);
                    if (m != null)
                    {
                        harmony.Patch(m, postfix: new HarmonyMethod(typeof(HarmonyPatches_OpenTopTurret), nameof(Postfix_RotationTracker)));
                    }
                }
                // The ManTurret job faces the turret from the interaction cell (e.g. North for the DCA 40); block it.
                foreach (string name in new[] { "FaceTarget", "Face", "FaceCell" })
                {
                    var m = AccessTools.Method(typeof(Pawn_RotationTracker), name);
                    if (m != null)
                    {
                        harmony.Patch(m, prefix: new HarmonyMethod(typeof(HarmonyPatches_OpenTopTurret), nameof(Prefix_RotationTracker_Face)));
                    }
                }
                LogStartup("Patched Pawn_RotationTracker for seated gunner facing.");
            }
            catch (Exception ex)
            {
                Log.Warning($"[AMC OpenTop] Rotation patch skipped: {ex.Message}");
            }

            // 2. Seated gunner's CE vertical range follows the gun's barrel height, not the cover in his logical cell.
            try
            {
                var calc = AccessTools.Method(typeof(CollisionVertical), "CalculateHeightRange");
                harmony.Patch(calc, postfix: new HarmonyMethod(typeof(HarmonyPatches_OpenTopTurret), nameof(Postfix_CalculateHeightRange)));
                LogStartup("Patched CollisionVertical.CalculateHeightRange for seated gunners.");
            }
            catch (Exception ex)
            {
                Log.Warning($"[AMC OpenTop] CollisionVertical patch skipped: {ex.Message}");
            }

            // 3. Explosions judge a seated gunner by his seat cell, not his logical (interaction) cell.
            try
            {
                int patched = 0;
                var types = new List<Type> { typeof(DamageWorker) };
                types.AddRange(typeof(DamageWorker).AllSubclassesNonAbstract());
                foreach (Type t in types)
                {
                    var dmgThing = AccessTools.DeclaredMethod(t, "ExplosionDamageThing");
                    if (dmgThing != null)
                    {
                        harmony.Patch(dmgThing,
                            prefix: new HarmonyMethod(typeof(HarmonyPatches_OpenTopTurret), nameof(Prefix_ExplosionDamageThing)),
                            finalizer: new HarmonyMethod(typeof(HarmonyPatches_OpenTopTurret), nameof(Finalizer_ExplosionDamageThing)));
                        patched++;
                    }
                    var affectCell = AccessTools.DeclaredMethod(t, "ExplosionAffectCell");
                    if (affectCell != null)
                    {
                        harmony.Patch(affectCell, postfix: new HarmonyMethod(typeof(HarmonyPatches_OpenTopTurret), nameof(Postfix_ExplosionAffectCell)));
                        patched++;
                    }
                }
                LogStartup($"Patched {patched} DamageWorker explosion methods for seat-cell blasts.");
            }
            catch (Exception ex)
            {
                Log.Warning($"[AMC OpenTop] Explosion patches skipped: {ex.Message}");
            }

            // 4. CE projectile hooks: reach the seated gunner across the turret footprint; shield interception.
            try
            {
                BlockerRegistry.RegisterCheckForCollisionCallback(OpenTopShield.CheckCell);
                BlockerRegistry.RegisterBeforeCollideWithCallback(OpenTopShield.BeforeCollideWith);
                LogStartup("Registered CE BlockerRegistry callbacks.");
            }
            catch (Exception ex)
            {
                Log.Warning($"[AMC OpenTop] BlockerRegistry registration skipped: {ex.Message}");
            }
        }

        private static void LogStartup(string msg)
        {
            if (TurretBarrelAnimationMod.settings?.logStartup ?? false)
            {
                Log.Message("[AMC OpenTop] " + msg);
            }
        }

        private static readonly AccessTools.FieldRef<Pawn_RotationTracker, Pawn> RotationTrackerPawn =
            AccessTools.FieldRefAccess<Pawn_RotationTracker, Pawn>("pawn");

        public static void Postfix_RotationTracker(Pawn_RotationTracker __instance)
        {
            Pawn pawn = RotationTrackerPawn(__instance);
            if (pawn != null && OpenTopSeatUtility.TryGetSeated(pawn, out Building turret, out _))
            {
                pawn.Rotation = Rot4.FromAngleFlat(OpenTopSeatUtility.TopAngle(turret));
            }
        }

        public static bool Prefix_RotationTracker_Face(Pawn_RotationTracker __instance)
        {
            Pawn pawn = RotationTrackerPawn(__instance);
            if (pawn != null && OpenTopSeatUtility.TryGetSeated(pawn, out Building turret, out _))
            {
                pawn.Rotation = Rot4.FromAngleFlat(OpenTopSeatUtility.TopAngle(turret));
                return false;
            }
            return true;
        }

        public static bool Prefix_DrawTracker_DrawPos(Pawn_DrawTracker __instance, ref Vector3 __result)
        {
            Pawn pawn = DrawTrackerPawn(__instance);
            if (pawn != null && OpenTopSeatUtility.TryGetSeated(pawn, out Building turret, out CompEnclosedTurret comp))
            {
                Vector3 seat = OpenTopSeatUtility.SeatWorldPos(turret, comp);
                // Above the turret top (drawn at turret.y + AltInc), below a drawOnTop barrel (ItemImportant + 0.1).
                seat.y = turret.DrawPos.y + Altitudes.AltInc * 2f;
                __result = seat;
                return false;
            }
            return true;
        }

        private static readonly System.Reflection.MethodInfo ExplosionDamageThingMethod =
            AccessTools.Method(typeof(DamageWorker), "ExplosionDamageThing");

        // Positional args (__1 = Thing t, __4 = IntVec3 cell): overrides may rename parameters.
        public static bool Prefix_ExplosionDamageThing(Thing __1, IntVec3 __4)
        {
            if (__1 is Pawn pawn && OpenTopSeatUtility.TryGetSeated(pawn, out Building turret, out CompEnclosedTurret comp))
            {
                if (__4 != OpenTopSeatUtility.SeatCell(turret, comp))
                {
                    return false; // the blast reached his logical cell, not his seat
                }
                OpenTopShield.DevLog($"blast seat gunner={pawn.thingIDNumber} cell={__4}");
            }
            OpenTopContext.InBlast = true;
            return true;
        }

        public static Exception Finalizer_ExplosionDamageThing(Exception __exception)
        {
            OpenTopContext.InBlast = false;
            return __exception;
        }

        // Positional args: __0 = Explosion, __1 = IntVec3 c, __2 = damagedThings, __3 = ignoredThings.
        public static void Postfix_ExplosionAffectCell(DamageWorker __instance, Explosion __0, IntVec3 __1, List<Thing> __2, List<Thing> __3)
        {
            Map map = __0?.Map;
            if (map == null || ExplosionDamageThingMethod == null) return;
            var turrets = OpenTopSeatUtility.SeatedTurrets(map);
            for (int i = 0; i < turrets.Count; i++)
            {
                CompEnclosedTurret comp = turrets[i];
                Pawn gunner = OpenTopSeatUtility.SeatedGunner(comp);
                if (gunner == null || gunner.Position == __1) continue;
                if (__2 != null && __2.Contains(gunner)) continue;
                if (__3 != null && __3.Contains(gunner)) continue;
                if (OpenTopSeatUtility.SeatCell(comp.Turret, comp) != __1) continue;
                ExplosionDamageThingMethod.Invoke(__instance, new object[] { __0, gunner, __2, __3, __1 });
            }
        }

        public static void Postfix_CalculateHeightRange(Thing thing, ref FloatRange heightRange, ref float shotHeight)
        {
            if (thing is Pawn pawn && OpenTopSeatUtility.TryGetSeated(pawn, out Building turret, out _))
            {
                FloatRange r = OpenTopSeatUtility.GunnerVerticalRange(pawn, turret);
                heightRange = r;
                shotHeight = r.max - (r.max - r.min) * (1f - CollisionVertical.BodyRegionMiddleHeight);
            }
        }
    }
}
