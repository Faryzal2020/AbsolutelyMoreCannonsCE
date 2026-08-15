using HarmonyLib;
using CombatExtended;
using Verse;
using RimWorld;

namespace AbsolutelyMoreCannons
{
    /// <summary>
    /// Utility class to evaluate mid-burst continuous tracking eligibility.
    /// </summary>
    public static class TurretTrackingUtility
    {
        public static bool CanTrackMidBurst(Thing caster)
        {
            if (caster == null) return false;

            // 1. Check if caster's ThingDef has TurretTrackingExtension
            var trackingExt = caster.def?.GetModExtension<TurretTrackingExtension>();
            if (trackingExt != null && trackingExt.enableMidBurstTracking)
            {
                return true;
            }

            // 2. Check if caster has CompTurretFCS with trackingAbility
            var fcsComp = caster.TryGetComp<CompTurretFCS>();
            if (fcsComp != null && fcsComp.HasFCS && fcsComp.ActiveStats != null && fcsComp.ActiveStats.trackingAbility)
            {
                return true;
            }

            return false;
        }
    }

    /// <summary>
    /// Harmony patch for Verb_LaunchProjectileCE.LockRotationAndAngle getter.
    /// Unlocks rotation & angle calculation mid-burst when tracking is enabled.
    /// </summary>
    [HarmonyPatch(typeof(Verb_LaunchProjectileCE), "LockRotationAndAngle", MethodType.Getter)]
    public static class Patch_Verb_LaunchProjectileCE_LockRotationAndAngle
    {
        [HarmonyPostfix]
        public static void Postfix(Verb_LaunchProjectileCE __instance, ref bool __result)
        {
            if (__result && __instance != null && __instance.caster != null)
            {
                if (TurretTrackingUtility.CanTrackMidBurst(__instance.caster))
                {
                    __result = false;
                }
            }
        }
    }

    /// <summary>
    /// Harmony patch for Building_TurretGunCE.Tick.
    /// Updates turret top rotation tick-by-tick while firing if tracking is enabled.
    /// </summary>
    [HarmonyPatch(typeof(Building_TurretGunCE), nameof(Building_TurretGunCE.Tick))]
    public static class Patch_Building_TurretGunCE_Tick_TargetTracking
    {
        [HarmonyPostfix]
        public static void Postfix(Building_TurretGunCE __instance)
        {
            if (__instance != null && __instance.Spawned && __instance.Active)
            {
                var verb = __instance.AttackVerb;
                if (verb != null && verb.state == VerbState.Bursting)
                {
                    if (TurretTrackingUtility.CanTrackMidBurst(__instance))
                    {
                        __instance.top?.TurretTopTick();
                    }
                }
            }
        }
    }
}
