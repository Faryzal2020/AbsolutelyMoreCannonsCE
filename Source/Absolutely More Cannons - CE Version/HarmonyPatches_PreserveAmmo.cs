using HarmonyLib;
using CombatExtended;
using Verse;
using RimWorld;

namespace AbsolutelyMoreCannons
{
    /// <summary>
    /// Harmony patch for Verb_LaunchProjectileCE.TryCastShot.
    /// Intercepts burst shots on downed pawns when Preserve Ammo is enabled,
    /// aborting the burst if no standing hostiles are in the line of fire.
    /// </summary>
    [HarmonyPatch(typeof(Verb_LaunchProjectileCE), nameof(Verb_LaunchProjectileCE.TryCastShot))]
    public static class Patch_Verb_LaunchProjectileCE_PreserveAmmo
    {
        private static readonly System.Reflection.MethodInfo retargetMethod =
            AccessTools.Method(typeof(Verb_LaunchProjectileCE), "Retarget");

        [HarmonyPrefix]
        public static bool Prefix(Verb_LaunchProjectileCE __instance, ref bool __result)
        {
            if (__instance == null || __instance.caster == null) return true;

            Thing caster = __instance.caster;

            // 1. Determine if Preserve Ammo is active for this caster
            bool isEnabled = false;
            var preserveComp = caster.TryGetComp<CompTurretPreserveAmmo>();
            if (preserveComp != null)
            {
                isEnabled = preserveComp.preserveAmmo;
            }
            else
            {
                var ext = caster.def?.GetModExtension<TurretPreserveAmmoExtension>();
                if (ext != null)
                {
                    isEnabled = ext.defaultPreserveAmmo;
                }
            }

            if (!isEnabled) return true; // Normal CE firing

            // 2. Ignore during Suppress Fire aim mode
            if (__instance.CompFireModes?.CurrentAimMode == AimMode.SuppressFire) return true;

            // 3. Check if target is a downed pawn
            LocalTargetInfo target = __instance.CurrentTarget;
            if (target.Pawn != null && target.Pawn.Downed)
            {
                // Attempt mid-burst retargeting first via CE's Retarget()
                bool retargeted = false;
                if (retargetMethod != null)
                {
                    retargeted = (bool)retargetMethod.Invoke(__instance, null);
                }

                // If retargeting successfully locked onto a non-downed target, continue firing
                if (retargeted && __instance.CurrentTarget.Pawn != null && !__instance.CurrentTarget.Pawn.Downed)
                {
                    return true;
                }

                // If target remains a downed pawn, abort this shot to save ammo
                if (__instance.CurrentTarget.Pawn != null && __instance.CurrentTarget.Pawn.Downed)
                {
                    var settings = TurretBarrelAnimationMod.settings;
                    if (settings != null && settings.logTurretTarget)
                    {
                        AMCLogger.LogTurretTarget($"[Preserve Ammo] {caster.LabelCap} @ {caster.Position} aborted shot on downed target ({__instance.CurrentTarget.Pawn.LabelCap})");
                    }
                    __result = false;
                    return false; // Skip original TryCastShot, stopping the burst
                }
            }

            return true;
        }
    }
}
