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

        // === Turret Component Logging ===
        public bool logTurretBarrel = false;
        public bool logTurretModeSwap = false;
        public bool logTurretAmmo = false;
        public bool logTurretTarget = false;

        // === General Component Logging ===
        public bool logRotation = false;
        public bool logAnimation = false;

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

            // Turret Component Logging
            Scribe_Values.Look(ref logTurretBarrel, "logTurretBarrel", false);
            Scribe_Values.Look(ref logTurretModeSwap, "logTurretModeSwap", false);
            Scribe_Values.Look(ref logTurretAmmo, "logTurretAmmo", false);
            Scribe_Values.Look(ref logTurretTarget, "logTurretTarget", false);

            // General Component Logging
            Scribe_Values.Look(ref logRotation, "logRotation", false);
            Scribe_Values.Look(ref logAnimation, "logAnimation", false);
            
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
            Log.Message($"[AMC] Temporary Debug Logging: {logTemporaryDebug}");
            Log.Message($"[AMC] ");
            Log.Message($"[AMC] Turret Barrel Logging: {logTurretBarrel}");
            Log.Message($"[AMC] Turret Mode Swap Logging: {logTurretModeSwap}");
            Log.Message($"[AMC] Turret Ammo Logging: {logTurretAmmo}");
            Log.Message($"[AMC] Turret Target Logging: {logTurretTarget}");
            Log.Message($"[AMC] ");
            Log.Message($"[AMC] Rotation Logging: {logRotation}");
            Log.Message($"[AMC] Animation Logging: {logAnimation}");
            Log.Message("[AMC] ═══════════════════════════════════════");
        }

        /// <summary>
        /// Draw the settings UI
        /// </summary>
        public void DoSettingsWindowContents(Rect inRect)
        {
            Listing_Standard listing = new Listing_Standard();
            listing.Begin(inRect);

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
        }

        private void EnableAllLogs()
        {
            logElevationLaunch = true;
            logRotationLaunch = true;
            logTurretBarrel = true;
            logTurretModeSwap = true;
            logTurretAmmo = true;
            logTurretTarget = true;
            logRotation = true;
            logAnimation = true;
        }

        private void DisableAllLogs()
        {
            logElevationLaunch = false;
            logRotationLaunch = false;
            logTurretBarrel = false;
            logTurretModeSwap = false;
            logTurretAmmo = false;
            logTurretTarget = false;
            logRotation = false;
            logAnimation = false;
        }
    }
}
