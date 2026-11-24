using System;
using CombatExtended;
using HarmonyLib;
using RimWorld;
using Verse;

namespace AbsolutelyMoreCannons
{
    /// <summary>
    /// DefModExtension for turret buildings or turret weapons equipped with high-magnification optics.
    /// Removes or scales down CE's ambient lighting accuracy penalty unless the target is in pitch darkness.
    /// </summary>
    public class TurretLightPenaltyExtension : DefModExtension
    {
        /// <summary>
        /// Glow threshold below which the target is considered in pitch darkness (0.0 - 1.0).
        /// </summary>
        public float pitchDarkGlowThreshold = 0.15f;

        /// <summary>
        /// Multiplier applied to ShiftVecReport.lightingShift when glow >= pitchDarkGlowThreshold.
        /// </summary>
        public float litPenaltyMultiplier = 0f;

        /// <summary>
        /// Multiplier applied to ShiftVecReport.lightingShift when glow &lt; pitchDarkGlowThreshold.
        /// </summary>
        public float darkPenaltyMultiplier = 1f;
    }

    [StaticConstructorOnStartup]
    public static class HarmonyPatches_LightPenalty
    {
        static HarmonyPatches_LightPenalty()
        {
            var harmony = new Harmony("AbsolutelyMoreCannons.LightPenalty");
            var method = AccessTools.Method(
                typeof(Verb_LaunchProjectileCE),
                nameof(Verb_LaunchProjectileCE.ShiftVecReportFor),
                new[] { typeof(LocalTargetInfo), typeof(IntVec3) });

            if (method != null)
            {
                harmony.Patch(
                    original: method,
                    postfix: new HarmonyMethod(typeof(HarmonyPatches_LightPenalty), nameof(Postfix_ShiftVecReportFor)));
            }
            else
            {
                Log.Warning("[AMC] Could not find Verb_LaunchProjectileCE.ShiftVecReportFor(LocalTargetInfo, IntVec3) for light penalty adjustment.");
            }
        }

        public static void Postfix_ShiftVecReportFor(Verb_LaunchProjectileCE __instance, IntVec3 targetCell, ref ShiftVecReport __result)
        {
            if (__instance == null || __result == null || __instance.caster == null || __instance.caster.Map == null)
            {
                return;
            }

            TurretLightPenaltyExtension ext =
                __instance.EquipmentSource?.def?.GetModExtension<TurretLightPenaltyExtension>()
                ?? __instance.caster.def?.GetModExtension<TurretLightPenaltyExtension>();

            if (ext == null)
            {
                return;
            }

            var tracker = __instance.caster.Map.GetLightingTracker();
            if (tracker == null)
            {
                return;
            }

            float glow = tracker.CombatGlowAtFor(__instance.caster.Position, targetCell);
            __result.lightingShift *= (glow >= ext.pitchDarkGlowThreshold)
                ? ext.litPenaltyMultiplier
                : ext.darkPenaltyMultiplier;
        }
    }
}
