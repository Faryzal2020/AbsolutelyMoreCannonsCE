using System;
using UnityEngine;
using Verse;

namespace AbsolutelyMoreCannons
{
    /// <summary>
    /// Mod settings for Absolutely More Cannons - CE Version.
    /// Provides toggles for various debug logging categories.
    /// </summary>
    public class AMCSettings : ModSettings
    {
        // === Projectile Launch Logging ===
        public bool logElevationLaunch = false; // Vertical angle logging
        public bool logRotationLaunch = false;  // Horizontal angle/rotation logging

        // === Rotation Clamping ===
        public bool enableRotationClamping = false;
        public float rotationClampAngle = 5.0f; // degrees
        
        // === Elevation Clamping ===
        public bool enableElevationClamping = false;
        public float minimumElevationAngle = 0.0f; // degrees (prevent shooting into ground)
        
        // === Diagnostic Logging ===
        public bool logRotationDiagnostics = false; // AMC DIAGNOSTIC and AMC OBSERVE logs
        public bool logStartup = true; // Mod initialization and patch success messages
        public bool logTemporaryDebug = false; // Temporary debugging logs (e.g., projectile tick tracking)
        public bool logVerticalAngleDetailed = false; // Detailed vertical angle breakdown logging
        
        // === Individual Accuracy Tuning (0-100%) ===
        public float swayReductionPercent = 0f;    // 0% = full sway, 100% = no sway
        public float recoilReductionPercent = 0f;  // 0% = full recoil, 100% = no recoil
        public float spreadReductionPercent = 0f;  // 0% = full spread, 100% = no spread
        public bool showAccuracyOverrideInspect = true; // Show per-turret accuracy overrides in inspect panel


        // === Turret Component Logging ===
        public bool logTurretBarrel = false;
        public bool logTurretModeSwap = false;
        public bool logTurretAmmo = false;
        public bool logTurretTarget = false;
        public bool logTurretFireTimestamp = false;  // Timestamp when turret fires

        // === General Component Logging ===
        public bool logRotation = false;
        public bool logAnimation = false;
        public bool logTurretSmoke = false;
        public bool logTurretSmokeParticleTelemetry = false;  // Detailed particle spawn/lifecycle logging
        public bool logTurretSmokeParticleTick = false;       // Per-tick particle position/velocity logging

        /// <summary>
        /// Save and load settings from XML
        /// </summary>
        public override void ExposeData()
        {
            base.ExposeData();

            // Projectile Launch Logging
            Scribe_Values.Look(ref logElevationLaunch, "logElevationLaunch", false);
            Scribe_Values.Look(ref logRotationLaunch, "logRotationLaunch", false);

            // Rotation Clamping
            Scribe_Values.Look(ref enableRotationClamping, "enableRotationClamping", false);
            Scribe_Values.Look(ref rotationClampAngle, "rotationClampAngle", 5.0f);
            
            // Elevation Clamping
            Scribe_Values.Look(ref enableElevationClamping, "enableElevationClamping", false);
            Scribe_Values.Look(ref minimumElevationAngle, "minimumElevationAngle", 0.0f);
            
            // Diagnostic Logging
            Scribe_Values.Look(ref logRotationDiagnostics, "logRotationDiagnostics", false);
            Scribe_Values.Look(ref logStartup, "logStartup", true);
            Scribe_Values.Look(ref logTemporaryDebug, "logTemporaryDebug", false);
            Scribe_Values.Look(ref logVerticalAngleDetailed, "logVerticalAngleDetailed", false);
            Scribe_Values.Look(ref swayReductionPercent, "swayReductionPercent", 0f);
            Scribe_Values.Look(ref recoilReductionPercent, "recoilReductionPercent", 0f);
            Scribe_Values.Look(ref spreadReductionPercent, "spreadReductionPercent", 0f);
            Scribe_Values.Look(ref showAccuracyOverrideInspect, "showAccuracyOverrideInspect", true);

            // Turret Component Logging
            Scribe_Values.Look(ref logTurretBarrel, "logTurretBarrel", false);
            Scribe_Values.Look(ref logTurretModeSwap, "logTurretModeSwap", false);
            Scribe_Values.Look(ref logTurretAmmo, "logTurretAmmo", false);
            Scribe_Values.Look(ref logTurretTarget, "logTurretTarget", false);
            Scribe_Values.Look(ref logTurretFireTimestamp, "logTurretFireTimestamp", false);

            // General Component Logging
            Scribe_Values.Look(ref logRotation, "logRotation", false);
            Scribe_Values.Look(ref logAnimation, "logAnimation", false);
            Scribe_Values.Look(ref logTurretSmoke, "logTurretSmoke", false);
            Scribe_Values.Look(ref logTurretSmokeParticleTelemetry, "logTurretSmokeParticleTelemetry", false);
            Scribe_Values.Look(ref logTurretSmokeParticleTick, "logTurretSmokeParticleTick", false);
            
            // Log settings after they're loaded/saved
            if (Scribe.mode == LoadSaveMode.LoadingVars || Scribe.mode == LoadSaveMode.Saving)
            {
                LogCurrentSettings();
            }
        }
        
        /// <summary>
        /// Log the current mod settings values
        /// </summary>
        private void LogCurrentSettings()
        {
            Log.Message("[AMC] ═══════════════════════════════════════");
            Log.Message("[AMC] Mod Settings:");
            Log.Message("[AMC] ═══════════════════════════════════════");
            Log.Message($"[AMC] Elevation Launch Logging: {logElevationLaunch}");
            Log.Message($"[AMC] Rotation Launch Logging: {logRotationLaunch}");
            Log.Message($"[AMC] ");
            Log.Message($"[AMC] Rotation Clamping Enabled: {enableRotationClamping}");
            Log.Message($"[AMC] Rotation Clamp Angle: ±{rotationClampAngle:F1}°");
            Log.Message($"[AMC] ");
            Log.Message($"[AMC] Elevation Clamping Enabled: {enableElevationClamping}");
            Log.Message($"[AMC] Minimum Elevation Angle: {minimumElevationAngle:F1}°");
            Log.Message($"[AMC] ");
            Log.Message($"[AMC] Rotation Diagnostics Logging: {logRotationDiagnostics}");
            Log.Message($"[AMC] Startup Logging: {logStartup}");
            Log.Message("[AMC] Temporary Debug Logging: " + logTemporaryDebug);
            Log.Message("[AMC] Vertical Angle Detailed Logging: " + logVerticalAngleDetailed);
            Log.Message("[AMC] Sway Reduction: " + swayReductionPercent + "%");
            Log.Message("[AMC] Recoil Reduction: " + recoilReductionPercent + "%");
            Log.Message("[AMC] Spread Reduction: " + spreadReductionPercent + "%");
            Log.Message($"[AMC] ");
            Log.Message($"[AMC] Turret Barrel Logging: {logTurretBarrel}");
            Log.Message($"[AMC] Turret Mode Swap Logging: {logTurretModeSwap}");
            Log.Message($"[AMC] Turret Ammo Logging: {logTurretAmmo}");
            Log.Message($"[AMC] Turret Target Logging: {logTurretTarget}");
            Log.Message($"[AMC] Turret Fire Timestamp Logging: {logTurretFireTimestamp}");
            Log.Message($"[AMC] ");
            Log.Message($"[AMC] Rotation Logging: {logRotation}");
            Log.Message($"[AMC] Animation Logging: {logAnimation}");
            Log.Message($"[AMC] Turret Smoke Logging: {logTurretSmoke}");
            Log.Message($"[AMC] Turret Smoke Particle Telemetry: {logTurretSmokeParticleTelemetry}");
            Log.Message($"[AMC] Turret Smoke Particle Tick Logging: {logTurretSmokeParticleTick}");
            Log.Message("[AMC] ═══════════════════════════════════════");
        }

        // Scroll position for settings window
        private Vector2 scrollPosition = Vector2.zero;
        private const float ContentHeight = 2000f; // Tall enough for all settings
        
        /// <summary>
        /// Draw the settings UI
        /// </summary>
        public void DoSettingsWindowContents(Rect inRect)
        {
            // Create scrollable view
            Rect viewRect = new Rect(0f, 0f, inRect.width - 16f, ContentHeight);
            Widgets.BeginScrollView(inRect, ref scrollPosition, viewRect);
            
            Listing_Standard listing = new Listing_Standard();
            listing.Begin(viewRect);

            // === HEADER ===
            Text.Font = GameFont.Medium;
            Text.Anchor = TextAnchor.MiddleCenter;
            Widgets.Label(listing.GetRect(40f), "Absolutely More Cannons - Debug Logging");
            Text.Anchor = TextAnchor.UpperLeft;
            Text.Font = GameFont.Small;
            listing.Gap();

            // === PROJECTILE LAUNCH LOGGING ===
            Widgets.Label(listing.GetRect(30f), "═══ Projectile Launch Logging ═══");
            listing.Gap(4);
            
            listing.CheckboxLabeled(
                "Log Elevation/Vertical Angle",
                ref logElevationLaunch
            );
            
            listing.CheckboxLabeled(
                "Log Rotation/Horizontal Angle",
                ref logRotationLaunch
            );

            listing.Gap();

            // === ROTATION CLAMPING ===
            Widgets.Label(listing.GetRect(30f), "═══ Rotation Clamping ═══");
            listing.Gap(4);
            
            listing.CheckboxLabeled(
                "Enable Rotation Clamping (Turrets Only)",
                ref enableRotationClamping
            );
            
            if (enableRotationClamping)
            {
                listing.Gap(4);
                Rect sliderRect = listing.GetRect(22f);
                Rect labelRect = sliderRect.LeftPart(0.7f);
                Rect valueRect = sliderRect.RightPart(0.25f);
                
                Widgets.Label(labelRect, $"  └─ Max Deviation Angle: ");
                rotationClampAngle = Widgets.HorizontalSlider(
                    labelRect.RightPart(0.6f),
                    rotationClampAngle,
                    0f,
                    45f,
                    true,
                    $"{rotationClampAngle:F1}°"
                );
                
                // Display current value
                Text.Anchor = TextAnchor.MiddleRight;
                Widgets.Label(valueRect, $"±{rotationClampAngle:F1}°");
                Text.Anchor = TextAnchor.UpperLeft;
                
                listing.Gap(4);
                // Add helper text using GetRect and Widgets.Label
                Text.Font = GameFont.Tiny;
                Rect helpTextRect = listing.GetRect(Text.LineHeight);
                Widgets.Label(helpTextRect, "  (This clamps the shotRotation field before projectile launch)");
                Text.Font = GameFont.Small;
            }

            listing.Gap();
            
            // === ELEVATION CLAMPING ===
            Widgets.Label(listing.GetRect(30f), "═══ Elevation Clamping ═══");
            listing.Gap(4);
            
            listing.CheckboxLabeled(
                "Enable Elevation Clamping (Prevent Ground Shots)",
                ref enableElevationClamping
            );
            
            if (enableElevationClamping)
            {
                listing.Gap(4);
                Rect sliderRect = listing.GetRect(22f);
                Rect labelRect = sliderRect.LeftPart(0.7f);
                Rect valueRect = sliderRect.RightPart(0.25f);
                
                Widgets.Label(labelRect, $"  └─ Minimum Elevation: ");
                minimumElevationAngle = Widgets.HorizontalSlider(
                    labelRect.RightPart(0.6f),
                    minimumElevationAngle,
                    0f,
                    10f,
                    true,
                    $"{minimumElevationAngle:F1}°"
                );
                
                // Display current value
                Text.Anchor = TextAnchor.MiddleRight;
                Widgets.Label(valueRect, $"≥{minimumElevationAngle:F1}°");
                Text.Anchor = TextAnchor.UpperLeft;
                
                listing.Gap(4);
                // Add helper text
                Text.Font = GameFont.Tiny;
                Rect helpTextRect = listing.GetRect(Text.LineHeight);
                Widgets.Label(helpTextRect, "  (Diagnostic mode: watch logs to verify angle reference frame)");
                Text.Font = GameFont.Small;
            }

            listing.Gap();
            
            // === DIAGNOSTIC LOGGING ===
            Widgets.Label(listing.GetRect(30f), "═══ Diagnostic Logging ═══");
            listing.Gap(4);
            
            listing.CheckboxLabeled(
                "Enable Rotation Diagnostics (AMC DIAGNOSTIC/OBSERVE)",
                ref logRotationDiagnostics
            );
            
            listing.Gap(4);
            Text.Font = GameFont.Tiny;
            Rect diagHelpRect = listing.GetRect(Text.LineHeight);
            Widgets.Label(diagHelpRect, "  (Detailed rotation/deviation analysis logs)");
            Text.Font = GameFont.Small;
            
            listing.Gap(8);
            
            listing.CheckboxLabeled(
                "Enable Startup Logs",
                ref logStartup
            );
            
            listing.Gap(4);
            Text.Font = GameFont.Tiny;
            Rect startupHelpRect = listing.GetRect(Text.LineHeight);
            Widgets.Label(startupHelpRect, "  (Mod initialization and patch success messages)");
            Text.Font = GameFont.Small;
            
            listing.Gap(8);
            
            
            listing.CheckboxLabeled(
                "Enable Temporary Debug Logs",
                ref logTemporaryDebug
            );
            
            listing.Gap(4);
            Text.Font = GameFont.Tiny;
            Rect tempDebugHelpRect = listing.GetRect(Text.LineHeight);
            Widgets.Label(tempDebugHelpRect, "  (Temporary debugging logs - e.g., projectile tick tracking)");
            Text.Font = GameFont.Small;

            listing.Gap(8);
            
            listing.CheckboxLabeled(
                "Enable Detailed Vertical Angle Logging",
                ref logVerticalAngleDetailed
            );
            
            listing.Gap(4);
            Text.Font = GameFont.Tiny;
            Rect vertAngleHelpRect = listing.GetRect(Text.LineHeight);
            Widgets.Label(vertAngleHelpRect, "  (Shows breakdown: ballistic + sway + recoil + spread for each shot)");
            Text.Font = GameFont.Small;

            listing.Gap();
            
            // === ACCURACY TUNING ===
            Widgets.Label(listing.GetRect(30f), "═══ Vertical Accuracy Tuning ═══");
            listing.Gap(4);
            
            // Sway Reduction Slider
            Rect swayLabelRect = listing.GetRect(Text.LineHeight);
            Widgets.Label(swayLabelRect, $"Sway Reduction: {swayReductionPercent:F0}%");
            Rect swaySliderRect = listing.GetRect(22f);
            swayReductionPercent = Widgets.HorizontalSlider(swaySliderRect, swayReductionPercent, 0f, 100f, true);
            listing.Gap(4);
            Text.Font = GameFont.Tiny;
            Rect swayHelpRect = listing.GetRect(Text.LineHeight);
            Widgets.Label(swayHelpRect, "  (Reduces weapon wobble - sin wave pattern)");
            Text.Font = GameFont.Small;
            listing.Gap(8);
            
            // Recoil Reduction Slider
            Rect recoilLabelRect = listing.GetRect(Text.LineHeight);
            Widgets.Label(recoilLabelRect, $"Recoil Reduction: {recoilReductionPercent:F0}%");
            Rect recoilSliderRect = listing.GetRect(22f);
            recoilReductionPercent = Widgets.HorizontalSlider(recoilSliderRect, recoilReductionPercent, 0f, 100f, true);
            listing.Gap(4);
            Text.Font = GameFont.Tiny;
            Rect recoilHelpRect = listing.GetRect(Text.LineHeight);
            Widgets.Label(recoilHelpRect, "  (Reduces shot-to-shot kick in bursts)");
            Text.Font = GameFont.Small;
            listing.Gap(8);
            
            // Spread Reduction Slider
            Rect spreadLabelRect = listing.GetRect(Text.LineHeight);
            Widgets.Label(spreadLabelRect, $"Spread Reduction: {spreadReductionPercent:F0}%");
            Rect spreadSliderRect = listing.GetRect(22f);
            spreadReductionPercent = Widgets.HorizontalSlider(spreadSliderRect, spreadReductionPercent, 0f, 100f, true);
            listing.Gap(4);
            Text.Font = GameFont.Tiny;
            Rect spreadHelpRect = listing.GetRect(Text.LineHeight);
            Widgets.Label(spreadHelpRect, "  (Reduces random mechanical variation)");
            Text.Font = GameFont.Small;

            listing.Gap(8);
            
            // Show Accuracy Override Info
            listing.CheckboxLabeled(
                "Show Accuracy Override Info (inspect panel)",
                ref showAccuracyOverrideInspect,
                "Display per-turret accuracy override values when selecting turrets"
            );

            listing.Gap();

            // === TURRET COMPONENT LOGGING ===
            Widgets.Label(listing.GetRect(30f), "═══ Turret Component Logging ═══");
            listing.Gap(4);

            listing.CheckboxLabeled(
                "Log Turret Barrel Events",
                ref logTurretBarrel
            );
            listing.CheckboxLabeled(
                "Log Turret Mode Swap",
                ref logTurretModeSwap
            );
            listing.CheckboxLabeled(
                "Log Turret Ammo",
                ref logTurretAmmo
            );
            listing.CheckboxLabeled(
                "Log Turret Target",
                ref logTurretTarget
            );
            listing.CheckboxLabeled(
                "Log Turret Fire Timestamp",
                ref logTurretFireTimestamp
            );

            listing.Gap();

            // === GENERAL COMPONENT LOGGING ===
            Widgets.Label(listing.GetRect(30f), "═══ General Component Logging ═══");
            listing.Gap(4);

            listing.CheckboxLabeled(
                "Log Rotation Events",
                ref logRotation
            );
            listing.CheckboxLabeled(
                "Log Animation Events",
                ref logAnimation
            );
            listing.CheckboxLabeled(
                "Log Turret Smoke (General)",
                ref logTurretSmoke
            );
            
            listing.Gap(4);
            listing.CheckboxLabeled(
                "  └─ Smoke Particle Telemetry (Detailed)",
                ref logTurretSmokeParticleTelemetry
            );
            Text.Font = GameFont.Tiny;
            Rect telemetryHelpRect = listing.GetRect(Text.LineHeight);
            Widgets.Label(telemetryHelpRect, "     (Logs particle spawn with full configuration and lifecycle events)");
            Text.Font = GameFont.Small;
            
            listing.Gap(4);
            listing.CheckboxLabeled(
                "  └─ Smoke Particle Tick Tracking (Compact)",
                ref logTurretSmokeParticleTick
            );
            Text.Font = GameFont.Tiny;
            Rect tickHelpRect = listing.GetRect(Text.LineHeight);
            Widgets.Label(tickHelpRect, "     (Logs position/velocity each tick - very verbose!)");
            Text.Font = GameFont.Small;

            listing.Gap(12);

            // === QUICK ACTIONS ===
            Widgets.Label(listing.GetRect(30f), "═══ Quick Actions ═══");
            listing.Gap(4);

            if (listing.ButtonText("Enable All Logs"))
            {
                EnableAllLogs();
            }
            if (listing.ButtonText("Disable All Logs"))
            {
                DisableAllLogs();
            }
            if (listing.ButtonText("Enable Only Projectile Launch Logs"))
            {
                DisableAllLogs();
                logElevationLaunch = true;
                logRotationLaunch = true;
            }

            listing.End();
            Widgets.EndScrollView();
        }

        private void EnableAllLogs()
        {
            logElevationLaunch = true;
            logRotationLaunch = true;
            logTurretBarrel = true;
            logTurretModeSwap = true;
            logTurretAmmo = true;
            logTurretTarget = true;
            logTurretFireTimestamp = true;
            logRotation = true;
            logAnimation = true;
            logTurretSmoke = true;
            logTurretSmokeParticleTelemetry = true;
            logTurretSmokeParticleTick = true;
        }

        private void DisableAllLogs()
        {
            logElevationLaunch = false;
            logRotationLaunch = false;
            logTurretBarrel = false;
            logTurretModeSwap = false;
            logTurretAmmo = false;
            logTurretTarget = false;
            logTurretFireTimestamp = false;
            logRotation = false;
            logAnimation = false;
            logTurretSmoke = false;
            logTurretSmokeParticleTelemetry = false;
            logTurretSmokeParticleTick = false;
        }
    }
}
