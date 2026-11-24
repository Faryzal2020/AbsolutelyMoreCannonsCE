using System;
using System.Reflection;
using System.Collections.Generic;
using HarmonyLib;
using Verse;
using RimWorld;
using UnityEngine;

namespace AbsolutelyMoreCannons
{
    /// <summary>
    /// Harmony patches for individual accuracy control (sway, recoil, spread reduction, warmup scaling, range extension)
    /// </summary>
    [StaticConstructorOnStartup]
    public static class HarmonyPatches_AccuracyControl
    {
        static HarmonyPatches_AccuracyControl()
        {
            var harmony = new Harmony("AbsolutelyMoreCannons.AccuracyControl");
            
            var settings = TurretBarrelAnimationMod.settings;
            if (settings != null && settings.logStartup)
            {
                Log.Message("[AMC] Initializing FCS Accuracy & Target Control Harmony Patches...");
            }

            // Patch CE's sway, recoil, spread, warmup, and range methods
            TryPatchSwayMethod(harmony);
            TryPatchRecoilMethod(harmony);
            TryPatchSpreadMethod(harmony);
            TryPatchWarmupMethod(harmony);
            TryPatchRangeMethod(harmony);
            TryPatchShotFiredMethod(harmony);
        }
        
        [ThreadStatic]
        private static float preSwayRot;
        [ThreadStatic]
        private static float preSwayAng;
        [ThreadStatic]
        private static float preRecoilRot;
        [ThreadStatic]
        private static float preRecoilAng;

        private static void TryPatchSwayMethod(Harmony harmony)
        {
            try
            {
                var verbType = AccessTools.TypeByName("CombatExtended.Verb_LaunchProjectileCE");
                if (verbType == null)
                {
                    Log.Warning("[AMC] Could not find Verb_LaunchProjectileCE for sway patching");
                    return;
                }
                
                var swayMethod = AccessTools.Method(verbType, "GetSwayVec");
                if (swayMethod != null)
                {
                    harmony.Patch(
                        original: swayMethod,
                        prefix: new HarmonyMethod(typeof(HarmonyPatches_AccuracyControl), nameof(Prefix_GetSwayVec)) { priority = Priority.High },
                        postfix: new HarmonyMethod(typeof(HarmonyPatches_AccuracyControl), nameof(Postfix_GetSwayVec)) { priority = Priority.High }
                    );
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[AMC] Error patching GetSwayVec: {ex}");
            }
        }
        
        private static void TryPatchRecoilMethod(Harmony harmony)
        {
            try
            {
                var verbType = AccessTools.TypeByName("CombatExtended.Verb_LaunchProjectileCE");
                if (verbType == null) return;
                
                var recoilMethod = AccessTools.Method(verbType, "GetRecoilVec");
                if (recoilMethod != null)
                {
                    harmony.Patch(
                        original: recoilMethod,
                        prefix: new HarmonyMethod(typeof(HarmonyPatches_AccuracyControl), nameof(Prefix_GetRecoilVec)) { priority = Priority.VeryHigh },
                        postfix: new HarmonyMethod(typeof(HarmonyPatches_AccuracyControl), nameof(Postfix_GetRecoilVec)) { priority = Priority.High }
                    );
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[AMC] Error patching GetRecoilVec: {ex}");
            }
        }
        
        private static void TryPatchSpreadMethod(Harmony harmony)
        {
            try
            {
                var verbType = AccessTools.TypeByName("CombatExtended.Verb_LaunchProjectileCE");
                if (verbType == null)
                {
                    Log.Warning("[AMC] Could not find Verb_LaunchProjectileCE for spread patching");
                    return;
                }
                
                var reportMethod = AccessTools.Method(verbType, "ShiftVecReportFor", new Type[] { typeof(LocalTargetInfo) });
                if (reportMethod != null)
                {
                    harmony.Patch(
                        original: reportMethod,
                        postfix: new HarmonyMethod(typeof(HarmonyPatches_AccuracyControl), nameof(Postfix_ShiftVecReportFor))
                    );
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[AMC] Error patching ShiftVecReportFor: {ex}");
            }
        }

        private static void TryPatchWarmupMethod(Harmony harmony)
        {
            try
            {
                var tickMethod = AccessTools.Method(typeof(Building_TurretGun), "Tick");
                if (tickMethod != null)
                {
                    harmony.Patch(
                        original: tickMethod,
                        postfix: new HarmonyMethod(typeof(HarmonyPatches_AccuracyControl), nameof(Postfix_TurretWarmupScale))
                    );
                }

                var ceTurretType = AccessTools.TypeByName("CombatExtended.Building_TurretGunCE");
                if (ceTurretType != null)
                {
                    var ceTickMethod = AccessTools.Method(ceTurretType, "Tick");
                    if (ceTickMethod != null)
                    {
                        harmony.Patch(
                            original: ceTickMethod,
                            postfix: new HarmonyMethod(typeof(HarmonyPatches_AccuracyControl), nameof(Postfix_TurretWarmupScale))
                        );
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[AMC] Error patching Tick for warmup: {ex}");
            }
        }

        private static void TryPatchRangeMethod(Harmony harmony)
        {
            try
            {
                var canHitMethod = AccessTools.Method(typeof(Verb), nameof(Verb.CanHitTargetFrom), new Type[] { typeof(IntVec3), typeof(LocalTargetInfo) });
                if (canHitMethod != null)
                {
                    harmony.Patch(
                        original: canHitMethod,
                        postfix: new HarmonyMethod(typeof(HarmonyPatches_AccuracyControl), nameof(Postfix_CanHitTargetFrom))
                    );
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[AMC] Error patching Verb.CanHitTargetFrom: {ex}");
            }
        }

        private static void TryPatchShotFiredMethod(Harmony harmony)
        {
            try
            {
                var verbType = AccessTools.TypeByName("CombatExtended.Verb_LaunchProjectileCE");
                if (verbType != null)
                {
                    var tryCastMethod = AccessTools.Method(verbType, "TryCastShot");
                    if (tryCastMethod != null)
                    {
                        harmony.Patch(
                            original: tryCastMethod,
                            postfix: new HarmonyMethod(typeof(HarmonyPatches_AccuracyControl), nameof(Postfix_CEVerbShotFired))
                        );
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[AMC] Error patching Verb_LaunchProjectileCE.TryCastShot: {ex}");
            }
        }

        private static readonly Dictionary<int, int> lastWarmupTicks = new Dictionary<int, int>();

        public static void Postfix_TurretWarmupScale(Thing __instance)
        {
            try
            {
                if (__instance is Building_Turret turret && turret.Spawned)
                {
                    var fcsComp = turret.TryGetComp<CompTurretFCS>();
                    if (fcsComp != null && fcsComp.ActiveStats != null)
                    {
                        float aimMult = fcsComp.ActiveStats.aimTimeMultiplier;
                        if (aimMult > 0f && aimMult != 1.0f)
                        {
                            FieldInfo warmupField = AccessTools.Field(__instance.GetType(), "burstWarmupTicksLeft");
                            if (warmupField != null)
                            {
                                int currentWarmup = (int)warmupField.GetValue(turret);
                                int thingID = turret.thingIDNumber;

                                lastWarmupTicks.TryGetValue(thingID, out int prevWarmup);

                                // Detect the moment burstWarmupTicksLeft gets newly assigned from <=0 to >3
                                if (prevWarmup <= 0 && currentWarmup > 3)
                                {
                                    int scaledWarmup = Mathf.Max(1, Mathf.RoundToInt(currentWarmup * aimMult));
                                    warmupField.SetValue(turret, scaledWarmup);
                                    lastWarmupTicks[thingID] = scaledWarmup;

                                    string fcsLabel = fcsComp.LoadedFCSItem?.def?.label ?? "Unknown FCS";
                                    AMCLogger.LogFCS($"TURRET AIMING & TARGET ACQUIRED | Turret: {turret.LabelCap} at ({turret.Position.x},{turret.Position.z}) | FCS Installed: '{fcsLabel}' | Base Aim Time: {currentWarmup} ticks ({(currentWarmup / 60f):F2}s) -> FCS Aim Time: {scaledWarmup} ticks ({(scaledWarmup / 60f):F2}s) [aimTimeMultiplier: {aimMult:F2}]");
                                    return;
                                }

                                lastWarmupTicks[thingID] = currentWarmup;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[AMC] Error in Postfix_TurretWarmupScale: {ex}");
            }
        }

        public static void Postfix_CanHitTargetFrom(Verb __instance, IntVec3 root, LocalTargetInfo targ, ref bool __result)
        {
            try
            {
                if (__instance == null || __instance.caster == null || !targ.IsValid || __instance.caster.Map == null) return;

                var fcsComp = GetFCSCompFromCaster(__instance.caster);
                if (fcsComp != null && fcsComp.ActiveStats != null)
                {
                    float rangeMult = fcsComp.ActiveStats.rangeMultiplier;
                    if (rangeMult > 1.0f)
                    {
                        float baseRange = __instance.verbProps.range;
                        float extendedRange = baseRange * rangeMult;
                        float minRange = __instance.verbProps.minRange;
                        float dist = (targ.Cell - root).LengthHorizontal;

                        if (dist >= minRange && dist <= extendedRange)
                        {
                            if (dist > baseRange && !__result)
                            {
                                if (GenSight.LineOfSight(root, targ.Cell, __instance.caster.Map))
                                {
                                    __result = true;
                                    string fcsLabel = fcsComp.LoadedFCSItem?.def?.label ?? "Unknown FCS";
                                    AMCLogger.LogFCS($"EXTENDED RANGE TARGETING | Shooter: {__instance.caster.LabelCap} | FCS Installed: '{fcsLabel}' | Target Dist: {dist:F1} (Base Max Range: {baseRange:F1} -> Extended Max Range: {extendedRange:F1})");
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[AMC] Error in Postfix_CanHitTargetFrom: {ex}");
            }
        }

        public static void Postfix_ShiftVecReportFor(Verb __instance, ref object __result)
        {
            try
            {
                if (__instance == null || __instance.caster == null || __result == null) return;

                Thing caster = __instance.caster;
                var fcsComp = GetFCSCompFromCaster(caster);
                var accComp = caster.TryGetComp<Comp_AccuracyOverride>();
                bool isTurret = caster is Building_Turret || (caster.ParentHolder is Building_Turret);

                if (fcsComp == null && accComp == null && !isTurret) return;

                float spreadReductionPercent = GetSpreadReductionForVerb(__instance);
                float swayReductionPercent = GetSwayReductionForVerb(__instance);

                FieldInfo spreadField = AccessTools.Field(__result.GetType(), "spreadDegrees");
                FieldInfo swayField = AccessTools.Field(__result.GetType(), "swayDegrees");

                if (spreadField != null && spreadReductionPercent > 0f)
                {
                    float origSpread = (float)spreadField.GetValue(__result);
                    float spreadMult = Mathf.Max(0f, 1.0f - (spreadReductionPercent / 100f));
                    float newSpread = origSpread * spreadMult;
                    spreadField.SetValue(__result, newSpread);

                    if (fcsComp != null && fcsComp.ActiveStats != null)
                    {
                        string fcsLabel = fcsComp.LoadedFCSItem?.def?.label ?? "FCS";
                        AMCLogger.LogFCS($"ACCURACY REPORT | Shooter: {caster.LabelCap} | FCS Installed: '{fcsLabel}' | Spread Mult: {spreadMult:F2} | Base Spread: {origSpread:F3}° -> Final Spread: {newSpread:F3}° ({spreadReductionPercent:F1}% reduction)");
                    }
                }

                if (swayField != null && swayReductionPercent > 0f)
                {
                    float origSway = (float)swayField.GetValue(__result);
                    float swayMult = Mathf.Max(0f, 1.0f - (swayReductionPercent / 100f));
                    swayField.SetValue(__result, origSway * swayMult);
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[AMC] Error in Postfix_ShiftVecReportFor: {ex}");
            }
        }

        public static void Postfix_CEVerbShotFired(Verb __instance, bool __result)
        {
            try
            {
                if (!__result || __instance == null || __instance.caster == null) return;

                var fcsComp = GetFCSCompFromCaster(__instance.caster);
                if (fcsComp != null)
                {
                    string fcsLabel = fcsComp.LoadedFCSItem?.def?.label ?? "No FCS";
                    AMCLogger.LogFCS($"SHOT FIRED | Turret: {__instance.caster.LabelCap} | FCS Installed: '{fcsLabel}' | Target: {__instance.CurrentTarget}");
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[AMC] Error in Postfix_CEVerbShotFired: {ex}");
            }
        }
        
        public static void Prefix_GetSwayVec(float rotation, float angle)
        {
            preSwayRot = rotation;
            preSwayAng = angle;
        }

        public static void Postfix_GetSwayVec(object __instance, ref float rotation, ref float angle)
        {
            try
            {
                float reductionPercent = GetSwayReductionForVerb(__instance);
                if (reductionPercent <= 0f) return;
                
                float scale = Mathf.Max(0f, 1.0f - (reductionPercent / 100f));
                float dRot = rotation - preSwayRot;
                float dAng = angle - preSwayAng;
                rotation = preSwayRot + dRot * scale;
                angle = preSwayAng + dAng * scale;
            }
            catch (Exception ex)
            {
                Log.Error($"[AMC] Error in Postfix_GetSwayVec: {ex}");
            }
        }

        public static void Prefix_GetRecoilVec(float rotation, float angle)
        {
            preRecoilRot = rotation;
            preRecoilAng = angle;
        }
        
        public static void Postfix_GetRecoilVec(object __instance, ref float rotation, ref float angle)
        {
            try
            {
                float reductionPercent = GetRecoilReductionForVerb(__instance);
                if (reductionPercent <= 0f) return;
                
                float scale = Mathf.Max(0f, 1.0f - (reductionPercent / 100f));
                float dRot = rotation - preRecoilRot;
                float dAng = angle - preRecoilAng;
                rotation = preRecoilRot + dRot * scale;
                angle = preRecoilAng + dAng * scale;
            }
            catch (Exception ex)
            {
                Log.Error($"[AMC] Error in Postfix_GetRecoilVec: {ex}");
            }
        }
        
        // === HELPER METHODS ===
        
        private static float GetSwayReductionForVerb(object verbInstance)
        {
            Thing caster = GetCasterFromVerb(verbInstance);
            if (caster == null) return 0f;

            var fcsComp = GetFCSCompFromCaster(caster);
            var comp = caster.TryGetComp<Comp_AccuracyOverride>();
            bool isTurret = caster is Building_Turret || (caster.ParentHolder is Building_Turret);

            // Apply only to turrets or things with FCS / AccuracyOverride
            if (fcsComp == null && comp == null && !isTurret) return 0f;

            float baseReduction = 0f;
            float fcsMultiplier = 1.0f;

            if (fcsComp != null && fcsComp.ActiveStats != null)
            {
                fcsMultiplier = fcsComp.ActiveStats.swayMultiplier;
            }

            if (comp != null)
            {
                baseReduction = comp.GetSwayReduction();
            }
            else
            {
                var settings = TurretBarrelAnimationMod.settings;
                baseReduction = (settings != null) ? settings.swayReductionPercent : 0f;
            }

            return 100f - ((100f - baseReduction) * fcsMultiplier);
        }
        
        private static float GetRecoilReductionForVerb(object verbInstance)
        {
            Thing caster = GetCasterFromVerb(verbInstance);
            if (caster == null) return 0f;

            var fcsComp = GetFCSCompFromCaster(caster);
            var comp = caster.TryGetComp<Comp_AccuracyOverride>();
            bool isTurret = caster is Building_Turret || (caster.ParentHolder is Building_Turret);

            // Apply only to turrets or things with FCS / AccuracyOverride
            if (fcsComp == null && comp == null && !isTurret) return 0f;

            float baseReduction = 0f;
            float fcsMultiplier = 1.0f;

            if (fcsComp != null && fcsComp.ActiveStats != null)
            {
                fcsMultiplier = fcsComp.ActiveStats.recoilMultiplier;
            }

            if (comp != null)
            {
                baseReduction = comp.GetRecoilReduction();
            }
            else
            {
                var settings = TurretBarrelAnimationMod.settings;
                baseReduction = (settings != null) ? settings.recoilReductionPercent : 0f;
            }

            return 100f - ((100f - baseReduction) * fcsMultiplier);
        }

        private static float GetSpreadReductionForVerb(object verbInstance)
        {
            Thing caster = GetCasterFromVerb(verbInstance);
            if (caster == null) return 0f;

            var fcsComp = GetFCSCompFromCaster(caster);
            var comp = caster.TryGetComp<Comp_AccuracyOverride>();
            bool isTurret = caster is Building_Turret || (caster.ParentHolder is Building_Turret);

            if (fcsComp == null && comp == null && !isTurret) return 0f;

            float baseReduction = 0f;
            float fcsMultiplier = 1.0f;

            if (fcsComp != null && fcsComp.ActiveStats != null)
            {
                fcsMultiplier = fcsComp.ActiveStats.spreadMultiplier;
            }

            if (comp != null)
            {
                baseReduction = comp.GetSpreadReduction();
            }
            else
            {
                var settings = TurretBarrelAnimationMod.settings;
                baseReduction = (settings != null) ? settings.spreadReductionPercent : 0f;
            }

            return 100f - ((100f - baseReduction) * fcsMultiplier);
        }
        
        public static CompTurretFCS GetFCSCompFromCaster(Thing caster)
        {
            if (caster == null) return null;
            var comp = caster.TryGetComp<CompTurretFCS>();
            if (comp != null) return comp;

            IThingHolder currentHolder = caster.ParentHolder;
            while (currentHolder != null)
            {
                if (currentHolder is Thing parentThing)
                {
                    var parentComp = parentThing.TryGetComp<CompTurretFCS>();
                    if (parentComp != null) return parentComp;
                }
                currentHolder = currentHolder.ParentHolder;
            }
            return null;
        }
        
        private static Thing GetCasterFromVerb(object verbInstance)
        {
            if (verbInstance == null) return null;
            if (verbInstance is Verb verb && verb.caster != null)
            {
                return verb.caster;
            }

            try
            {
                FieldInfo casterField = AccessTools.Field(verbInstance.GetType(), "caster");
                if (casterField != null)
                {
                    return casterField.GetValue(verbInstance) as Thing;
                }
                
                PropertyInfo casterPawnProp = AccessTools.Property(verbInstance.GetType(), "CasterPawn");
                if (casterPawnProp != null)
                {
                    return casterPawnProp.GetValue(verbInstance) as Thing;
                }
            }
            catch
            {
            }
            
            return null;
        }
    }
}
