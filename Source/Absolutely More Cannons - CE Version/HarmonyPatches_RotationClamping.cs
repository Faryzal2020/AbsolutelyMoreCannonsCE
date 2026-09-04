using System;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Verse;
using RimWorld;
using UnityEngine;

namespace AbsolutelyMoreCannons
{
    /// <summary>
    /// Partial class for rotation clamping patches
    /// </summary>
    public static partial class HarmonyPatches
    {
        // Counter to track how many times the rotation clamping patch is called
        private static int rotationClampingPatchCallCount = 0;
        
        private static readonly AccessTools.FieldRef<CombatExtended.Verb_LaunchProjectileCE, int> numShotsFiredRef =
            AccessTools.FieldRefAccess<CombatExtended.Verb_LaunchProjectileCE, int>("numShotsFired");

        /// <summary>
        /// Retrieves the TurretClampingExtension for a given turret caster if defined.
        /// </summary>
        private static TurretClampingExtension GetClampingExtension(Thing caster)
        {
            if (caster == null) return null;

            if (caster.def != null && caster.def.HasModExtension<TurretClampingExtension>())
                return caster.def.GetModExtension<TurretClampingExtension>();

            if (caster.ParentHolder is Thing parentThing && parentThing.def != null && parentThing.def.HasModExtension<TurretClampingExtension>())
                return parentThing.def.GetModExtension<TurretClampingExtension>();

            if (caster is Building_TurretGun buildingTurret)
            {
                var gun = buildingTurret.gun;
                if (gun != null && gun.def != null && gun.def.HasModExtension<TurretClampingExtension>())
                    return gun.def.GetModExtension<TurretClampingExtension>();
            }

            // Fallback check on TurretBarrelExtension
            var barrelExt = caster.def?.GetModExtension<TurretBarrelExtension>()
                ?? (caster.ParentHolder as Thing)?.def?.GetModExtension<TurretBarrelExtension>();
            if (barrelExt != null)
            {
                float vert = barrelExt.maxVerticalDeviation >= 0f ? barrelExt.maxVerticalDeviation : barrelExt.maxElevationDeviation;
                float rot = barrelExt.maxRotationDeviation >= 0f ? barrelExt.maxRotationDeviation : barrelExt.maxHorizontalDeviation;
                if (vert >= 0f || rot >= 0f)
                {
                    return new TurretClampingExtension
                    {
                        maxVerticalDeviation = vert,
                        maxRotationDeviation = rot
                    };
                }
            }

            return null;
        }

        /// <summary>
        /// Postfix for Verb_LaunchProjectileCE.ShiftTarget - clamps shotRotation and shotAngle for turrets based on XML extension settings
        /// </summary>
        public static void Postfix_Verb_LaunchProjectileCE_ShiftTarget_ClampRotation(object __instance)
        {
            try
            {
                var settings = TurretBarrelAnimationMod.settings;
                if (settings == null)
                    return;

                Thing caster = GetCasterFromVerb(__instance);
                if (caster == null)
                    return;

                TurretClampingExtension clampingExt = GetClampingExtension(caster);
                if (clampingExt == null || (!clampingExt.HasRotationClamping && !clampingExt.HasVerticalClamping))
                {
                    return; // No clamping configured for this turret
                }

                rotationClampingPatchCallCount++;

                Type verbType = __instance.GetType();

                // Get turret base rotation
                float turretBaseRotation = float.NaN;
                Building_Turret building_turret = caster as Building_Turret ?? (caster.ParentHolder as Building_Turret);
                if (building_turret != null)
                {
                    try
                    {
                        var topField = building_turret.GetType().GetField("top", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                        if (topField != null)
                        {
                            var top = topField.GetValue(building_turret);
                            if (top != null)
                            {
                                var curRotationProp = top.GetType().GetProperty("CurRotation", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                                if (curRotationProp != null)
                                {
                                    turretBaseRotation = (float)curRotationProp.GetValue(top);
                                }
                            }
                        }
                    }
                    catch { }
                }

                // === ROTATION CLAMPING (BARREL ORIENTATION CLAMP BEFORE MECHANICAL SPREAD) ===
                FieldInfo shotRotationField = verbType.GetField("shotRotation", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                FieldInfo lastShotRotationField = verbType.GetField("lastShotRotation", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                FieldInfo rotationDegreesField = verbType.GetField("rotationDegrees", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

                if (clampingExt.HasRotationClamping && shotRotationField != null && !float.IsNaN(turretBaseRotation))
                {
                    float shotRotation = (float)shotRotationField.GetValue(__instance);

                    // Isolate pure barrel rotation (Target Heading + Sway + Recoil) before mechanical shot spread
                    float pureBarrelRotation = shotRotation;
                    float spreadX = 0f;

                    if (lastShotRotationField != null && rotationDegreesField != null)
                    {
                        float lastRot = (float)lastShotRotationField.GetValue(__instance);
                        float rotDeg = (float)rotationDegreesField.GetValue(__instance);
                        pureBarrelRotation = lastRot + rotDeg;

                        // Calculate mechanical spread X component (difference between total shotRotation and pure barrel rotation)
                        spreadX = Mathf.Repeat((shotRotation - pureBarrelRotation) + 180f, 360f) - 180f;
                    }

                    // CRITICAL NON-NEGOTIABLE ARCHITECTURAL RULE: DO NOT ALTER THIS MATH OR CONVERT TO DeltaAngle!
                    // CE's shotRotation uses a sign-inverted coordinate space relative to RimWorld turret base:
                    // 1. Raw signed deviation MUST be calculated as: (pureBarrelRotation + turretBaseRotation)
                    // 2. Re-converting back to CE space MUST be calculated as: (deviation - turretBaseRotation)
                    float clampAngle = clampingExt.EffectiveMaxRotationDeviation;
                    float deviation = pureBarrelRotation + turretBaseRotation;
                    deviation = Mathf.Repeat(deviation + 180f, 360f) - 180f;
                    deviation = Mathf.Clamp(deviation, -clampAngle, clampAngle);
                    float clampedBarrelRotation = deviation - turretBaseRotation;
                    clampedBarrelRotation = Mathf.Repeat(clampedBarrelRotation + 180f, 360f) - 180f;

                    // Re-apply mechanical shot spread ON TOP of the clamped barrel orientation
                    float finalShotRotation = Mathf.Repeat((clampedBarrelRotation + spreadX) + 180f, 360f) - 180f;

                    shotRotationField.SetValue(__instance, finalShotRotation);
                }

                // === ELEVATION CLAMPING (BARREL ORIENTATION CLAMP BEFORE MECHANICAL SPREAD) ===
                FieldInfo shotAngleField = verbType.GetField("shotAngle", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                FieldInfo lastShotAngleField = verbType.GetField("lastShotAngle", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                FieldInfo angleRadiansField = verbType.GetField("angleRadians", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

                float shotAngle = 0f;
                float baseBallisticAngle = 0f;
                float clampedAngle = 0f;
                bool hasBaseAngle = false;

                if (shotAngleField != null)
                {
                    shotAngle = (float)shotAngleField.GetValue(__instance);
                    clampedAngle = shotAngle;

                    if (lastShotAngleField != null)
                    {
                        baseBallisticAngle = (float)lastShotAngleField.GetValue(__instance);
                        hasBaseAngle = true;
                    }

                    if (clampingExt.HasVerticalClamping && hasBaseAngle)
                    {
                        // Isolate pure barrel angle (Ballistic + Sway + Recoil) before mechanical shot spread
                        float pureBarrelAngle = shotAngle;
                        float vertSpreadRad = 0f;

                        if (angleRadiansField != null)
                        {
                            pureBarrelAngle = (float)angleRadiansField.GetValue(__instance);
                            vertSpreadRad = shotAngle - pureBarrelAngle;
                        }

                        float clampAngle = clampingExt.EffectiveMaxVerticalDeviation;
                        float clampAngleRad = clampAngle * Mathf.Deg2Rad;

                        float originalDeviationRad = pureBarrelAngle - baseBallisticAngle;
                        float clampedDeviationRad = Mathf.Clamp(originalDeviationRad, -clampAngleRad, clampAngleRad);
                        float clampedBarrelAngle = baseBallisticAngle + clampedDeviationRad;

                        // Re-apply vertical mechanical spread ON TOP of the clamped barrel orientation
                        clampedAngle = clampedBarrelAngle + vertSpreadRad;

                        shotAngleField.SetValue(__instance, clampedAngle);
                    }
                }

                // === CLAMPING DEV TELEMETRY LOGGING ===
                if (settings.logTurretClamping)
                {
                    int shotNum = 1;
                    if (__instance is CombatExtended.Verb_LaunchProjectileCE verbCE)
                    {
                        try
                        {
                            shotNum = numShotsFiredRef(verbCE) + 1;
                        }
                        catch { }
                    }

                    LocalTargetInfo currentTarget = default;
                    try
                    {
                        PropertyInfo currentTargetProp = verbType.GetProperty("CurrentTarget", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                        if (currentTargetProp != null)
                        {
                            currentTarget = (LocalTargetInfo)currentTargetProp.GetValue(__instance);
                        }
                    }
                    catch { }

                    string turretLoc = caster.Position.ToString();
                    string targetLoc = currentTarget.IsValid ? currentTarget.Cell.ToString() : "Unknown Target";
                    float targetElevationDeg = baseBallisticAngle * Mathf.Rad2Deg;
                    float producedElevationDeg = clampedAngle * Mathf.Rad2Deg;

                    AMCLogger.LogTurretClamping($"Shot #{shotNum} , Elevation from {turretLoc} to {targetLoc} = {targetElevationDeg:F2} deg, produced elevation = {producedElevationDeg:F2} deg");
                }
            }
            catch (Exception ex)
            {
                if (TurretBarrelAnimationMod.settings?.logStartup ?? false)
                {
                    Log.Warning($"[AMC] Error in Postfix_Verb_LaunchProjectileCE_ShiftTarget_ClampRotation: {ex.Message}");
                }
            }
        }
        
        /// <summary>
        /// Postfix for Verb_LaunchProjectile CE.ShiftTarget - logs detailed vertical angle breakdown
        /// Shows: ballistic angle + sway + recoil + spread = final shotAngle
        /// </summary>
        public static void Postfix_Verb_LaunchProjectileCE_ShiftTarget_DetailedLogging(object __instance)
        {
            try
            {
                var settings = TurretBarrelAnimationMod.settings;
                if (settings == null || !settings.logVerticalAngleDetailed)
                {
                    return;
                }
                
                Type verbType = __instance.GetType();
                
                // Get final shotAngle (after all adjustments)
                FieldInfo shotAngleField = verbType.GetField("shotAngle", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (shotAngleField == null) return;
                float finalShotAngle = (float)shotAngleField.GetValue(__instance);
                
                // Get angleRadians (ballistic + sway + recoil, before spread)
                FieldInfo angleRadiansField = verbType.GetField("angleRadians", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                float angleRadians = 0f;
                if (angleRadiansField != null)
                {
                    angleRadians = (float)angleRadiansField.GetValue(__instance);
                }
                
                // Get lastShotAngle (pure ballistic angle)
                FieldInfo lastShotAngleField = verbType.GetField("lastShotAngle", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                float ballisticAngle = 0f;
                if (lastShotAngleField != null)
                {
                    ballisticAngle = (float)lastShotAngleField.GetValue(__instance);
                }
                
                // Calculate sway + recoil contribution (angleRadians - ballistic)
                float swayRecoilCombined = angleRadians - ballisticAngle;
                
                // Calculate spread contribution (finalShotAngle - angleRadians)
                float spreadContribution = finalShotAngle - angleRadians;
                
                // Convert to degrees for readability
                float ballisticDeg = ballisticAngle * Mathf.Rad2Deg;
                float swayRecoilDeg = swayRecoilCombined * Mathf.Rad2Deg;
                float spreadDeg = spreadContribution * Mathf.Rad2Deg;
                float finalDeg = finalShotAngle * Mathf.Rad2Deg;
                
                // Log the breakdown
                AMCLogger.LogVerticalAngleDetailed(
                    $"═══ VERTICAL ANGLE BREAKDOWN ═══\n" +
                    $"  Ballistic (physics): {ballisticDeg:F3}°\n" +
                    $"  Sway + Recoil:       {swayRecoilDeg:F3}°\n" +
                    $"  Random Spread:       {spreadDeg:F3}°\n" +
                    $"  ────────────────────────────\n" +
                    $"  Final shotAngle:     {finalDeg:F3}°\n" +
                    $"  Formula: {ballisticDeg:F2}° + {swayRecoilDeg:F2}° + {spreadDeg:F2}° = {finalDeg:F2}°\n" +
                    $"═════════════════════════════"
                );
            }
            catch
            {
                // Silent fail to avoid log spam
            }
        }
    }
}
