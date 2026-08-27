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
        
        /// <summary>
        /// Helper to verify if a thing is an AMC-configured turret.
        /// </summary>
        private static bool IsAMCConfiguredTurret(Thing caster)
        {
            if (caster == null) return false;

            if (caster.TryGetComp<CompTurretBarrel>() != null) return true;
            if (caster.def != null && caster.def.HasModExtension<TurretBarrelExtension>()) return true;

            if (caster.ParentHolder is Thing parentThing)
            {
                if (parentThing.TryGetComp<CompTurretBarrel>() != null) return true;
                if (parentThing.def != null && parentThing.def.HasModExtension<TurretBarrelExtension>()) return true;
            }

            return false;
        }

        /// <summary>
        /// Postfix for Verb_LaunchProjectileCE.ShiftTarget - clamps shotRotation for turrets
        /// This patch includes diagnostic logging to determine the reference frame of shotRotation
        /// </summary>
        public static void Postfix_Verb_LaunchProjectileCE_ShiftTarget_ClampRotation(object __instance)
        {
            try
            {
                // Get settings early
                var settings = TurretBarrelAnimationMod.settings;
                if (settings == null)
                    return;
                
                // Increment call counter
                rotationClampingPatchCallCount++;
                
                // Log every 10th call to confirm patch is working
                if (settings.logRotationLaunch && rotationClampingPatchCallCount % 10 == 1 && rotationClampingPatchCallCount <= 100)
                {
                    Log.Message($"[AMC] Rotation clamping patch called (count: {rotationClampingPatchCallCount})");
                }
                
                // Get the verb type
                Type verbType = __instance.GetType();
                
                // Check if it's an AMC-configured turret
                Thing caster = GetCasterFromVerb(__instance);
                if (caster == null || !IsAMCConfiguredTurret(caster))
                {
                    return; // Only process AMC-configured turrets
                }
                
                // Get shotRotation field
                FieldInfo shotRotationField = verbType.GetField("shotRotation", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (shotRotationField == null)
                    return;
                
                float shotRotation = (float)shotRotationField.GetValue(__instance);
                float originalRotation = shotRotation;
                
                // Get turret base rotation for diagnostic logging
                float turretBaseRotation = float.NaN;
                if (caster is Building_Turret building_turret)
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
                
                // DIAGNOSTIC LOGGING - Log the reference frame analysis
                if (settings.logRotationDiagnostics)
                {
                    Log.Message($"[AMC DIAGNOSTIC] ═══ Rotation Reference Frame Analysis ═══");
                    Log.Message($"[AMC DIAGNOSTIC] Turret: {caster.LabelCap}");
                    Log.Message($"[AMC DIAGNOSTIC] Turret Base Rotation (RW coords): {turretBaseRotation:F3}°");
                    Log.Message($"[AMC DIAGNOSTIC] shotRotation (CE field): {shotRotation:F3}°");
                    
                    // Get target for direction calculation
                    try
                    {
                        PropertyInfo currentTargetProp = verbType.GetProperty("CurrentTarget", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                        FieldInfo currentTargetField = verbType.GetField("currentTarget", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                        LocalTargetInfo target = default(LocalTargetInfo);
                        
                        if (currentTargetProp != null)
                        {
                            target = (LocalTargetInfo)currentTargetProp.GetValue(__instance);
                        }
                        else if (currentTargetField != null)
                        {
                            target = (LocalTargetInfo)currentTargetField.GetValue(__instance);
                        }
                        
                        if (target.IsValid)
                        {
                            Vector2 origin = new Vector2(caster.Position.x, caster.Position.z);
                            Vector2 targetPos = new Vector2(target.Cell.x, target.Cell.z);
                            Vector2 toTarget = targetPos - origin;
                            float idealRotationRW = Mathf.Atan2(toTarget.x, toTarget.y) * Mathf.Rad2Deg;
                            if (idealRotationRW < 0) idealRotationRW += 360f;
                            
                            Log.Message($"[AMC DIAGNOSTIC] Target Direction (RW coords): {idealRotationRW:F3}°");
                            
                            // Calculate deviations
                            float deviationFromTarget = shotRotation - idealRotationRW;
                            while (deviationFromTarget > 180f) deviationFromTarget -= 360f;
                            while (deviationFromTarget < -180f) deviationFromTarget += 360f;
                            
                            float deviationFromTurret = shotRotation - turretBaseRotation;
                            while (deviationFromTurret > 180f) deviationFromTurret -= 360f;
                            while (deviationFromTurret < -180f) deviationFromTurret += 360f;
                            
                            Log.Message($"[AMC DIAGNOSTIC] shotRotation - idealDir = {deviationFromTarget:F3}°");
                            Log.Message($"[AMC DIAGNOSTIC] shotRotation - turretBase = {deviationFromTurret:F3}°");
                            Log.Message($"[AMC DIAGNOSTIC] ");
                            Log.Message($"[AMC DIAGNOSTIC] INTERPRETATION:");
                            Log.Message($"[AMC DIAGNOSTIC] → shotRotation is ABSOLUTE (map coordinates)");
                            Log.Message($"[AMC DIAGNOSTIC] → Deviation from turret base: {deviationFromTurret:F3}°");
                            Log.Message($"[AMC DIAGNOSTIC] → This deviation will be clamped to ±{settings.rotationClampAngle:F1}° if enabled");
                            
                            Log.Message($"[AMC DIAGNOSTIC] ═══════════════════════════════════════");
                        }
                    }
                    catch (Exception diagEx)
                    {
                        Log.Warning($"[AMC DIAGNOSTIC] Error in diagnostic logging: {diagEx.Message}");
                    }
                }
                
                // Apply rotation clamping if enabled
                if (settings.enableRotationClamping)
                {
                    float clampAngle = settings.rotationClampAngle;
                    
                    // =========================================================================================
                    // CRITICAL NON-NEGOTIABLE ARCHITECTURAL RULE: DO NOT ALTER THIS MATH OR CONVERT TO DeltaAngle!
                    // =========================================================================================
                    // Combat Extended's BaseTrajectoryWorker.ShotRotation formula is:
                    //   shotRotation = (-90 + Mathf.Rad2Deg * Mathf.Atan2(w.z, w.x)) % 360
                    // Standard RimWorld turretBaseRotation / AngleFlat formula is:
                    //   turretBaseRotation = Mathf.Rad2Deg * Mathf.Atan2(w.x, w.z)
                    //
                    // Because CE's shotRotation uses a sign-inverted coordinate space relative to RimWorld turret base:
                    // 1. Raw signed deviation MUST be calculated as: (shotRotation + turretBaseRotation)
                    // 2. Re-converting back to CE space MUST be calculated as: (deviation - turretBaseRotation)
                    //
                    // WARNING: Replacing this with Mathf.DeltaAngle(turretBaseRotation, shotRotation) ASSUMES
                    // both angles use the same coordinate space, which is FALSE in CE. Doing so causes a 90° to 180°
                    // rotation corruption, directing bullets South-East when aiming South-West.
                    // =========================================================================================
                    
                    // 1. Calculate raw signed deviation in CE inverted coordinate space
                    float deviation = shotRotation + turretBaseRotation;
                    
                    // 2. Normalize deviation to [-180°, +180°] range
                    deviation = Mathf.Repeat(deviation + 180f, 360f) - 180f;
                    
                    float originalDeviation = deviation;
                    
                    // 3. Clamp deviation within [-clampAngle, +clampAngle]
                    deviation = Mathf.Clamp(deviation, -clampAngle, clampAngle);
                    
                    // 4. Convert back to CE shotRotation space
                    float clampedShotRotation = deviation - turretBaseRotation;
                    clampedShotRotation = Mathf.Repeat(clampedShotRotation + 180f, 360f) - 180f;
                    
                    // Calculate actual direction for logging
                    float actualDirection = turretBaseRotation + deviation;
                    while (actualDirection >= 360f) actualDirection -= 360f;
                    while (actualDirection < 0f) actualDirection += 360f;
                    
                    // Set the clamped value
                    shotRotationField.SetValue(__instance, clampedShotRotation);
                    
                    // Diagnostic logging if enabled
                    if (settings.logRotationDiagnostics)
                    {
                        Log.Message($"[AMC OBSERVE] ═══════════════════════════════");
                        Log.Message($"[AMC OBSERVE] {caster.LabelCap}");
                        Log.Message($"[AMC OBSERVE] Turret Base: {turretBaseRotation:F3}°");
                        Log.Message($"[AMC OBSERVE] shotRotation (CE): {shotRotation:F3}°");
                        Log.Message($"[AMC OBSERVE] Shot Deviation (signed): {originalDeviation:F3}°");
                        Log.Message($"[AMC OBSERVE] Actual Direction: {actualDirection:F3}°");
                        Log.Message($"[AMC OBSERVE] Clamped deviation: {deviation:F3}°");
                        Log.Message($"[AMC OBSERVE] Set shotRotation to: {clampedShotRotation:F3}°");
                        Log.Message($"[AMC OBSERVE] ═══════════════════════════════");
                    }
                }
                
                // === ELEVATION CLAMPING ===
                if (settings.enableElevationClamping)
                {
                    // Get shotAngle field
                    FieldInfo shotAngleField = verbType.GetField("shotAngle", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    if (shotAngleField != null)
                    {
                        float shotAngle = (float)shotAngleField.GetValue(__instance);
                        float originalShotAngle = shotAngle;
                        
                        // Get base ballistic elevation angle (lastShotAngle is CE's pure target elevation requirement)
                        FieldInfo lastShotAngleField = verbType.GetField("lastShotAngle", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                        float baseBallisticAngle = 0f;
                        bool hasBaseAngle = false;
                        if (lastShotAngleField != null)
                        {
                            baseBallisticAngle = (float)lastShotAngleField.GetValue(__instance);
                            hasBaseAngle = true;
                        }
                        
                        float clampAngle = settings.elevationClampAngle;
                        float clampAngleRad = clampAngle * Mathf.Deg2Rad;
                        float clampedAngle = shotAngle;
                        float originalDeviationRad = 0f;
                        float clampedDeviationRad = 0f;

                        if (hasBaseAngle)
                        {
                            // Calculate raw vertical deviation caused by sway, recoil, and spread relative to target elevation angle
                            originalDeviationRad = shotAngle - baseBallisticAngle;
                            
                            // Clamp vertical deviation within [-clampAngle, +clampAngle]
                            clampedDeviationRad = Mathf.Clamp(originalDeviationRad, -clampAngleRad, clampAngleRad);
                            
                            // Recombine with base ballistic angle
                            clampedAngle = baseBallisticAngle + clampedDeviationRad;
                        }
                        else
                        {
                            // Fallback: If lastShotAngle is unretrievable
                            if (settings.logElevationLaunch)
                            {
                                Log.Warning("[AMC ELEVATION] Could not find lastShotAngle field for relative elevation clamping");
                            }
                        }

                        // Set the clamped value
                        shotAngleField.SetValue(__instance, clampedAngle);

                        // DIAGNOSTIC LOGGING
                        if (settings.logElevationLaunch)
                        {
                            float shotAngleDeg = shotAngle * Mathf.Rad2Deg;
                            float baseBallisticDeg = baseBallisticAngle * Mathf.Rad2Deg;
                            float originalDevDeg = originalDeviationRad * Mathf.Rad2Deg;
                            float clampedDevDeg = clampedDeviationRad * Mathf.Rad2Deg;
                            float clampedAngleDeg = clampedAngle * Mathf.Rad2Deg;

                            Log.Message($"[AMC ELEVATION] ═══ Elevation Analysis ═══");
                            Log.Message($"[AMC ELEVATION] {caster.LabelCap}");
                            Log.Message($"[AMC ELEVATION] Base Target Elevation (ballistic): {baseBallisticDeg:F3}°");
                            Log.Message($"[AMC ELEVATION] Unclamped shotAngle (CE):           {shotAngleDeg:F3}°");
                            Log.Message($"[AMC ELEVATION] Raw Vertical Deviation:              {originalDevDeg:F3}°");
                            Log.Message($"[AMC ELEVATION] Max Allowed Clamp Angle:            ±{clampAngle:F1}°");
                            Log.Message($"[AMC ELEVATION] Clamped Vertical Deviation:          {clampedDevDeg:F3}°");
                            Log.Message($"[AMC ELEVATION] Final Clamped shotAngle:             {clampedAngleDeg:F3}°");
                            Log.Message($"[AMC ELEVATION] ═══════════════════════════");
                        }
                        else if (Mathf.Abs(clampedAngle - originalShotAngle) > 0.0001f && settings.logRotationDiagnostics)
                        {
                            float origDevDeg = originalDeviationRad * Mathf.Rad2Deg;
                            float clampedDevDeg = clampedDeviationRad * Mathf.Rad2Deg;
                            Log.Message($"[AMC ELEVATION] {caster.LabelCap} elevation deviation clamped: {origDevDeg:F3}° → {clampedDevDeg:F3}° (max: ±{clampAngle:F1}°)");
                        }
                    }
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
