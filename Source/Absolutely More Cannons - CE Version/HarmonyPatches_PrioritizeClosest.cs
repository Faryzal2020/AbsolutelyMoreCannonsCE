using System;
using CombatExtended;
using HarmonyLib;
using RimWorld;
using Verse;

namespace AbsolutelyMoreCannons
{
    /// <summary>
    /// Harmony patches for "Prioritize Closest" smart target acquisition and between-burst target switching.
    /// Ensures target switches NEVER happen during a burst, respects the 4-second post-alignment commitment window,
    /// and evaluates candidates within the dynamic focusing/widening vision cone using distance + kinematic traverse time.
    /// </summary>
    [StaticConstructorOnStartup]
    public static class HarmonyPatches_PrioritizeClosest
    {
        static HarmonyPatches_PrioritizeClosest()
        {
            var harmony = new Harmony("AbsolutelyMoreCannons.PrioritizeClosest");

            // 1. Patch Building_TurretGunCE.TryStartShootSomething:
            //    Called when AttackVerb.state != VerbState.Bursting and burstCooldownTicksLeft <= 0.
            //    In vanilla CE, TryStartShootSomething only calls TryFindNewTarget() if !currentTargetInt.IsValid,
            //    meaning a turret will keep shooting its old far target after a burst unless we re-evaluate here!
            var tryStartShootMethod = AccessTools.Method(typeof(Building_TurretGunCE), nameof(Building_TurretGunCE.TryStartShootSomething));
            if (tryStartShootMethod != null)
            {
                harmony.Patch(
                    original: tryStartShootMethod,
                    prefix: new HarmonyMethod(typeof(HarmonyPatches_PrioritizeClosest), nameof(Prefix_Building_TurretGunCE_TryStartShootSomething)));
            }

            // 2. Patch Building_TurretGunCE.TryFindNewTarget:
            //    Replaces CE's random score-weighted target selection when Prioritize Closest is active.
            var tryFindNewTargetMethod = AccessTools.Method(typeof(Building_TurretGunCE), nameof(Building_TurretGunCE.TryFindNewTarget));
            if (tryFindNewTargetMethod != null)
            {
                harmony.Patch(
                    original: tryFindNewTargetMethod,
                    prefix: new HarmonyMethod(typeof(HarmonyPatches_PrioritizeClosest), nameof(Prefix_Building_TurretGunCE_TryFindNewTarget)));
            }

            // 3. Patch Verb_LaunchProjectileCE.TryCastShot Postfix:
            //    Marks target alignment completed as soon as the first shot of a burst is fired at the target.
            var tryCastShotMethod = AccessTools.Method(typeof(Verb_LaunchProjectileCE), nameof(Verb_LaunchProjectileCE.TryCastShot));
            if (tryCastShotMethod != null)
            {
                harmony.Patch(
                    original: tryCastShotMethod,
                    postfix: new HarmonyMethod(typeof(HarmonyPatches_PrioritizeClosest), nameof(Postfix_Verb_LaunchProjectileCE_TryCastShot)));
            }
        }

        private static bool ShouldRunPrioritizeClosest(Building_TurretGunCE turret, out CompTurretPrioritizeClosest comp, out Verb verb)
        {
            comp = null;
            verb = null;

            if (turret == null || !turret.Spawned || turret.Map == null)
                return false;

            // Never interfere with world-map bombardment targeting
            if (turret.targetingWorldMap || turret.globalTargetInfo.IsValid)
                return false;

            // Respect player forced target
            if (turret.ForcedTarget.IsValid)
                return false;

            comp = turret.TryGetComp<CompTurretPrioritizeClosest>();
            if (comp == null || !comp.IsEffectivelyActive)
                return false;

            verb = turret.AttackVerb;
            if (verb == null)
                return false;

            // NEVER switch targets during an active burst
            if (verb.state == VerbState.Bursting)
                return false;

            // Bypass during SuppressFire mode
            if (turret.CompFireModes?.CurrentAimMode == AimMode.SuppressFire)
                return false;

            return true;
        }

        /// <summary>
        /// Runs right before Building_TurretGunCE starts warming up for a new burst (between bursts, when not bursting and cooldown == 0).
        /// If the turret already has a valid currentTargetInt:
        /// - If still within the 4s post-alignment commitment window, keeps currentTargetInt locked.
        /// - If the commitment window has expired, checks if a visibly closer target in the 90-degree (45-deg half-angle) focus cone
        ///   is worth switching to (factoring in distance + traverse time).
        /// - If currentTargetInt is dead/downed/invalid/lost LoS, clears currentTargetInt so TryFindNewTarget will acquire the best target in the cone.
        /// </summary>
        public static void Prefix_Building_TurretGunCE_TryStartShootSomething(Building_TurretGunCE __instance)
        {
            if (!ShouldRunPrioritizeClosest(__instance, out CompTurretPrioritizeClosest comp, out Verb verb))
                return;

            LocalTargetInfo curTarget = __instance.currentTargetInt;
            if (!curTarget.IsValid)
                return;

            // If current target died, went down (with Preserve Ammo), or lost LoS/range, clear it so TryFindNewTarget runs cleanly
            if (!comp.IsTargetValidAndAlive(__instance, verb, curTarget, checkLoS: true))
            {
                __instance.currentTargetInt = LocalTargetInfo.Invalid;
                return;
            }

            // Re-evaluate between bursts: if commitment expired and a closer target in the focus cone is worth switching to, update currentTargetInt
            if (comp.TryEvaluateSmartTarget(__instance, verb, curTarget, out LocalTargetInfo chosenTarget, out _))
            {
                if (chosenTarget.IsValid && chosenTarget != curTarget)
                {
                    __instance.currentTargetInt = chosenTarget;
                }
            }
        }

        /// <summary>
        /// Overrides Building_TurretGunCE.TryFindNewTarget when Prioritize Closest is enabled.
        /// Uses our dynamic vision cone (45° half-angle focus, widening +10°/s when idle) and effective cost (distance + traverse time).
        /// </summary>
        public static bool Prefix_Building_TurretGunCE_TryFindNewTarget(Building_TurretGunCE __instance, ref LocalTargetInfo __result)
        {
            if (!ShouldRunPrioritizeClosest(__instance, out CompTurretPrioritizeClosest comp, out Verb verb))
                return true;

            if (!verb.Available())
            {
                __result = LocalTargetInfo.Invalid;
                return false;
            }

            LocalTargetInfo curTarget = __instance.currentTargetInt;
            if (comp.TryEvaluateSmartTarget(__instance, verb, curTarget, out LocalTargetInfo chosenTarget, out _))
            {
                __result = chosenTarget;
                return false; // Skip vanilla CE random score target finder
            }

            __result = LocalTargetInfo.Invalid;
            return false;
        }

        public static void Postfix_Verb_LaunchProjectileCE_TryCastShot(Verb_LaunchProjectileCE __instance, bool __result)
        {
            if (!__result || __instance?.caster == null)
                return;

            if (__instance.caster is Building_Turret turret && turret.Spawned)
            {
                var comp = turret.TryGetComp<CompTurretPrioritizeClosest>();
                if (comp != null && comp.IsEffectivelyActive)
                {
                    comp.Notify_ShotFiredAt(turret, __instance.CurrentTarget);
                }
            }
        }
    }
}
