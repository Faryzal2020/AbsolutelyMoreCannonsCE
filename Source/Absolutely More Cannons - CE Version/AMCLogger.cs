using System;
using System.Text;
using Verse;
using RimWorld;
using UnityEngine;
using System.Reflection;

namespace AbsolutelyMoreCannons
{
    /// <summary>
    /// Centralized logging utility for Absolutely More Cannons mod.
    /// Provides formatted logging with category-based toggles.
    /// </summary>
    public static class AMCLogger
    {
        private const string PREFIX = "[AMC]";

        /// <summary>
        /// Get current settings instance
        /// </summary>
        private static AMCSettings Settings => TurretBarrelAnimationMod.settings;

        // === PROJECTILE LAUNCH LOGGING ===

        /// <summary>
        /// Logs the vertical and horizontal angles of the shell launched from turrets
        /// </summary>
        public static void LogProjectileLaunch(
            object verbInstance,
            Thing launcher,
            Vector2 origin,
            float shotHeight,
            float shotSpeed,
            float shotAngle,
            float shotRotation,
            float turretBaseRotation,
            float rotationDegrees,
            float lastShotRotation,
            Vector2 newTargetLoc,
            LocalTargetInfo target,
            Thing equipment,
            object projectileInstance = null)
        {
            if (Settings == null)
                return;

            // Only log turrets
            if (launcher == null || !(launcher is Building_Turret))
                return;

            string turretLabel = launcher.LabelCap ?? "Unknown Turret";
            string position = $"({launcher.Position.x}, {launcher.Position.z})";
            
            // Log elevation if enabled
            if (Settings.logElevationLaunch)
            {
                float verticalAngleDegrees = shotAngle * Mathf.Rad2Deg;
                Log.Message($"{PREFIX} {turretLabel} at {position} | Vertical: {verticalAngleDegrees:F3}°");
            }
            
            // Log rotation if enabled
            if (Settings.logRotationLaunch)
            {
                Log.Message($"{PREFIX} {turretLabel} at {position} | Horizontal (shotRotation): {shotRotation:F3}° | Turret Base: {turretBaseRotation:F3}°");
            }
        }

        /// <summary>
        /// Logs produced elevation and deviation telemetry for turrets with clamping settings enabled.
        /// </summary>
        public static void LogTurretClamping(string message)
        {
            if (Settings == null || !Settings.logTurretClamping)
                return;
            Log.Message($"{PREFIX} {message}");
        }

        // === TURRET COMPONENT LOGGING ===

        public static void LogTurretBarrel(string message)
        {
            if (Settings == null || !Settings.logTurretBarrel)
                return;
            Log.Message($"{PREFIX} [BARREL] {message}");
        }

        public static void LogTurretModeSwap(string message)
        {
            if (Settings == null || !Settings.logTurretModeSwap)
                return;
            Log.Message($"{PREFIX} [MODE_SWAP] {message}");
        }

        public static void LogTurretAmmo(string message)
        {
            if (Settings == null || !Settings.logTurretAmmo)
                return;
            Log.Message($"{PREFIX} [AMMO] {message}");
        }

        public static void LogTurretTarget(string message)
        {
            if (Settings == null || !Settings.logTurretTarget)
                return;
            Log.Message($"{PREFIX} [TARGET] {message}");
        }

        public static void LogTurretFireTimestamp(string message)
        {
            if (Settings == null || !Settings.logTurretFireTimestamp)
                return;
            Log.Message($"{PREFIX} [FIRE_TIMESTAMP] {message}");
        }

        public static void LogFCS(string message)
        {
            if (Settings == null || !Settings.logFCS)
                return;
            Log.Message($"{PREFIX} [FCS Log] {message}");
        }

        // === GENERAL COMPONENT LOGGING ===

        public static void LogRotation(string message)
        {
            if (Settings == null || !Settings.logRotation)
                return;
            Log.Message($"{PREFIX} [ROTATION] {message}");
        }

        public static void LogAnimation(string message)
        {
            if (Settings == null || !Settings.logAnimation)
                return;
            Log.Message($"{PREFIX} [ANIMATION] {message}");
        }

        // === TEMPORARY DEBUG LOGGING ===

        public static void LogTemporaryDebug(string message)
        {
            if (Settings == null || !Settings.logTemporaryDebug)
                return;
            Log.Message($"{PREFIX} [TEMP_DEBUG] {message}");
        }
        
        
        public static void LogVerticalAngleDetailed(string message)
        {
            if (Settings == null || !Settings.logVerticalAngleDetailed)
                return;
            Log.Message($"{PREFIX} [VERTICAL_ANGLE] {message}");
        }

        public static void LogTurretViewTransfer(string message)
        {
            if (Settings == null || !Settings.logTurretViewTransfer)
                return;
            Log.Message($"{PREFIX} [VIEW_TRANSFER] {message}");
        }

        // === AIRBURST DETONATION LOGGING ===

        public static void LogAirburst(string message)
        {
            if (Settings == null || !Settings.logAirburstDetonation)
                return;
            Log.Message($"{PREFIX} [AIRBURST] {message}");
        }

        // === PROJECTILE TRACER LOGGING ===

        public static void LogProjectileTracer(string message)
        {
            if (Settings == null || !Settings.logProjectileTracers)
                return;
            Log.Message($"{PREFIX} [TRACER] {message}");
        }

        // === TURRET SMOKE LOGGING ===

        public static void LogTurretSmoke(string message)
        {
            if (Settings == null || !Settings.logTurretSmoke)
                return;
            int ticks = Find.TickManager.TicksGame;
            Log.Message($"{PREFIX} [SMOKE] T={ticks} | {message}");
        }

        /// <summary>
        /// Logs detailed smoke particle telemetry (spawn configuration, lifecycle events)
        /// </summary>
        public static void LogTurretSmokeParticleTelemetry(string message)
        {
            if (Settings == null || !Settings.logTurretSmokeParticleTelemetry)
                return;
            int ticks = Find.TickManager.TicksGame;
            Log.Message($"{PREFIX} [SMOKE_TELEMETRY] T={ticks} | {message}");
        }

        /// <summary>
        /// Logs compact per-tick particle tracking (position, velocity, direction)
        /// </summary>
        public static void LogTurretSmokeParticleTick(string message)
        {
            if (Settings == null || !Settings.logTurretSmokeParticleTick)
                return;
            Log.Message($"{PREFIX} [SMOKE_TICK] {message}");
        }

        // === MUZZLE FLASH MOD LOGGING ===

        public static void LogMuzzleFlashMod(string message)
        {
            if (Settings == null || !Settings.logMuzzleFlashMod)
                return;
            Log.Message($"{PREFIX} [MUZZLE_FLASH_MOD] {message}");
        }
    }
}
