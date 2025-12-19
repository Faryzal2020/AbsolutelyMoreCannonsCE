using System;
using System.Reflection;
using HarmonyLib;
using Verse;
using UnityEngine;

namespace AbsolutelyMoreCannons
{
    /// <summary>
    /// Harmony patches for individual accuracy control (sway, recoil, spread reduction)
    /// Patches CE's deviation calculation methods to scale down their effects
    /// </summary>
    [StaticConstructorOnStartup]
    public static class HarmonyPatches_AccuracyControl
    {
        static HarmonyPatches_AccuracyControl()
        {
            var harmony = new Harmony("AbsolutelyMoreCannons.AccuracyControl");
            
            // Patch CE's sway, recoil, and spread methods
            TryPatchSwayMethod(harmony);
            TryPatchRecoilMethod(harmony);
            TryPatchSpreadMethod(harmony);
        }
        
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
                        postfix: new HarmonyMethod(typeof(HarmonyPatches_AccuracyControl), nameof(Postfix_GetSwayVec))
                    );
                    
                    var settings = TurretBarrelAnimationMod.settings;
                    if (settings != null && settings.logStartup)
                    {
                        Log.Message("[AMC] Successfully patched GetSwayVec for sway reduction");
                    }
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
                if (verbType == null)
                {
                    return;
                }
                
                var recoilMethod = AccessTools.Method(verbType, "GetRecoilVec");
                if (recoilMethod != null)
                {
                    harmony.Patch(
                        original: recoilMethod,
                        postfix: new HarmonyMethod(typeof(HarmonyPatches_AccuracyControl), nameof(Postfix_GetRecoilVec))
                    );
                    
                    var settings = TurretBarrelAnimationMod.settings;
                    if (settings != null && settings.logStartup)
                    {
                        Log.Message("[AMC] Successfully patched GetRecoilVec for recoil reduction");
                    }
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
                var reportType = AccessTools.TypeByName("CombatExtended.ShiftVecReport");
                if (reportType == null)
                {
                    Log.Warning("[AMC] Could not find ShiftVecReport for spread patching");
                    return;
                }
                
                var spreadMethod = AccessTools.Method(reportType, "GetRandSpreadVec");
                if (spreadMethod != null)
                {
                    harmony.Patch(
                        original: spreadMethod,
                        postfix: new HarmonyMethod(typeof(HarmonyPatches_AccuracyControl), nameof(Postfix_GetRandSpreadVec))
                    );
                    
                    var settings = TurretBarrelAnimationMod.settings;
                    if (settings != null && settings.logStartup)
                    {
                        Log.Message("[AMC] Successfully patched GetRandSpreadVec for spread reduction");
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[AMC] Error patching GetRandSpreadVec: {ex}");
            }
        }
        
        /// <summary>
        /// Postfix for GetSwayVec - scales sway values based on reduction setting
        /// Checks per-turret override first, then global setting
        /// </summary>
        public static void Postfix_GetSwayVec(object __instance, ref float rotation, ref float angle)
        {
            try
            {
                // Try to get per-turret override
                float reductionPercent = GetSwayReductionForVerb(__instance);
                
                if (reductionPercent <= 0f)
                {
                    return;
                }
                
                // Calculate scaling factor (0% = 1.0 scale, 100% = 0.0 scale)
                float scale = 1.0f - (reductionPercent / 100f);
                
                // Scale down the sway values
                rotation *= scale;
                angle *= scale;
            }
            catch (Exception ex)
            {
                Log.Error($"[AMC] Error in Postfix_GetSwayVec: {ex}");
            }
        }
        
        /// <summary>
        /// Postfix for GetRecoilVec - scales recoil values based on reduction setting
        /// Checks per-turret override first, then global setting
        /// </summary>
        public static void Postfix_GetRecoilVec(object __instance, ref float rotation, ref float angle)
        {
            try
            {
                // Try to get per-turret override
                float reductionPercent = GetRecoilReductionForVerb(__instance);
                
                if (reductionPercent <= 0f)
                {
                    return;
                }
                
                // Calculate scaling factor (0% = 1.0 scale, 100% = 0.0 scale)
                float scale = 1.0f - (reductionPercent / 100f);
                
                // Scale down the recoil values
                rotation *= scale;
                angle *= scale;
            }
            catch (Exception ex)
            {
                Log.Error($"[AMC] Error in Postfix_GetRecoilVec: {ex}");
            }
        }
        
        /// <summary>
        /// Postfix for GetRandSpreadVec - scales spread vector based on reduction setting
        /// Checks per-turret override first, then global setting
        /// </summary>
        public static void Postfix_GetRandSpreadVec(object __instance, ref Vector2 __result)
        {
            try
            {
                // Try to get per-turret override
                float reductionPercent = GetSpreadReductionForReport(__instance);
                
                if (reductionPercent <= 0f)
                {
                    return;
                }
                
                // Calculate scaling factor (0% = 1.0 scale, 100% = 0.0 scale)
                float scale = 1.0f - (reductionPercent / 100f);
                
                // Scale down the spread vector
                __result *= scale;
            }
            catch (Exception ex)
            {
                Log.Error($"[AMC] Error in Postfix_GetRandSpreadVec: {ex}");
            }
        }
        
        // === HELPER METHODS ===
        
        /// <summary>
        /// Get sway reduction percent for a verb instance
        /// Checks per-turret comp first, falls back to global setting
        /// </summary>
        private static float GetSwayReductionForVerb(object verbInstance)
        {
            Thing caster = GetCasterFromVerb(verbInstance);
            if (caster != null)
            {
                var comp = caster.TryGetComp<Comp_AccuracyOverride>();
                if (comp != null)
                {
                    return comp.GetSwayReduction();
                }
            }
            
            // Fall back to global setting
            var settings = TurretBarrelAnimationMod.settings;
            return (settings != null) ? settings.swayReductionPercent : 0f;
        }
        
        /// <summary>
        /// Get recoil reduction percent for a verb instance
        /// </summary>
        private static float GetRecoilReductionForVerb(object verbInstance)
        {
            Thing caster = GetCasterFromVerb(verbInstance);
            if (caster != null)
            {
                var comp = caster.TryGetComp<Comp_AccuracyOverride>();
                if (comp != null)
                {
                    return comp.GetRecoilReduction();
                }
            }
            
            // Fall back to global setting
            var settings = TurretBarrelAnimationMod.settings;
            return (settings != null) ? settings.recoilReductionPercent : 0f;
        }
        
        /// <summary>
        /// Get spread reduction percent for a ShiftVecReport instance
        /// ShiftVecReport doesn't have direct access to caster, so we use global setting
        /// </summary>
        private static float GetSpreadReductionForReport(object reportInstance)
        {
            // ShiftVecReport doesn't store caster reference
            // We'd need to track this in ShiftTarget prefix if we want per-turret spread
            // For now, use global setting only
            var settings = TurretBarrelAnimationMod.settings;
            return (settings != null) ? settings.spreadReductionPercent : 0f;
        }
        
        /// <summary>
        /// Extract caster Thing from a Verb instance using reflection
        /// </summary>
        private static Thing GetCasterFromVerb(object verbInstance)
        {
            try
            {
                // Try to get caster field from Verb base class
                FieldInfo casterField = verbInstance.GetType().GetField("caster", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (casterField != null)
                {
                    return casterField.GetValue(verbInstance) as Thing;
                }
                
                // Try casterPawn (for pawn-held weapons)
                PropertyInfo casterPawnProp = verbInstance.GetType().GetProperty("CasterPawn", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (casterPawnProp != null)
                {
                    return casterPawnProp.GetValue(verbInstance) as Thing;
                }
            }
            catch
            {
                // Silent fail - will use global setting
            }
            
            return null;
        }
    }
}
