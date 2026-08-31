using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Verse;
using Verse.AI;
using RimWorld;

namespace AbsolutelyMoreCannons
{
    /// <summary>
    /// Harmony patches for transferring target search line-of-sight origin to the turret center
    /// and bypassing self-occlusion from tall turret buildings.
    /// Only affects turrets configured with CompProperties_TurretViewTransfer.
    /// </summary>
    public static class HarmonyPatches_TurretViewTransfer
    {
        public static void TryPatchTurretViewTransfer(Harmony harmony)
        {
            if (TurretBarrelAnimationMod.settings?.logStartup ?? false)
            {
                Log.Message("[AMC Turret View Transfer] Initializing runtime patches...");
            }

            // 1. Vanilla Building_TurretGun.TargSearcher (Property or Method)
            try
            {
                var vanillaTargSearcher = AccessTools.PropertyGetter(typeof(Building_TurretGun), "TargSearcher")
                                       ?? AccessTools.Method(typeof(Building_TurretGun), "TargSearcher");
                if (vanillaTargSearcher != null)
                {
                    harmony.Patch(vanillaTargSearcher, postfix: new HarmonyMethod(typeof(HarmonyPatches_TurretViewTransfer), nameof(Postfix_TargSearcher)));
                    if (TurretBarrelAnimationMod.settings?.logStartup ?? false)
                    {
                        Log.Message("[AMC Turret View Transfer] SUCCESS: Patched Building_TurretGun.TargSearcher.");
                    }
                }
                else if (TurretBarrelAnimationMod.settings?.logStartup ?? false)
                {
                    Log.Warning("[AMC Turret View Transfer] WARNING: Building_TurretGun.TargSearcher not found.");
                }
            }
            catch (Exception ex)
            {
                if (TurretBarrelAnimationMod.settings?.logStartup ?? false)
                {
                    Log.Warning($"[AMC Turret View Transfer] Vanilla TargSearcher patch failed: {ex.Message}");
                }
            }

            // 2. CE Building_TurretGunCE.TargSearcher (Method or Property)
            try
            {
                var ceTurretType = AccessTools.TypeByName("CombatExtended.Building_TurretGunCE");
                if (ceTurretType != null)
                {
                    var ceTargSearcher = AccessTools.Method(ceTurretType, "TargSearcher")
                                      ?? AccessTools.PropertyGetter(ceTurretType, "TargSearcher");
                    if (ceTargSearcher != null)
                    {
                        harmony.Patch(ceTargSearcher, postfix: new HarmonyMethod(typeof(HarmonyPatches_TurretViewTransfer), nameof(Postfix_TargSearcher)));
                        if (TurretBarrelAnimationMod.settings?.logStartup ?? false)
                        {
                            Log.Message("[AMC Turret View Transfer] SUCCESS: Patched Building_TurretGunCE.TargSearcher.");
                        }
                    }
                    else if (TurretBarrelAnimationMod.settings?.logStartup ?? false)
                    {
                        Log.Warning("[AMC Turret View Transfer] WARNING: Building_TurretGunCE.TargSearcher not found.");
                    }
                }
            }
            catch (Exception ex)
            {
                if (TurretBarrelAnimationMod.settings?.logStartup ?? false)
                {
                    Log.Warning($"[AMC Turret View Transfer] CE TargSearcher patch failed: {ex.Message}");
                }
            }

            // 3. GenSight.LineOfSight (Patch static overloads that have skipFirstCell parameter)
            try
            {
                var losMethods = typeof(GenSight).GetMethods(BindingFlags.Public | BindingFlags.Static)
                                                  .Where(m => m.Name == nameof(GenSight.LineOfSight) && m.GetParameters().Any(p => p.Name == "skipFirstCell")).ToList();
                int patchedCount = 0;
                foreach (var losMethod in losMethods)
                {
                    harmony.Patch(losMethod, prefix: new HarmonyMethod(typeof(HarmonyPatches_TurretViewTransfer), nameof(Prefix_GenSight_LineOfSight)));
                    patchedCount++;
                }
                if (TurretBarrelAnimationMod.settings?.logStartup ?? false)
                {
                    Log.Message($"[AMC Turret View Transfer] SUCCESS: Patched {patchedCount} GenSight.LineOfSight overload(s).");
                }
            }
            catch (Exception ex)
            {
                if (TurretBarrelAnimationMod.settings?.logStartup ?? false)
                {
                    Log.Warning($"[AMC Turret View Transfer] GenSight.LineOfSight patch failed: {ex.Message}");
                }
            }

            // 4. Vanilla Verb.CanHitTargetFrom (Patch overloads with root parameter)
            try
            {
                var vanillaCanHitMethods = typeof(Verb).GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                                                       .Where(m => m.Name == nameof(Verb.CanHitTargetFrom) && m.GetParameters().Any(p => p.Name == "root")).ToList();
                int patchedCount = 0;
                foreach (var method in vanillaCanHitMethods)
                {
                    harmony.Patch(method, prefix: new HarmonyMethod(typeof(HarmonyPatches_TurretViewTransfer), nameof(Prefix_CanHitTargetFrom)));
                    patchedCount++;
                }
                if (TurretBarrelAnimationMod.settings?.logStartup ?? false)
                {
                    Log.Message($"[AMC Turret View Transfer] SUCCESS: Patched {patchedCount} Verb.CanHitTargetFrom overload(s).");
                }
            }
            catch (Exception ex)
            {
                if (TurretBarrelAnimationMod.settings?.logStartup ?? false)
                {
                    Log.Warning($"[AMC Turret View Transfer] Verb.CanHitTargetFrom patch failed: {ex.Message}");
                }
            }

            // 5. CE Verb_LaunchProjectileCE.CanHitTargetFrom (Patch overloads with root parameter)
            try
            {
                var ceVerbType = AccessTools.TypeByName("CombatExtended.Verb_LaunchProjectileCE");
                if (ceVerbType != null)
                {
                    var ceCanHitMethods = ceVerbType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                                                     .Where(m => m.Name == "CanHitTargetFrom" && m.GetParameters().Any(p => p.Name == "root")).ToList();
                    int patchedCount = 0;
                    foreach (var method in ceCanHitMethods)
                    {
                        harmony.Patch(method, prefix: new HarmonyMethod(typeof(HarmonyPatches_TurretViewTransfer), nameof(Prefix_CanHitTargetFrom)));
                        patchedCount++;
                    }
                    if (TurretBarrelAnimationMod.settings?.logStartup ?? false)
                    {
                        Log.Message($"[AMC Turret View Transfer] SUCCESS: Patched {patchedCount} Verb_LaunchProjectileCE.CanHitTargetFrom overload(s).");
                    }
                }
            }
            catch (Exception ex)
            {
                if (TurretBarrelAnimationMod.settings?.logStartup ?? false)
                {
                    Log.Warning($"[AMC Turret View Transfer] Verb_LaunchProjectileCE.CanHitTargetFrom patch failed: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Postfix for TargSearcher: transfers IAttackTargetSearcher to the turret itself if transferViewOrigin is enabled.
        /// </summary>
        public static void Postfix_TargSearcher(Building __instance, ref IAttackTargetSearcher __result)
        {
            if (__instance == null) return;

            CompTurretViewTransfer comp = __instance.GetComp<CompTurretViewTransfer>();
            if (comp != null && comp.Props.transferViewOrigin)
            {
                if (__instance is IAttackTargetSearcher searcher)
                {
                    __result = searcher;
                    AMCLogger.LogTurretViewTransfer($"TargSearcher redirected to turret {__instance.def.defName} at {__instance.Position}");
                }
            }
        }

        /// <summary>
        /// Prefix for Verb.CanHitTargetFrom: redirects root position to turret center if transferViewOrigin is enabled.
        /// </summary>
        public static void Prefix_CanHitTargetFrom(Verb __instance, ref IntVec3 root)
        {
            if (__instance?.caster is Building turret)
            {
                CompTurretViewTransfer comp = turret.GetComp<CompTurretViewTransfer>();
                if (comp != null && comp.Props.transferViewOrigin)
                {
                    if (root != turret.Position)
                    {
                        AMCLogger.LogTurretViewTransfer($"CanHitTargetFrom root redirected from {root} to turret position {turret.Position} for {turret.def.defName}");
                        root = turret.Position;
                    }
                }
            }
        }

        /// <summary>
        /// Prefix for GenSight.LineOfSight: if start/end position belongs to a turret with ignoreSelfOcclusion,
        /// completely overrides LineOfSight logic so cells within the turret's occupied footprint do NOT block sight.
        /// </summary>
        public static bool Prefix_GenSight_LineOfSight(IntVec3 start, IntVec3 end, Map map, bool skipFirstCell, Func<IntVec3, bool> validator, ref bool __result)
        {
            if (map == null || !start.InBounds(map) || !end.InBounds(map)) return true;

            Building turret = GetTurretForSightCheck(start, map) ?? GetTurretForSightCheck(end, map);

            if (turret != null)
            {
                CompTurretViewTransfer comp = turret.GetComp<CompTurretViewTransfer>();
                if (comp != null && comp.Props.ignoreSelfOcclusion)
                {
                    CellRect occupiedRect = turret.OccupiedRect();
                    __result = CustomLineOfSight(start, end, map, skipFirstCell, validator, occupiedRect, turret);
                    AMCLogger.LogTurretViewTransfer($"LineOfSight between {start} and {end} for {turret.def.defName} evaluated to {__result} (Self-Occlusion Bypassed)");
                    return false; // Skip original GenSight.LineOfSight completely
                }
            }

            return true; // Fall through to original GenSight.LineOfSight
        }

        private static bool CustomLineOfSight(IntVec3 start, IntVec3 end, Map map, bool skipFirstCell, Func<IntVec3, bool> validator, CellRect ignoreRect, Building turret)
        {
            foreach (IntVec3 c in GenSight.PointsOnLineOfSight(start, end))
            {
                if (skipFirstCell && c == start)
                {
                    continue;
                }

                // Cells within the turret's footprint do not block view
                if (ignoreRect.Contains(c))
                {
                    continue;
                }

                // Standard RimWorld line-of-sight checks for cells outside turret footprint
                if (!c.CanBeSeenOverFast(map))
                {
                    AMCLogger.LogTurretViewTransfer($"CustomLineOfSight: Cell {c} outside turret footprint blocked vision (CanBeSeenOverFast=false).");
                    return false;
                }
                if (validator != null && !validator(c))
                {
                    AMCLogger.LogTurretViewTransfer($"CustomLineOfSight: Cell {c} outside turret footprint failed custom validator.");
                    return false;
                }
            }

            return true;
        }

        private static Building GetTurretForSightCheck(IntVec3 cell, Map map)
        {
            if (map == null || !cell.InBounds(map)) return null;

            // 1. Direct edifice check (if cell is inside turret occupied footprint)
            Building edifice = cell.GetEdifice(map) as Building;
            if (edifice != null && edifice.GetComp<CompTurretViewTransfer>() != null)
            {
                return edifice;
            }

            // 2. Check all artificial buildings to see if cell is inside occupied rect or interaction cell
            List<Thing> buildings = map.listerThings.ThingsInGroup(ThingRequestGroup.BuildingArtificial);
            for (int i = 0; i < buildings.Count; i++)
            {
                Building b = buildings[i] as Building;
                if (b != null && b.GetComp<CompTurretViewTransfer>() != null)
                {
                    if (b.OccupiedRect().Contains(cell) || (b.def.hasInteractionCell && b.InteractionCell == cell))
                    {
                        return b;
                    }
                }
            }

            // 3. Check if a pawn is standing at cell manning a turret
            List<Thing> thingList = cell.GetThingList(map);
            for (int i = 0; i < thingList.Count; i++)
            {
                Pawn pawn = thingList[i] as Pawn;
                if (pawn != null)
                {
                    Building manned = pawn.MannedThing() as Building;
                    if (manned != null && manned.GetComp<CompTurretViewTransfer>() != null)
                    {
                        return manned;
                    }
                }
            }

            return null;
        }
    }
}
