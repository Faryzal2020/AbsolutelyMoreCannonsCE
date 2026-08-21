using System;
using HarmonyLib;
using Verse;
using Verse.AI;
using RimWorld;
using UnityEngine;

namespace AbsolutelyMoreCannons
{
    /// <summary>
    /// Harmony patches enforcing operator protection, graphic suppression, label re-centering,
    /// fire, temperature, and suppression immunity for pawns operating enclosed manned turrets.
    /// Uses safe runtime method lookup to prevent PatchAll exceptions.
    /// </summary>
    public static class HarmonyPatches_EnclosedTurret
    {
        public static void TryPatchEnclosedTurrets(Harmony harmony)
        {
            // 1. Hide Pawn Graphic (Pawn.DrawAt, Pawn.DynamicDrawPhaseAt, PawnRenderer.RenderPawnAt, PawnRenderer.DynamicDrawPhaseAt, PawnRenderTree.Draw)
            try
            {
                var drawAtMethod = AccessTools.Method(typeof(Pawn), "DrawAt");
                if (drawAtMethod != null)
                {
                    harmony.Patch(drawAtMethod, prefix: new HarmonyMethod(typeof(HarmonyPatches_EnclosedTurret), nameof(Prefix_Pawn_DrawAt)));
                }

                var pawnDynDrawMethod = AccessTools.Method(typeof(Pawn), "DynamicDrawPhaseAt");
                if (pawnDynDrawMethod != null)
                {
                    harmony.Patch(pawnDynDrawMethod, prefix: new HarmonyMethod(typeof(HarmonyPatches_EnclosedTurret), nameof(Prefix_Pawn_DynamicDrawPhaseAt)));
                }

                var pawnRendererType = AccessTools.TypeByName("Verse.PawnRenderer");
                if (pawnRendererType != null)
                {
                    var renderPawnAtMethod = AccessTools.Method(pawnRendererType, "RenderPawnAt");
                    if (renderPawnAtMethod != null)
                    {
                        harmony.Patch(renderPawnAtMethod, prefix: new HarmonyMethod(typeof(HarmonyPatches_EnclosedTurret), nameof(Prefix_PawnRenderer_RenderPawnAt)));
                    }

                    var rendererDynDrawMethod = AccessTools.Method(pawnRendererType, "DynamicDrawPhaseAt");
                    if (rendererDynDrawMethod != null)
                    {
                        harmony.Patch(rendererDynDrawMethod, prefix: new HarmonyMethod(typeof(HarmonyPatches_EnclosedTurret), nameof(Prefix_PawnRenderer_DynamicDrawPhaseAt)));
                    }
                }

                var pawnRenderTreeType = AccessTools.TypeByName("Verse.PawnRenderTree");
                if (pawnRenderTreeType != null)
                {
                    var treeDrawMethod = AccessTools.Method(pawnRenderTreeType, "Draw");
                    if (treeDrawMethod != null)
                    {
                        harmony.Patch(treeDrawMethod, prefix: new HarmonyMethod(typeof(HarmonyPatches_EnclosedTurret), nameof(Prefix_PawnRenderTree_Draw)));
                    }
                }

                Log.Message("[AMC Enclosed Turret] Patched Pawn rendering methods for graphic hiding.");
            }
            catch (Exception ex)
            {
                Log.Warning($"[AMC Enclosed Turret] Graphic hiding patch skipped: {ex.Message}");
            }

            // 2. Center Pawn Name & Selection (Pawn.DrawPos)
            try
            {
                var drawPosProp = AccessTools.DeclaredPropertyGetter(typeof(Pawn), "DrawPos")
                                  ?? AccessTools.PropertyGetter(typeof(Thing), "DrawPos");
                if (drawPosProp != null)
                {
                    harmony.Patch(drawPosProp, prefix: new HarmonyMethod(typeof(HarmonyPatches_EnclosedTurret), nameof(Prefix_Pawn_DrawPos)));
                    Log.Message("[AMC Enclosed Turret] Patched Pawn.DrawPos for label re-centering.");
                }
            }
            catch (Exception ex)
            {
                Log.Warning($"[AMC Enclosed Turret] DrawPos patch skipped: {ex.Message}");
            }

            // 3. Fine-grained Damage & Hit Check Protection (Pawn.PreApplyDamage)
            try
            {
                var preApplyDamageMethod = AccessTools.DeclaredMethod(typeof(Pawn), "PreApplyDamage")
                                          ?? AccessTools.Method(typeof(Pawn), "PreApplyDamage");
                if (preApplyDamageMethod != null)
                {
                    harmony.Patch(preApplyDamageMethod, prefix: new HarmonyMethod(typeof(HarmonyPatches_EnclosedTurret), nameof(Prefix_Pawn_PreApplyDamage)));
                    Log.Message("[AMC Enclosed Turret] Patched Pawn.PreApplyDamage for fine-grained damage protection.");
                }
            }
            catch (Exception ex)
            {
                Log.Warning($"[AMC Enclosed Turret] PreApplyDamage patch skipped: {ex.Message}");
            }

            // 4. Fire Immunity (FireUtility.TryAttachFire)
            try
            {
                var tryAttachFireMethod = AccessTools.Method(typeof(FireUtility), "TryAttachFire");
                if (tryAttachFireMethod != null)
                {
                    harmony.Patch(tryAttachFireMethod, prefix: new HarmonyMethod(typeof(HarmonyPatches_EnclosedTurret), nameof(Prefix_FireUtility_TryAttachFire)));
                    Log.Message("[AMC Enclosed Turret] Patched FireUtility.TryAttachFire for fire immunity.");
                }
            }
            catch (Exception ex)
            {
                Log.Warning($"[AMC Enclosed Turret] FireUtility patch skipped: {ex.Message}");
            }

            // 5. Temperature Immunity (Thing.AmbientTemperature & HediffGivers)
            try
            {
                var ambTempProp = AccessTools.DeclaredPropertyGetter(typeof(Pawn), "AmbientTemperature")
                                  ?? AccessTools.PropertyGetter(typeof(Thing), "AmbientTemperature");
                if (ambTempProp != null)
                {
                    harmony.Patch(ambTempProp, prefix: new HarmonyMethod(typeof(HarmonyPatches_EnclosedTurret), nameof(Prefix_Pawn_AmbientTemperature)));
                    Log.Message("[AMC Enclosed Turret] Patched AmbientTemperature for temperature immunity.");
                }
            }
            catch (Exception ex)
            {
                Log.Warning($"[AMC Enclosed Turret] AmbientTemperature patch skipped: {ex.Message}");
            }

            try
            {
                var hypoType = AccessTools.TypeByName("RimWorld.HediffGiver_Hypothermia") ?? AccessTools.TypeByName("Verse.HediffGiver_Hypothermia");
                if (hypoType != null)
                {
                    var hypoMethod = AccessTools.Method(hypoType, "OnIntervalPassed");
                    if (hypoMethod != null)
                    {
                        harmony.Patch(hypoMethod, prefix: new HarmonyMethod(typeof(HarmonyPatches_EnclosedTurret), nameof(Prefix_HediffGiver_Temperature)));
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Warning($"[AMC Enclosed Turret] Hypothermia patch skipped: {ex.Message}");
            }

            try
            {
                var heatType = AccessTools.TypeByName("RimWorld.HediffGiver_Heatstroke") ?? AccessTools.TypeByName("Verse.HediffGiver_Heatstroke");
                if (heatType != null)
                {
                    var heatMethod = AccessTools.Method(heatType, "OnIntervalPassed");
                    if (heatMethod != null)
                    {
                        harmony.Patch(heatMethod, prefix: new HarmonyMethod(typeof(HarmonyPatches_EnclosedTurret), nameof(Prefix_HediffGiver_Temperature)));
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Warning($"[AMC Enclosed Turret] Heatstroke patch skipped: {ex.Message}");
            }

            // 6. CE Suppression Immunity (CompSuppressable)
            try
            {
                var suppType = AccessTools.TypeByName("CombatExtended.CompSuppressable");
                if (suppType != null)
                {
                    var canReactProp = AccessTools.PropertyGetter(suppType, "CanReactToSuppression");
                    if (canReactProp != null)
                    {
                        harmony.Patch(canReactProp, postfix: new HarmonyMethod(typeof(HarmonyPatches_EnclosedTurret), nameof(Postfix_CompSuppressable_CanReactToSuppression)));
                    }

                    var addSuppMethod = AccessTools.Method(suppType, "AddSuppression");
                    if (addSuppMethod != null)
                    {
                        harmony.Patch(addSuppMethod, prefix: new HarmonyMethod(typeof(HarmonyPatches_EnclosedTurret), nameof(Prefix_CompSuppressable_AddSuppression)));
                    }
                    Log.Message("[AMC Enclosed Turret] Patched CompSuppressable for CE suppression immunity.");
                }
            }
            catch (Exception ex)
            {
                Log.Warning($"[AMC Enclosed Turret] CompSuppressable patch skipped: {ex.Message}");
            }
        }

        private static readonly AccessTools.FieldRef<PawnRenderer, Pawn> PawnRendererPawnRef = AccessTools.FieldRefAccess<PawnRenderer, Pawn>("pawn");

        private static Pawn GetPawnFromRenderer(PawnRenderer renderer)
        {
            if (renderer == null) return null;
            try
            {
                return PawnRendererPawnRef(renderer);
            }
            catch
            {
                return null;
            }
        }

        // 1. Hide Pawn Graphic Prefixes
        public static bool Prefix_Pawn_DrawAt(Pawn __instance)
        {
            if (__instance != null && __instance.IsManningEnclosedTurret(out _, out CompEnclosedTurret comp) && comp.Props.hidePawnGraphics)
            {
                return false; // Skip drawing pawn visual graphics
            }
            return true;
        }

        public static bool Prefix_Pawn_DynamicDrawPhaseAt(Pawn __instance)
        {
            if (__instance != null && __instance.IsManningEnclosedTurret(out _, out CompEnclosedTurret comp) && comp.Props.hidePawnGraphics)
            {
                return false; // Skip drawing pawn visual graphics
            }
            return true;
        }

        public static bool Prefix_PawnRenderer_RenderPawnAt(PawnRenderer __instance)
        {
            Pawn pawn = GetPawnFromRenderer(__instance);
            if (pawn != null && pawn.IsManningEnclosedTurret(out _, out CompEnclosedTurret comp) && comp.Props.hidePawnGraphics)
            {
                return false; // Skip drawing pawn visual graphics
            }
            return true;
        }

        public static bool Prefix_PawnRenderer_DynamicDrawPhaseAt(PawnRenderer __instance)
        {
            Pawn pawn = GetPawnFromRenderer(__instance);
            if (pawn != null && pawn.IsManningEnclosedTurret(out _, out CompEnclosedTurret comp) && comp.Props.hidePawnGraphics)
            {
                return false; // Skip drawing pawn visual graphics
            }
            return true;
        }

        public static bool Prefix_PawnRenderTree_Draw(PawnDrawParms parms)
        {
            if (parms.pawn != null && parms.pawn.IsManningEnclosedTurret(out _, out CompEnclosedTurret comp) && comp.Props.hidePawnGraphics)
            {
                return false; // Skip drawing pawn visual graphics
            }
            return true;
        }

        // 2. Center Pawn Name Prefix
        public static bool Prefix_Pawn_DrawPos(Pawn __instance, ref Vector3 __result)
        {
            if (__instance != null && __instance.IsManningEnclosedTurret(out Building turret, out CompEnclosedTurret comp) && (comp.Props.centerPawnPosition || comp.Props.centerPawnName))
            {
                __result = turret.DrawPos;
                return false;
            }
            return true;
        }

        // 3. Fine-grained Damage Immunity Prefix
        public static bool Prefix_Pawn_PreApplyDamage(Pawn __instance, ref DamageInfo dinfo, out bool absorbed)
        {
            absorbed = false;
            if (__instance != null && __instance.IsManningEnclosedTurret(out _, out CompEnclosedTurret comp))
            {
                float protection = comp.Props.GetProtectionFor(dinfo.Def);

                if (protection >= 1.0f)
                {
                    absorbed = true;
                    return false; // Absorb incoming damage completely (100% protection)
                }
                else if (protection > 0.0f)
                {
                    float newAmount = dinfo.Amount * (1.0f - protection);
                    if (newAmount <= 0.0f)
                    {
                        absorbed = true;
                        return false;
                    }
                    dinfo.SetAmount(newAmount);
                }
            }
            return true;
        }

        // 4. Fire Immunity Prefix
        public static bool Prefix_FireUtility_TryAttachFire(Thing t)
        {
            if (t is Pawn pawn && pawn.IsManningEnclosedTurret(out _, out CompEnclosedTurret comp))
            {
                if (comp.Props.flameProtection >= 1.0f)
                {
                    return false; // Block catching fire if flame protection is 100%
                }
            }
            return true;
        }

        // 5. Temperature Hediff Giver Prefix
        public static bool Prefix_HediffGiver_Temperature(Pawn pawn)
        {
            if (pawn != null && pawn.IsManningEnclosedTurret(out _, out CompEnclosedTurret comp))
            {
                if (comp.Props.temperatureProtection >= 1.0f)
                {
                    return false; // Skip hypothermia / heatstroke tick if 100% protected
                }
            }
            return true;
        }

        // 5b. Ambient Temperature Prefix
        public static bool Prefix_Pawn_AmbientTemperature(Thing __instance, ref float __result)
        {
            if (__instance is Pawn pawn && pawn.IsManningEnclosedTurret(out _, out CompEnclosedTurret comp))
            {
                float tempProt = comp.Props.temperatureProtection;
                if (tempProt >= 1.0f)
                {
                    __result = 21f; // Room temperature (21°C)
                    return false;
                }
                else if (tempProt > 0.0f)
                {
                    __result = Mathf.Lerp(__result, 21f, tempProt);
                    return false;
                }
            }
            return true;
        }

        // 6. Suppression Immunity Postfix & Prefix
        public static void Postfix_CompSuppressable_CanReactToSuppression(ThingComp __instance, ref bool __result)
        {
            if (__result && __instance.parent is Pawn pawn && pawn.IsManningEnclosedTurret(out _, out _))
            {
                __result = false;
            }
        }

        public static bool Prefix_CompSuppressable_AddSuppression(ThingComp __instance)
        {
            if (__instance.parent is Pawn pawn && pawn.IsManningEnclosedTurret(out _, out _))
            {
                return false; // Skip AddSuppression
            }
            return true;
        }
    }
}
