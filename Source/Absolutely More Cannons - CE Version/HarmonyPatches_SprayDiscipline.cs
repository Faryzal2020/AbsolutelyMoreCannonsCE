using System.Reflection;
using CombatExtended;
using HarmonyLib;
using RimWorld;
using Verse;

namespace AbsolutelyMoreCannons
{
    /// <summary>
    /// Harmony patch for Verb_LaunchProjectileCE.TryCastShot.
    /// Manages rotary spray discipline (mid-burst target chaining across a cone).
    /// </summary>
    [HarmonyPatch(typeof(Verb_LaunchProjectileCE), nameof(Verb_LaunchProjectileCE.TryCastShot))]
    public static class Patch_Verb_LaunchProjectileCE_SprayDiscipline
    {
        private static readonly FieldInfo verbTargetField = AccessTools.Field(typeof(Verb), "currentTarget");
        private static readonly FieldInfo burstShotsLeftField = AccessTools.Field(typeof(Verb), "burstShotsLeft");

        private static void ApplyTargetSwitch(Verb_LaunchProjectileCE verb, Thing caster, LocalTargetInfo newTarget)
        {
            if (verbTargetField != null)
            {
                verbTargetField.SetValue(verb, newTarget);
            }

            if (caster is Building_Turret turret)
            {
                FieldInfo turretTargetField = HarmonyPatches.GetCurrentTargetField(turret.GetType());
                if (turretTargetField != null)
                {
                    turretTargetField.SetValue(turret, newTarget);
                }
            }
        }

        private static void SyncTurretTargetWithVerb(Verb_LaunchProjectileCE verb, Thing caster)
        {
            if (verb == null || !(caster is Building_Turret turret)) return;
            LocalTargetInfo verbTarget = verb.CurrentTarget;
            if (!verbTarget.IsValid) return;

            if (turret.CurrentTarget != verbTarget)
            {
                FieldInfo turretTargetField = HarmonyPatches.GetCurrentTargetField(turret.GetType());
                if (turretTargetField != null)
                {
                    turretTargetField.SetValue(turret, verbTarget);
                }
            }
        }

        /// <summary>
        /// Pre-shot check: if the current target died, despawned, went down, or lost LoS while projectiles
        /// were in flight between shots, immediately cycle to the next valid hostile inside the Spray Discipline
        /// cone BEFORE PreserveAmmo or CE's 3-cell Retarget() / ShootLine check can fail and abort the burst.
        /// </summary>
        [HarmonyPrefix]
        [HarmonyPriority(Priority.High)]
        public static bool Prefix(Verb_LaunchProjectileCE __instance, ref bool __result)
        {
            if (__instance == null || __instance.caster == null) return true;

            Thing caster = __instance.caster;
            var sprayComp = caster.TryGetComp<CompTurretSprayDiscipline>();
            if (sprayComp == null || !sprayComp.sprayDisciplineEnabled) return true;

            if (__instance.CompFireModes?.CurrentAimMode == AimMode.SuppressFire) return true;

            LocalTargetInfo curTarget = __instance.CurrentTarget;
            bool targetDeadOrGone = !curTarget.IsValid
                                 || curTarget.ThingDestroyed
                                 || (curTarget.HasThing && (curTarget.Thing == null || !curTarget.Thing.Spawned))
                                 || (curTarget.Pawn != null && curTarget.Pawn.Dead);
            bool targetDowned = !targetDeadOrGone && curTarget.Pawn != null && curTarget.Pawn.Downed;
            bool targetLostLoS = !targetDeadOrGone && !targetDowned && !__instance.CanHitTarget(curTarget);

            if (targetDeadOrGone || targetDowned || targetLostLoS)
            {
                int shotsOnPrevTarget = sprayComp.shotsFiredAtCurrentTarget;
                string switchWhy = targetDeadOrGone
                    ? "Pre-Shot: Target Dead"
                    : targetDowned
                        ? "Pre-Shot: Target Downed"
                        : "Pre-Shot: LoS Lost";

                if (sprayComp.TryGetNextConeTarget(__instance, curTarget, out LocalTargetInfo newTarget))
                {
                    ApplyTargetSwitch(__instance, caster, newTarget);
                    sprayComp.ResetShotCount();

                    if (caster is Building_Turret turret)
                    {
                        turret.Map?.GetComponent<MapComponent_TurretTargetingOverlay>()
                            ?.NotifySprayDisciplineSwitch(turret, curTarget, newTarget, switchWhy, shotsOnPrevTarget);
                    }

                    var settings = TurretBarrelAnimationMod.settings;
                    if (settings != null && settings.logTurretTarget)
                    {
                        string targetName = newTarget.HasThing ? newTarget.Thing.LabelCap.ToString() : newTarget.Cell.ToString();
                        float targetAngle = (newTarget.Cell - caster.Position).AngleFlat;
                        AMCLogger.LogTurretTarget($"[Spray Discipline Pre-Shot] {caster.LabelCap} @ {caster.Position} retargeted ({switchWhy}) mid-burst to: {targetName} @ {newTarget.Cell} (Heading: {targetAngle:F1}°)");
                    }

                    return true;
                }
                else
                {
                    // Current target is dead/downed/unhittable AND no standing hostiles remain in the cone:
                    // release trigger cleanly before wasting a shot or hitting a CE pre-shot abort.
                    SprayConeDiagReport coneReport = sprayComp.EvaluateCone(__instance, curTarget);
                    if (burstShotsLeftField != null)
                    {
                        burstShotsLeftField.SetValue(__instance, 0);
                    }

                    if (caster is Building_Turret sprayTurret && sprayTurret.Map != null)
                    {
                        sprayTurret.Map.GetComponent<MapComponent_TurretTargetingOverlay>()
                            ?.NotifySprayDisciplineTriggerRelease(sprayTurret, __instance, curTarget, coneReport);
                    }

                    var settings = TurretBarrelAnimationMod.settings;
                    if (settings != null && settings.logTurretTarget)
                    {
                        AMCLogger.LogTurretTarget($"[Spray Discipline Pre-Shot] {caster.LabelCap} @ {caster.Position} releasing trigger ({switchWhy} and no standing hostiles in cone)");
                    }

                    __result = false;
                    return false;
                }
            }

            return true;
        }

        [HarmonyPostfix]
        public static void Postfix(Verb_LaunchProjectileCE __instance, bool __result)
        {
            if (!__result || __instance == null || __instance.caster == null) return;

            Thing caster = __instance.caster;

            // Keep Turret Target synced if CE's native Retarget() updated Verb Target
            SyncTurretTargetWithVerb(__instance, caster);

            // 1. Get Spray Discipline component
            var sprayComp = caster.TryGetComp<CompTurretSprayDiscipline>();
            if (sprayComp == null || !sprayComp.sprayDisciplineEnabled) return;

            // 2. Bypass when CE Aim Mode is SuppressFire
            if (__instance.CompFireModes?.CurrentAimMode == AimMode.SuppressFire) return;

            // 3. Track shot count
            LocalTargetInfo curTarget = __instance.CurrentTarget;
            sprayComp.Notify_ShotFired(curTarget);

            // 4. Check if we should attempt dynamic target cycling
            bool targetDownedOrDead = curTarget.Pawn != null && (curTarget.Pawn.Dead || curTarget.Pawn.Downed);
            bool reachedShotThreshold = sprayComp.shotsFiredAtCurrentTarget >= sprayComp.ShotsPerTarget;
            int shotsOnPrevTarget = sprayComp.shotsFiredAtCurrentTarget;

            if (targetDownedOrDead || reachedShotThreshold)
            {
                if (sprayComp.TryGetNextConeTarget(__instance, curTarget, out LocalTargetInfo newTarget))
                {
                    string switchWhy = targetDownedOrDead
                        ? (curTarget.Pawn.Dead ? "Target Dead" : "Target Downed")
                        : $"Quota {shotsOnPrevTarget}/{sprayComp.ShotsPerTarget}";

                    ApplyTargetSwitch(__instance, caster, newTarget);

                    if (caster is Building_Turret turret)
                    {
                        turret.Map?.GetComponent<MapComponent_TurretTargetingOverlay>()
                            ?.NotifySprayDisciplineSwitch(turret, curTarget, newTarget, switchWhy, shotsOnPrevTarget);
                    }

                    sprayComp.ResetShotCount();

                    var settings = TurretBarrelAnimationMod.settings;
                    if (settings != null && settings.logTurretTarget)
                    {
                        string targetName = newTarget.HasThing ? newTarget.Thing.LabelCap.ToString() : newTarget.Cell.ToString();
                        float targetAngle = (newTarget.Cell - caster.Position).AngleFlat;
                        AMCLogger.LogTurretTarget($"[Spray Discipline] {caster.LabelCap} @ {caster.Position} cycled target mid-burst to: {targetName} @ {newTarget.Cell} (Target Heading: {targetAngle:F1}°, 0°=North, 90°=East)");
                    }
                }
                else if (caster is Building_Turret failedCycleTurret)
                {
                    string failWhy = targetDownedOrDead
                        ? (curTarget.Pawn.Dead ? "Target Dead" : "Target Downed")
                        : $"Quota {shotsOnPrevTarget}/{sprayComp.ShotsPerTarget}";
                    failedCycleTurret.Map?.GetComponent<MapComponent_TurretTargetingOverlay>()
                        ?.NotifySprayDisciplineNoNextTarget(failedCycleTurret, curTarget, failWhy);
                }
            }

            // 5. Continuous Trigger Hold / Burst Count Override based on Rolling Target Cone
            LocalTargetInfo updatedTarget = __instance.CurrentTarget;
            SprayConeDiagReport coneReport = sprayComp.EvaluateCone(__instance, updatedTarget);
            bool hasConeTargets = coneReport.ValidInConeCount > 0;

            if (hasConeTargets)
            {
                // Enemies exist within rolling cone: top up burstShotsLeft to keep trigger held continuously
                if (burstShotsLeftField != null)
                {
                    int curLeft = (int)burstShotsLeftField.GetValue(__instance);
                    if (curLeft <= 2)
                    {
                        burstShotsLeftField.SetValue(__instance, 10);
                    }
                }
            }
            else
            {
                // No hostiles in rolling cone: force burst termination (release trigger & enter cooldown)
                if (burstShotsLeftField != null)
                {
                    int curLeft = (int)burstShotsLeftField.GetValue(__instance);
                    if (curLeft > 0)
                    {
                        var settings = TurretBarrelAnimationMod.settings;
                        if (settings != null && settings.logTurretTarget)
                        {
                            float lastAngle = (updatedTarget.Cell - caster.Position).AngleFlat;
                            AMCLogger.LogTurretTarget($"[Spray Discipline] {caster.LabelCap} @ {caster.Position} releasing trigger (No standing hostiles in rolling cone relative to last heading {lastAngle:F1}°)");
                        }
                        if (caster is Building_Turret sprayTurret && sprayTurret.Map != null)
                        {
                            sprayTurret.Map.GetComponent<MapComponent_TurretTargetingOverlay>()
                                ?.NotifySprayDisciplineTriggerRelease(sprayTurret, __instance, updatedTarget, coneReport);
                        }
                        burstShotsLeftField.SetValue(__instance, 0);
                    }
                }
            }
        }
    }
}
