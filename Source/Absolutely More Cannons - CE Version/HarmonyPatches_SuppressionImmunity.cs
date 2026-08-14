using HarmonyLib;
using CombatExtended;
using Verse;
using RimWorld;

namespace AbsolutelyMoreCannons
{
    /// <summary>
    /// Utility and Harmony patches to grant suppression immunity to pawns
    /// while operating manned turrets configured with TurretSuppressionImmunityExtension.
    /// </summary>
    public static class TurretSuppressionImmunityUtility
    {
        public static bool IsManningImmuneTurret(Pawn pawn)
        {
            if (pawn == null || !pawn.Spawned || pawn.CurJobDef != JobDefOf.ManTurret) return false;

            Thing turret = pawn.CurJob?.targetA.Thing;
            if (turret == null) return false;

            var ext = turret.def?.GetModExtension<TurretSuppressionImmunityExtension>();
            return ext != null && ext.preventOperatorSuppression;
        }
    }

    /// <summary>
    /// Postfix patch on CompSuppressable.CanReactToSuppression.
    /// Disables suppression reactions for pawns manning an immune turret.
    /// </summary>
    [HarmonyPatch(typeof(CompSuppressable), nameof(CompSuppressable.CanReactToSuppression), MethodType.Getter)]
    public static class Patch_CompSuppressable_CanReactToSuppression
    {
        [HarmonyPostfix]
        public static void Postfix(CompSuppressable __instance, ref bool __result)
        {
            if (__result && __instance.parent is Pawn pawn)
            {
                if (TurretSuppressionImmunityUtility.IsManningImmuneTurret(pawn))
                {
                    __result = false;
                }
            }
        }
    }

    /// <summary>
    /// Prefix patch on CompSuppressable.AddSuppression.
    /// Completely blocks suppression accumulation for pawns manning an immune turret.
    /// </summary>
    [HarmonyPatch(typeof(CompSuppressable), nameof(CompSuppressable.AddSuppression))]
    public static class Patch_CompSuppressable_AddSuppression
    {
        [HarmonyPrefix]
        public static bool Prefix(CompSuppressable __instance, float amount, IntVec3 origin)
        {
            if (__instance.parent is Pawn pawn && TurretSuppressionImmunityUtility.IsManningImmuneTurret(pawn))
            {
                var settings = TurretBarrelAnimationMod.settings;
                if (settings != null && settings.logTurretTarget)
                {
                    AMCLogger.LogTurretTarget($"[Suppression Immunity] Suppressed shot near operator {pawn.LabelCap} ignored while manning {pawn.CurJob?.targetA.Thing?.LabelCap}");
                }
                return false; // Skip original AddSuppression
            }
            return true;
        }
    }
}
