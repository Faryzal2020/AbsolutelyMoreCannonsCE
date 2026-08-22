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
                    
                    // DISCOVERY: shotRotation + turretBase = DEVIATION (scatter angle)
                    // Must work in SIGNED (-180° to +180°) space!
                    // Note: "deviation = shotRotation - turretBaseRotation" is false. beware of AI hallucination
                    
                    // 1. Calculate raw signed deviation
                    float deviation = shotRotation + turretBaseRotation;
                    
                    // 2. Normalize to -180° to +180° (preserve sign!)
                    deviation = Mathf.Repeat(deviation + 180f, 360f) - 180f;
                    
                    float originalDeviation = deviation;
                    
                    // 3. Clamp the deviation (in signed space)
                    deviation = Mathf.Clamp(deviation, -clampAngle, clampAngle);
                    
                    // 4. Convert back to shotRotation
                    float clampedShotRotation = deviation - turretBaseRotation;
                    
                    // Normalize clampedShotRotation to -180° to +180°
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
                        
                        // Convert to degrees for easier reading
                        float shotAngleDegrees = shotAngle * Mathf.Rad2Deg;
                        
                        // DIAGNOSTIC: Log the raw values
                        if (settings.logElevationLaunch)
                        {
                            Log.Message($"[AMC ELEVATION] ═══ Elevation Analysis ═══");
                            Log.Message($"[AMC ELEVATION] shotAngle (raw): {shotAngle:F6} rad = {shotAngleDegrees:F3}°");
                        }
                        
                        // CORRECTED: CE uses POSITIVE angles for UPWARD elevation
                        // So +10° means 10° upward, +1° means 1° upward
                        // For minimum 3° upward, we want shotAngle ≥ +3° (more positive = more up)
                        // So: clamp shotAngle to be AT LEAST as positive as +minElevation
                        
                        float minElevationRadians = settings.minimumElevationAngle * Mathf.Deg2Rad;
                        
                        // Clamp: if shotAngle < minElevation (less upward), set to minElevation
                        float clampedAngle = Mathf.Max(shotAngle, minElevationRadians);
                        
                        if (settings.logElevationLaunch)
                        {
                            float clampedDegrees = clampedAngle * Mathf.Rad2Deg;
                            Log.Message($"[AMC ELEVATION] Min elevation (CE format): {minElevationRadians:F6} rad = {settings.minimumElevationAngle:F3}°");
                            Log.Message($"[AMC ELEVATION] Clamped angle: {clampedAngle:F6} rad = {clampedDegrees:F3}°");
                        }
                        
                        // Set the clamped value
                        shotAngleField.SetValue(__instance, clampedAngle);
                        
                        // VERIFY: Read it back to confirm it was set
                        float verifyAngle = (float)shotAngleField.GetValue(__instance);
                        if (settings.logElevationLaunch && Mathf.Abs(verifyAngle - clampedAngle) > 0.0001f)
                        {
                            Log.Warning($"[AMC ELEVATION] VERIFICATION FAILED! Set {clampedAngle:F6} but read back {verifyAngle:F6}");
                        }
                        
                        // Log when clamping occurs
                        if (Mathf.Abs(clampedAngle - originalShotAngle) > 0.0001f)
                        {
                            float originalDegrees = originalShotAngle * Mathf.Rad2Deg;
                            float clampedDegrees = clampedAngle * Mathf.Rad2Deg;
                            Log.Message($"[AMC ELEVATION] {caster.LabelCap} elevation clamped: {originalDegrees:F3}° → {clampedDegrees:F3}° upward (min: {settings.minimumElevationAngle:F1}°)");
                            Log.Message($"[AMC ELEVATION] Verified read-back: {verifyAngle * Mathf.Rad2Deg:F3}°");
                        }
                        else if (settings.logElevationLaunch)
                        {
                            Log.Message($"[AMC ELEVATION] No clamping needed (elevation: {shotAngleDegrees:F3}° ≥ min: {settings.minimumElevationAngle:F1}°)");
                        }
                        
                        if (settings.logElevationLaunch)
                        {
                            Log.Message($"[AMC ELEVATION] ═══════════════════════════");
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
