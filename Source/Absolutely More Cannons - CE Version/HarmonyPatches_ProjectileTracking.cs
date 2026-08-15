using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Verse;
using RimWorld;
using UnityEngine;

namespace AbsolutelyMoreCannons
{
    /// <summary>
    /// Harmony patches for tracking projectiles spawned from CE turrets.
    /// Logs projectile information every tick while it's still on the map.
    /// </summary>
    [StaticConstructorOnStartup]
    public static class HarmonyPatches_ProjectileTracking
    {
        private static readonly Dictionary<int, ProjectileTrackingInfo> trackedProjectiles = new Dictionary<int, ProjectileTrackingInfo>();
        
        static HarmonyPatches_ProjectileTracking()
        {
            var harmony = new Harmony("AbsolutelyMoreCannons.ProjectileTracking");
            
            // Patch ProjectileCE.Launch to start tracking
            TryPatchProjectileLaunch(harmony);
            
            // Patch ProjectileCE.Tick to log every tick
            TryPatchProjectileTick(harmony);
            
            // Patch ProjectileCE.Destroy to stop tracking
            TryPatchProjectileDestroy(harmony);
        }
        
        private static void TryPatchProjectileLaunch(Harmony harmony)
        {
            try
            {
                var projectileCEType = AccessTools.TypeByName("CombatExtended.ProjectileCE");
                if (projectileCEType == null)
                {
                    Log.Warning("[AMC] Could not find ProjectileCE type for projectile tracking");
                    return;
                }
                
                // Find the Launch method with the full signature
                // public override void Launch(Thing launcher, Vector2 origin, float shotAngle, float shotRotation, float shotHeight = 0f, float shotSpeed = -1f, Thing equipment = null, float distance = -1)
                var launchMethod = AccessTools.Method(projectileCEType, "Launch", new Type[] {
                    typeof(Thing),      // launcher
                    typeof(Vector2),    // origin
                    typeof(float),      // shotAngle
                    typeof(float),      // shotRotation
                    typeof(float),      // shotHeight
                    typeof(float),      // shotSpeed
                    typeof(Thing),      // equipment
                    typeof(float)       // distance
                });
                
                if (launchMethod != null)
                {
                    harmony.Patch(
                        original: launchMethod,
                        postfix: new HarmonyMethod(typeof(HarmonyPatches_ProjectileTracking), nameof(Postfix_ProjectileCE_Launch))
                    );
                    
                    var settings = TurretBarrelAnimationMod.settings;
                    if (settings != null && settings.logStartup)
                    {
                        Log.Message("[AMC] Successfully patched ProjectileCE.Launch for projectile tracking");
                    }
                }
                else
                {
                    Log.Warning("[AMC] Could not find ProjectileCE.Launch method for projectile tracking");
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[AMC] Error patching ProjectileCE.Launch: {ex}");
            }
        }
        
        private static void TryPatchProjectileTick(Harmony harmony)
        {
            try
            {
                var projectileCEType = AccessTools.TypeByName("CombatExtended.ProjectileCE");
                if (projectileCEType == null)
                {
                    return;
                }
                
                var tickMethod = AccessTools.Method(projectileCEType, "Tick");
                if (tickMethod != null)
                {
                    harmony.Patch(
                        original: tickMethod,
                        postfix: new HarmonyMethod(typeof(HarmonyPatches_ProjectileTracking), nameof(Postfix_ProjectileCE_Tick))
                    );
                    
                    var settings = TurretBarrelAnimationMod.settings;
                    if (settings != null && settings.logStartup)
                    {
                        Log.Message("[AMC] Successfully patched ProjectileCE.Tick for projectile tracking");
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[AMC] Error patching ProjectileCE.Tick: {ex}");
            }
        }
        
        private static void TryPatchProjectileDestroy(Harmony harmony)
        {
            try
            {
                var projectileCEType = AccessTools.TypeByName("CombatExtended.ProjectileCE");
                if (projectileCEType == null)
                {
                    return;
                }
                
                // We need to patch the base Destroy method since ProjectileCE likely doesn't override it
                // Patch Verse.ThingWithComps::Destroy(DestroyMode)
                var destroyMethod = AccessTools.Method(typeof(ThingWithComps), "Destroy", new Type[] { typeof(DestroyMode) });
                if (destroyMethod != null)
                {
                    harmony.Patch(
                        original: destroyMethod,
                        prefix: new HarmonyMethod(typeof(HarmonyPatches_ProjectileTracking), nameof(Prefix_ThingWithComps_Destroy))
                    );
                    
                    var settings = TurretBarrelAnimationMod.settings;
                    if (settings != null && settings.logStartup)
                    {
                        Log.Message("[AMC] Successfully patched ThingWithComps.Destroy for projectile tracking");
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[AMC] Error patching Destroy: {ex}");
            }
        }
        
        /// <summary>
        /// Called after ProjectileCE.Launch to start tracking the projectile
        /// </summary>
        public static void Postfix_ProjectileCE_Launch(
            Thing __instance,
            Thing launcher,
            Vector2 origin,
            float shotAngle,
            float shotRotation,
            float shotHeight,
            float shotSpeed,
            Thing equipment)
        {
            try
            {
                var settings = TurretBarrelAnimationMod.settings;
                if (settings == null || !settings.logTemporaryDebug)
                {
                    return;
                }

                if (!settings.logFragmentProjectiles && IsFragmentProjectile(__instance))
                {
                    return;
                }
                
                // Identify turret or launcher
                Building_Turret turret = null;
                if (launcher is Building_Turret bt)
                {
                    turret = bt;
                }
                else if (launcher is Pawn pawn && pawn.MannedThing() is Building_Turret mannedTurret)
                {
                    turret = mannedTurret;
                }
                else if (equipment is Building_Turret eqTurret)
                {
                    turret = eqTurret;
                }
                else if (launcher != null)
                {
                    var ceTurretType = AccessTools.TypeByName("CombatExtended.Building_TurretGunCE");
                    if (ceTurretType != null)
                    {
                        if (ceTurretType.IsAssignableFrom(launcher.GetType()))
                        {
                            turret = launcher as Building_Turret;
                        }
                        else if (ceTurretType.IsAssignableFrom(equipment?.GetType()))
                        {
                            turret = equipment as Building_Turret;
                        }
                    }
                }
                
                int projectileId = __instance.GetHashCode();
                string turretLabel = turret?.LabelCap ?? launcher?.LabelCap ?? equipment?.LabelCap ?? "Unknown Launcher";
                string projectileLabel = __instance.def?.label ?? "unknown projectile";
                
                Vector3 intendedTargetPos = Vector3.zero;
                bool hasTarget = false;
                if (turret != null && turret.CurrentTarget.IsValid)
                {
                    intendedTargetPos = turret.CurrentTarget.Cell.ToVector3Shifted();
                    hasTarget = true;
                }
                else if (launcher is Pawn shooterPawn && shooterPawn.TargetCurrentlyAimingAt.IsValid)
                {
                    intendedTargetPos = shooterPawn.TargetCurrentlyAimingAt.Cell.ToVector3Shifted();
                    hasTarget = true;
                }
                else if (launcher != null)
                {
                    try
                    {
                        var curTargetProp = launcher.GetType().GetProperty("CurrentTarget", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                        if (curTargetProp != null)
                        {
                            var targetInfo = (LocalTargetInfo)curTargetProp.GetValue(launcher);
                            if (targetInfo.IsValid)
                            {
                                intendedTargetPos = targetInfo.Cell.ToVector3Shifted();
                                hasTarget = true;
                            }
                        }
                    }
                    catch { }
                }

                var trackingInfo = new ProjectileTrackingInfo
                {
                    ProjectileId = projectileId,
                    LauncherLabel = turretLabel,
                    ProjectileLabel = projectileLabel,
                    LaunchOrigin = origin,
                    ShotAngle = shotAngle,
                    ShotRotation = shotRotation,
                    ShotHeight = shotHeight,
                    ShotSpeed = shotSpeed,
                    LaunchTick = Find.TickManager.TicksGame,
                    IntendedTargetPos = intendedTargetPos,
                    HasTargetPos = hasTarget
                };
                
                trackedProjectiles[projectileId] = trackingInfo;
                
                // Calculate initial yaw and pitch for clarity
                float initialPitchDegrees = shotAngle * Mathf.Rad2Deg;
                float distToTarget = hasTarget ? Vector2.Distance(origin, new Vector2(intendedTargetPos.x, intendedTargetPos.z)) : 0f;
                string targetStr = hasTarget ? $"({intendedTargetPos.x:F1}, {intendedTargetPos.z:F1}) [Dist: {distToTarget:F1} tiles]" : "Unknown";
                
                AMCLogger.LogTemporaryDebug(
                    $"═══ PROJECTILE LAUNCHED ═══" +
                    $"\n  ID: #{projectileId} ({projectileLabel})" +
                    $"\n  Launcher: {turretLabel} at ({origin.x:F2}, {origin.y:F2})" +
                    $"\n  Intended Target: {targetStr}" +
                    $"\n  Initial YAW: {shotRotation:F2}° (map-relative, North=0°)" +
                    $"\n  Initial PITCH: {initialPitchDegrees:F2}° (elevation angle)" +
                    $"\n  Shot Height: {shotHeight:F2}" +
                    $"\n  Shot Speed: {shotSpeed:F1}" +
                    $"\n═══════════════════════════"
                );
            }
            catch (Exception ex)
            {
                Log.Error($"[AMC] Error in Postfix_ProjectileCE_Launch: {ex}");
            }
        }
        
        /// <summary>
        /// Called after ProjectileCE.Tick to log projectile state every tick
        /// </summary>
        public static void Postfix_ProjectileCE_Tick(Thing __instance)
        {
            try
            {
                var settings = TurretBarrelAnimationMod.settings;
                if (settings == null || !settings.logTemporaryDebug)
                {
                    return;
                }

                if (!settings.logFragmentProjectiles && IsFragmentProjectile(__instance))
                {
                    return;
                }
                
                int projectileId = __instance.GetHashCode();
                
                if (!trackedProjectiles.TryGetValue(projectileId, out ProjectileTrackingInfo info))
                {
                    return; // Not tracking this projectile
                }
                
                // Check if projectile is still on the map
                if (__instance.Map == null || __instance.Destroyed)
                {
                    return; // Projectile is no longer on the map
                }
                
                // Get current position
                Vector3 currentPosition = __instance.Position.ToVector3();
                
                // Try to get exact position if available
                Type projectileType = __instance.GetType();
                FieldInfo exactPosField = projectileType.GetField("ExactPosition", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (exactPosField == null)
                {
                    // Try property
                    PropertyInfo exactPosProp = projectileType.GetProperty("ExactPosition", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    if (exactPosProp != null)
                    {
                        currentPosition = (Vector3)exactPosProp.GetValue(__instance);
                    }
                }
                else
                {
                    currentPosition = (Vector3)exactPosField.GetValue(__instance);
                }
                
                // Get current tick
                int currentTick = Find.TickManager.TicksGame;
                int ticksSinceLaunch = currentTick - info.LaunchTick;
                
                // Calculate distance from origin
                Vector2 currentPos2D = new Vector2(currentPosition.x, currentPosition.z);
                float distanceFromOrigin = Vector2.Distance(info.LaunchOrigin, currentPos2D);
                
                // Try to get velocity if available
                Vector3 velocity = Vector3.zero;
                FieldInfo velocityField = projectileType.GetField("velocity", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (velocityField != null)
                {
                    velocity = (Vector3)velocityField.GetValue(__instance);
                }
                
                // === CALCULATE YAW AND PITCH ANGLES (MAP-RELATIVE, SIGNED) ===
                
                // YAW (Horizontal/Azimuth angle, map-relative, SIGNED)
                // In RimWorld: North=0°, East=90°, South=180°/-180°, West=-90°
                // Range: -180° to +180° (signed)
                float yawDegrees = 0f;
                if (velocity.x != 0f || velocity.z != 0f)
                {
                    // Atan2 gives us angle from East in radians, we convert to RimWorld's North=0° convention
                    yawDegrees = (-90f + Mathf.Rad2Deg * Mathf.Atan2(velocity.z, velocity.x)) % 360f;
                    // Normalize to -180 to +180 range (signed)
                    if (yawDegrees > 180f) yawDegrees -= 360f;
                    if (yawDegrees < -180f) yawDegrees += 360f;
                }
                
                // PITCH (Vertical angle from horizontal plane, SIGNED)
                // Positive = upward, Negative = downward
                // Range: -90° (straight down) to +90° (straight up)
                float pitchDegrees = 0f;
                float horizontalSpeed = Mathf.Sqrt(velocity.x * velocity.x + velocity.z * velocity.z);
                if (horizontalSpeed > 0.001f || Mathf.Abs(velocity.y) > 0.001f)
                {
                    pitchDegrees = Mathf.Rad2Deg * Mathf.Atan2(velocity.y, horizontalSpeed);
                }
                
                // Velocity magnitude (total speed)
                float velocityMagnitude = velocity.magnitude;
                
                // === ADDITIONAL DEBUG INFO ===
                
                // Get shotAngle and shotRotation from projectile if available
                float currentShotAngle = float.NaN;
                float currentShotRotation = float.NaN;
                
                FieldInfo shotAngleField = projectileType.GetField("shotAngle", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (shotAngleField != null)
                {
                    currentShotAngle = (float)shotAngleField.GetValue(__instance);
                }
                
                FieldInfo shotRotationField = projectileType.GetField("shotRotation", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (shotRotationField != null)
                {
                    currentShotRotation = (float)shotRotationField.GetValue(__instance);
                }
                
                // Get FlightTicks if available
                int flightTicks = -1;
                FieldInfo flightTicksField = projectileType.GetField("FlightTicks", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (flightTicksField != null)
                {
                    flightTicks = (int)flightTicksField.GetValue(__instance);
                }
                
                // === COMPREHENSIVE LOG ===
                AMCLogger.LogTemporaryDebug(
                    $"PROJECTILE TICK #{projectileId} ({info.ProjectileLabel}): " +
                    $"Tick {ticksSinceLaunch} (Flight: {flightTicks}) | " +
                    $"Pos: ({currentPosition.x:F2}, {currentPosition.y:F2}, {currentPosition.z:F2}) | " +
                    $"Dist: {distanceFromOrigin:F2} | " +
                    $"Vel: ({velocity.x:F2}, {velocity.y:F2}, {velocity.z:F2}) | Speed: {velocityMagnitude:F2} | " +
                    $"YAW: {yawDegrees:F2}° | PITCH: {pitchDegrees:F2}° | " +
                    (float.IsNaN(currentShotAngle) ? "" : $"ShotAngle: {currentShotAngle * Mathf.Rad2Deg:F2}° | ") +
                    (float.IsNaN(currentShotRotation) ? "" : $"ShotRotation: {currentShotRotation:F2}° | ")
                );
            }
            catch
            {
                // Silently fail to avoid spamming logs
                // Log.Error($"[AMC] Error in Postfix_ProjectileCE_Tick: {ex}");
            }
        }
        
        /// <summary>
        /// Called before ThingWithComps.Destroy to stop tracking and log final state
        /// Only processes ProjectileCE instances
        /// </summary>
        public static void Prefix_ThingWithComps_Destroy(Thing __instance, DestroyMode mode)
        {
            try
            {
                // Only process ProjectileCE instances
                var projectileCEType = AccessTools.TypeByName("CombatExtended.ProjectileCE");
                if (projectileCEType == null || !projectileCEType.IsInstanceOfType(__instance))
                {
                    return; // Not a ProjectileCE, skip
                }
                
                var settings = TurretBarrelAnimationMod.settings;
                if (settings == null || !settings.logTemporaryDebug)
                {
                    return;
                }

                if (!settings.logFragmentProjectiles && IsFragmentProjectile(__instance))
                {
                    return;
                }
                
                int projectileId = __instance.GetHashCode();
                
                if (trackedProjectiles.TryGetValue(projectileId, out ProjectileTrackingInfo info))
                {
                    int currentTick = Find.TickManager.TicksGame;
                    int totalTicks = currentTick - info.LaunchTick;
                    
                    // Get final position
                    Vector3 finalPosition = __instance.Position.ToVector3();
                    Type projectileType = __instance.GetType();
                    PropertyInfo exactPosProp = projectileType.GetProperty("ExactPosition", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    if (exactPosProp != null)
                    {
                        finalPosition = (Vector3)exactPosProp.GetValue(__instance);
                    }
                    
                    Vector2 finalPos2D = new Vector2(finalPosition.x, finalPosition.z);
                    float totalDistance = Vector2.Distance(info.LaunchOrigin, finalPos2D);
                    float averageSpeed = totalTicks > 0 ? totalDistance / (totalTicks / 60f) : 0f;
                    
                    // Calculate final trajectory angles if velocity is available
                    Vector3 finalVelocity = Vector3.zero;
                    FieldInfo velocityField = projectileType.GetField("velocity", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    if (velocityField != null)
                    {
                        finalVelocity = (Vector3)velocityField.GetValue(__instance);
                    }
                    
                    float finalYaw = 0f;
                    float finalPitch = 0f;
                    if (finalVelocity.x != 0f || finalVelocity.z != 0f)
                    {
                        finalYaw = (-90f + Mathf.Rad2Deg * Mathf.Atan2(finalVelocity.z, finalVelocity.x)) % 360f;
                        if (finalYaw > 180f) finalYaw -= 360f;
                        if (finalYaw < -180f) finalYaw += 360f;
                        
                        float horizontalSpeed = Mathf.Sqrt(finalVelocity.x * finalVelocity.x + finalVelocity.z * finalVelocity.z);
                        if (horizontalSpeed > 0.001f || Mathf.Abs(finalVelocity.y) > 0.001f)
                        {
                            finalPitch = Mathf.Rad2Deg * Mathf.Atan2(finalVelocity.y, horizontalSpeed);
                        }
                    }

                    string targetDevStr = "N/A";
                    if (info.HasTargetPos)
                    {
                        float dev = Vector2.Distance(finalPos2D, new Vector2(info.IntendedTargetPos.x, info.IntendedTargetPos.z));
                        targetDevStr = $"{dev:F2} tiles from target ({info.IntendedTargetPos.x:F1}, {info.IntendedTargetPos.z:F1})";
                        if (dev >= 15f)
                        {
                            targetDevStr += " [⚠️ EXTREME DEVIATION DETECTED]";
                        }
                    }
                    
                    AMCLogger.LogTemporaryDebug(
                        $"═══ PROJECTILE DESTROYED ═══" +
                        $"\n  ID: #{projectileId} ({info.ProjectileLabel})" +
                        $"\n  Launcher: {info.LauncherLabel}" +
                        $"\n  Lifetime: {totalTicks} ticks ({totalTicks / 60f:F2} seconds)" +
                        $"\n  Total Distance: {totalDistance:F2} tiles" +
                        $"\n  Average Speed: {averageSpeed:F2} tiles/sec" +
                        $"\n  Impact Location: ({finalPosition.x:F2}, {finalPosition.y:F2}, {finalPosition.z:F2})" +
                        $"\n  Target Deviation: {targetDevStr}" +
                        $"\n  Final YAW: {finalYaw:F2}° | Final PITCH: {finalPitch:F2}°" +
                        $"\n  Final Velocity: ({finalVelocity.x:F2}, {finalVelocity.y:F2}, {finalVelocity.z:F2})" +
                        $"\n  Destroy Mode: {mode}" +
                        $"\n═════════════════════════════"
                    );
                    
                    // Remove from tracking
                    trackedProjectiles.Remove(projectileId);
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[AMC] Error in Prefix_ThingWithComps_Destroy: {ex}");
            }
        }
        
        private static bool IsFragmentProjectile(Thing projectile)
        {
            if (projectile == null) return false;
            
            // Check type name
            string typeName = projectile.GetType().Name;
            if (typeName.IndexOf("Fragment", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }
            
            // Check ThingDef defName and label
            if (projectile.def != null)
            {
                if (!string.IsNullOrEmpty(projectile.def.defName) && projectile.def.defName.IndexOf("Fragment", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
                if (!string.IsNullOrEmpty(projectile.def.label) && projectile.def.label.IndexOf("fragment", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }
            
            return false;
        }

        private class ProjectileTrackingInfo
        {
            public int ProjectileId;
            public string LauncherLabel;
            public string ProjectileLabel;
            public Vector2 LaunchOrigin;
            public float ShotAngle;
            public float ShotRotation;
            public float ShotHeight;
            public float ShotSpeed;
            public int LaunchTick;
            public Vector3 IntendedTargetPos;
            public bool HasTargetPos;
        }
    }
}
