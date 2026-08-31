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

        [HarmonyPostfix]
        public static void Postfix(Verb_LaunchProjectileCE __instance, bool __result)
        {
            if (!__result || __instance == null || __instance.caster == null) return;

            Thing caster = __instance.caster;

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

            if (targetDownedOrDead || reachedShotThreshold)
            {
                if (sprayComp.TryGetNextConeTarget(__instance, curTarget, out LocalTargetInfo newTarget))
                {
                    // Update verb target
                    if (verbTargetField != null)
                    {
                        verbTargetField.SetValue(__instance, newTarget);
                    }

                    // Update turret building target if caster is a turret
                    if (caster is Building_Turret turret)
                    {
                        FieldInfo turretTargetField = HarmonyPatches.GetCurrentTargetField(turret.GetType());
                        if (turretTargetField != null)
                        {
                            turretTargetField.SetValue(turret, newTarget);
                        }
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
            }

            // 5. Continuous Trigger Hold / Burst Count Override based on Rolling Target Cone
            LocalTargetInfo updatedTarget = __instance.CurrentTarget;
            bool hasConeTargets = sprayComp.HasAnyConeTarget(__instance, updatedTarget);

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
                        burstShotsLeftField.SetValue(__instance, 0);
                    }
                }
            }
        }
    }
}
