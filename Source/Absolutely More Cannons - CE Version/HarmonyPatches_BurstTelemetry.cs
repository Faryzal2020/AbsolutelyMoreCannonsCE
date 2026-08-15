using System;
using HarmonyLib;
using CombatExtended;
using Verse;
using RimWorld;

namespace AbsolutelyMoreCannons
{
    /// <summary>
    /// Telemetry and Harmony patches to log turret burst lifecycle events:
    /// - Burst Start (Target state & total burst shot count)
    /// - Shot Fired (Current shot index out of total burst count & current target state)
    /// - Burst End (Shots completed vs total & final target state)
    /// </summary>
    public static class BurstTelemetryUtility
    {
        public static string GetTargetStateString(LocalTargetInfo target)
        {
            if (!target.IsValid) return "No Target / Invalid";

            if (target.Thing is Pawn pawn)
            {
                if (pawn.Dead) return $"Pawn '{pawn.LabelCap}' [Dead]";
                if (!pawn.Spawned) return $"Pawn '{pawn.LabelCap}' [Despawned/Gone]";
                if (pawn.Downed) return $"Pawn '{pawn.LabelCap}' [Downed]";
                return $"Pawn '{pawn.LabelCap}' [Alive/Standing]";
            }

            if (target.Thing != null)
            {
                if (target.Thing.Destroyed || !target.Thing.Spawned)
                    return $"Thing '{target.Thing.LabelCap}' [Destroyed/Gone]";
                return $"Thing '{target.Thing.LabelCap}' [Active]";
            }

            return $"Cell ({target.Cell.x}, {target.Cell.z})";
        }
    }

    /// <summary>
    /// Postfix patch on Building_TurretGunCE.BeginBurst to log burst start telemetry.
    /// </summary>
    [HarmonyPatch(typeof(Building_TurretGunCE), nameof(Building_TurretGunCE.BeginBurst))]
    public static class Patch_Building_TurretGunCE_BeginBurst
    {
        [HarmonyPostfix]
        public static void Postfix(Building_TurretGunCE __instance)
        {
            var settings = TurretBarrelAnimationMod.settings;
            if (settings == null || (!settings.logTurretTarget && !settings.logTurretFireTimestamp))
                return;

            if (__instance == null || !__instance.Spawned) return;

            var verb = __instance.AttackVerb as Verb_LaunchProjectileCE;
            if (verb == null) return;

            int totalShots = verb.ShotsPerBurst;
            string targetState = BurstTelemetryUtility.GetTargetStateString(verb.CurrentTarget);

            AMCLogger.LogTurretTarget(
                $"[Burst Start] Turret: {__instance.LabelCap} @ {__instance.Position} | " +
                $"Target: {targetState} | Burst Count: {totalShots} shots"
            );
        }
    }

    /// <summary>
    /// Postfix patch on Verb_LaunchProjectileCE.TryCastShot to log per-shot progress telemetry.
    /// </summary>
    [HarmonyPatch(typeof(Verb_LaunchProjectileCE), nameof(Verb_LaunchProjectileCE.TryCastShot))]
    public static class Patch_Verb_LaunchProjectileCE_ShotTelemetry
    {
        private static readonly AccessTools.FieldRef<Verb_LaunchProjectileCE, int> numShotsFiredRef =
            AccessTools.FieldRefAccess<Verb_LaunchProjectileCE, int>("numShotsFired");

        [HarmonyPostfix]
        public static void Postfix(Verb_LaunchProjectileCE __instance, bool __result)
        {
            var settings = TurretBarrelAnimationMod.settings;
            if (settings == null || (!settings.logTurretTarget && !settings.logTurretFireTimestamp))
                return;

            if (__instance == null || __instance.caster == null || !__instance.caster.Spawned) return;

            // Only log if the caster is a turret
            if (!(__instance.caster is Building_Turret)) return;

            if (__result) // Shot was successfully fired
            {
                int currentShot = numShotsFiredRef(__instance); // incremented by CE on successful shot
                int totalShots = __instance.ShotsPerBurst;
                string targetState = BurstTelemetryUtility.GetTargetStateString(__instance.CurrentTarget);

                AMCLogger.LogTurretTarget(
                    $"[Burst Shot {currentShot}/{totalShots}] Turret: {__instance.caster.LabelCap} | " +
                    $"Target: {targetState}"
                );
            }
        }
    }

    /// <summary>
    /// Prefix patch on Building_TurretGunCE.BurstComplete to log burst end telemetry.
    /// </summary>
    [HarmonyPatch(typeof(Building_TurretGunCE), nameof(Building_TurretGunCE.BurstComplete))]
    public static class Patch_Building_TurretGunCE_BurstComplete
    {
        private static readonly AccessTools.FieldRef<Verb_LaunchProjectileCE, int> numShotsFiredRef =
            AccessTools.FieldRefAccess<Verb_LaunchProjectileCE, int>("numShotsFired");

        [HarmonyPrefix]
        public static void Prefix(Building_TurretGunCE __instance)
        {
            var settings = TurretBarrelAnimationMod.settings;
            if (settings == null || (!settings.logTurretTarget && !settings.logTurretFireTimestamp))
                return;

            if (__instance == null || !__instance.Spawned) return;

            var verb = __instance.AttackVerb as Verb_LaunchProjectileCE;
            if (verb == null) return;

            int shotsFired = numShotsFiredRef(verb);
            int totalShots = verb.ShotsPerBurst;
            string targetState = BurstTelemetryUtility.GetTargetStateString(verb.CurrentTarget);
            string status = shotsFired >= totalShots ? "Completed" : "Aborted / Interrupted";

            AMCLogger.LogTurretTarget(
                $"[Burst End] Turret: {__instance.LabelCap} | Shots Fired: {shotsFired}/{totalShots} | " +
                $"Target State: {targetState} | Status: {status}"
            );
        }
    }
}
