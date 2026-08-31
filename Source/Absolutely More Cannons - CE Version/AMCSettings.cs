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
        // === Master Logging Toggle ===
        public bool enableLogging = false;

        // === Projectile Launch Logging ===
        private bool _logElevationLaunch = false;
        private bool _logRotationLaunch = false;
        private bool _logTurretClamping = false;

        public bool logElevationLaunch { get => enableLogging && _logElevationLaunch; set => _logElevationLaunch = value; }
        public bool logRotationLaunch { get => enableLogging && _logRotationLaunch; set => _logRotationLaunch = value; }
        public bool logTurretClamping { get => enableLogging && _logTurretClamping; set => _logTurretClamping = value; }
        
        // === Diagnostic Logging ===
        private bool _logRotationDiagnostics = false;
        private bool _logStartup = true;
        private bool _logTemporaryDebug = false;
        private bool _logFragmentProjectiles = false;
        private bool _logVerticalAngleDetailed = false;
        private bool _logTurretViewTransfer = false;

        public bool logRotationDiagnostics { get => enableLogging && _logRotationDiagnostics; set => _logRotationDiagnostics = value; }
        public bool logStartup { get => enableLogging && _logStartup; set => _logStartup = value; }
        public bool logTemporaryDebug { get => enableLogging && _logTemporaryDebug; set => _logTemporaryDebug = value; }
        public bool logFragmentProjectiles { get => enableLogging && _logFragmentProjectiles; set => _logFragmentProjectiles = value; }
        public bool logVerticalAngleDetailed { get => enableLogging && _logVerticalAngleDetailed; set => _logVerticalAngleDetailed = value; }
        public bool logTurretViewTransfer { get => enableLogging && _logTurretViewTransfer; set => _logTurretViewTransfer = value; }

        // === Individual Accuracy Tuning (0-100%) ===
        public float swayReductionPercent = 0f;    // 0% = full sway, 100% = no sway
        public float recoilReductionPercent = 0f;  // 0% = full recoil, 100% = no recoil
        public float spreadReductionPercent = 0f;  // 0% = full spread, 100% = no spread
        public bool showAccuracyOverrideInspect = true; // Show per-turret accuracy overrides in inspect panel

        // === Turret Component Logging ===
        private bool _logTurretBarrel = false;
        private bool _logTurretModeSwap = false;
        private bool _logTurretAmmo = false;
        private bool _logTurretTarget = false;
        private bool _logTurretFireTimestamp = false;
        private bool _logFCS = false;

        public bool logTurretBarrel { get => enableLogging && _logTurretBarrel; set => _logTurretBarrel = value; }
        public bool logTurretModeSwap { get => enableLogging && _logTurretModeSwap; set => _logTurretModeSwap = value; }
        public bool logTurretAmmo { get => enableLogging && _logTurretAmmo; set => _logTurretAmmo = value; }
        public bool logTurretTarget { get => enableLogging && _logTurretTarget; set => _logTurretTarget = value; }
        public bool logTurretFireTimestamp { get => enableLogging && _logTurretFireTimestamp; set => _logTurretFireTimestamp = value; }
        public bool logFCS { get => enableLogging && _logFCS; set => _logFCS = value; }

        // === General Component Logging ===
        private bool _logRotation = false;
        private bool _logAnimation = false;
        private bool _logTurretSmoke = false;
        private bool _logTurretSmokeParticleTelemetry = false;
        private bool _logTurretSmokeParticleTick = false;
        private bool _logProjectileOffsets = false;
        private bool _logAirburstDetonation = false;
        private bool _logProjectileTracers = false;
        private bool _logMuzzleFlashMod = false;

        public bool logRotation { get => enableLogging && _logRotation; set => _logRotation = value; }
        public bool logAnimation { get => enableLogging && _logAnimation; set => _logAnimation = value; }
        public bool logTurretSmoke { get => enableLogging && _logTurretSmoke; set => _logTurretSmoke = value; }
        public bool logTurretSmokeParticleTelemetry { get => enableLogging && _logTurretSmokeParticleTelemetry; set => _logTurretSmokeParticleTelemetry = value; }
        public bool logTurretSmokeParticleTick { get => enableLogging && _logTurretSmokeParticleTick; set => _logTurretSmokeParticleTick = value; }
        public bool logProjectileOffsets { get => enableLogging && _logProjectileOffsets; set => _logProjectileOffsets = value; }
        public bool logAirburstDetonation { get => enableLogging && _logAirburstDetonation; set => _logAirburstDetonation = value; }
        public bool logProjectileTracers { get => enableLogging && _logProjectileTracers; set => _logProjectileTracers = value; }
        public bool logMuzzleFlashMod { get => enableLogging && _logMuzzleFlashMod; set => _logMuzzleFlashMod = value; }

        /// <summary>
        /// Check if third-party Muzzle Flash mod (by IssacZhuang) is loaded in current session.
        /// </summary>
        public static bool IsMuzzleFlashModActive => Verse.GenTypes.GetTypeInAnyAssembly("MuzzleFlash.Patch.HarmonyPatch_Verb") != null 
                                                   || HarmonyLib.AccessTools.TypeByName("MuzzleFlash.Patch.HarmonyPatch_Verb") != null;

        /// <summary>
        /// Save and load settings from XML
        /// </summary>
        public override void ExposeData()
        {
            base.ExposeData();

            // Master Logging Toggle
            Scribe_Values.Look(ref enableLogging, "enableLogging", false);

            // Projectile Launch Logging
            Scribe_Values.Look(ref _logElevationLaunch, "logElevationLaunch", false);
            Scribe_Values.Look(ref _logRotationLaunch, "logRotationLaunch", false);
            Scribe_Values.Look(ref _logTurretClamping, "logTurretClamping", false);
            
            // Diagnostic Logging
            Scribe_Values.Look(ref _logRotationDiagnostics, "logRotationDiagnostics", false);
            Scribe_Values.Look(ref _logStartup, "logStartup", true);
            Scribe_Values.Look(ref _logTemporaryDebug, "logTemporaryDebug", false);
            Scribe_Values.Look(ref _logFragmentProjectiles, "logFragmentProjectiles", false);
            Scribe_Values.Look(ref _logVerticalAngleDetailed, "logVerticalAngleDetailed", false);
            Scribe_Values.Look(ref _logTurretViewTransfer, "logTurretViewTransfer", false);
            Scribe_Values.Look(ref swayReductionPercent, "swayReductionPercent", 0f);
            Scribe_Values.Look(ref recoilReductionPercent, "recoilReductionPercent", 0f);
            Scribe_Values.Look(ref spreadReductionPercent, "spreadReductionPercent", 0f);
            Scribe_Values.Look(ref showAccuracyOverrideInspect, "showAccuracyOverrideInspect", true);

            // Turret Component Logging
            Scribe_Values.Look(ref _logTurretBarrel, "logTurretBarrel", false);
            Scribe_Values.Look(ref _logTurretModeSwap, "logTurretModeSwap", false);
            Scribe_Values.Look(ref _logTurretAmmo, "logTurretAmmo", false);
            Scribe_Values.Look(ref _logTurretTarget, "logTurretTarget", false);
            Scribe_Values.Look(ref _logTurretFireTimestamp, "logTurretFireTimestamp", false);
            Scribe_Values.Look(ref _logFCS, "logFCS", false);

            // General Component Logging
            Scribe_Values.Look(ref _logRotation, "logRotation", false);
            Scribe_Values.Look(ref _logAnimation, "logAnimation", false);
            Scribe_Values.Look(ref _logTurretSmoke, "logTurretSmoke", false);
            Scribe_Values.Look(ref _logTurretSmokeParticleTelemetry, "logTurretSmokeParticleTelemetry", false);
            Scribe_Values.Look(ref _logTurretSmokeParticleTick, "logTurretSmokeParticleTick", false);
            Scribe_Values.Look(ref _logProjectileOffsets, "logProjectileOffsets", false);
            Scribe_Values.Look(ref _logAirburstDetonation, "logAirburstDetonation", false);
            Scribe_Values.Look(ref _logProjectileTracers, "logProjectileTracers", false);
            Scribe_Values.Look(ref _logMuzzleFlashMod, "logMuzzleFlashMod", false);
            
            // Log settings after they're loaded/saved ONLY if startup logging is enabled
            if ((Scribe.mode == LoadSaveMode.LoadingVars || Scribe.mode == LoadSaveMode.Saving) && logStartup)
            {
                LogCurrentSettings();
            }
        }
        
        /// <summary>
        /// Log the current mod settings values
        /// </summary>
        private void LogCurrentSettings()
        {
            if (!enableLogging) return;

            Log.Message("[AMC] =======================================");
            Log.Message("[AMC] Mod Settings:");
            Log.Message("[AMC] =======================================");
            Log.Message($"[AMC] Master Logging Enabled: {enableLogging}");
            Log.Message($"[AMC] Elevation Launch Logging: {logElevationLaunch}");
            Log.Message($"[AMC] Rotation Launch Logging: {logRotationLaunch}");
            Log.Message($"[AMC] Turret Clamping Logging: {logTurretClamping}");
            Log.Message($"[AMC] ");
            Log.Message($"[AMC] Rotation Diagnostics Logging: {logRotationDiagnostics}");
            Log.Message($"[AMC] Startup Logging: {logStartup}");
            Log.Message("[AMC] Temporary Debug Logging: " + logTemporaryDebug);
            Log.Message("[AMC] Fragment Projectile Logging: " + logFragmentProjectiles);
            Log.Message("[AMC] Vertical Angle Detailed Logging: " + logVerticalAngleDetailed);
            Log.Message("[AMC] Turret View Transfer Logging: " + logTurretViewTransfer);
            Log.Message("[AMC] Sway Reduction: " + swayReductionPercent + "%");
            Log.Message("[AMC] Recoil Reduction: " + recoilReductionPercent + "%");
            Log.Message("[AMC] Spread Reduction: " + spreadReductionPercent + "%");
            Log.Message($"[AMC] ");
            Log.Message($"[AMC] Turret Barrel Logging: {logTurretBarrel}");
            Log.Message($"[AMC] Turret Mode Swap Logging: {logTurretModeSwap}");
            Log.Message($"[AMC] Turret Ammo Logging: {logTurretAmmo}");
            Log.Message($"[AMC] Turret Target Logging: {logTurretTarget}");
            Log.Message($"[AMC] Turret Fire Timestamp Logging: {logTurretFireTimestamp}");
            Log.Message($"[AMC] FCS Telemetry Logging: {logFCS}");
            Log.Message($"[AMC] ");
            Log.Message($"[AMC] Rotation Logging: {logRotation}");
            Log.Message($"[AMC] Animation Logging: {logAnimation}");
            Log.Message($"[AMC] Turret Smoke Logging: {logTurretSmoke}");
            Log.Message($"[AMC] Turret Smoke Particle Telemetry: {logTurretSmokeParticleTelemetry}");
            Log.Message($"[AMC] Turret Smoke Particle Tick Logging: {logTurretSmokeParticleTick}");
            Log.Message("[AMC] Projectile Offsets Logging: " + logProjectileOffsets);
            Log.Message("[AMC] Airburst Detonation Logging: " + logAirburstDetonation);
            if (IsMuzzleFlashModActive)
            {
                Log.Message("[AMC] Muzzle Flash Mod Logging: " + logMuzzleFlashMod);
            }
            Log.Message("[AMC] =======================================");
        }

        // Scroll position for settings window
        private Vector2 scrollPosition = Vector2.zero;
        
        /// <summary>
        /// Draw the settings UI
        /// </summary>
        public void DoSettingsWindowContents(Rect inRect)
        {
            float contentHeight = enableLogging ? 2000f : 800f;
            // Create scrollable view
            Rect viewRect = new Rect(0f, 0f, inRect.width - 16f, contentHeight);
            Widgets.BeginScrollView(inRect, ref scrollPosition, viewRect);
            
            Listing_Standard listing = new Listing_Standard();
            listing.Begin(viewRect);

            // === HEADER ===
            Text.Font = GameFont.Medium;
            Text.Anchor = TextAnchor.MiddleCenter;
            Widgets.Label(listing.GetRect(40f), "Absolutely More Cannons - Mod Settings");
            Text.Anchor = TextAnchor.UpperLeft;
            Text.Font = GameFont.Small;
            listing.Gap();

            // =========================================================================
            // === GAMEPLAY RELATED SETTINGS (POSITIONED ON TOP OF LIST) ===
            // =========================================================================


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

            listing.Gap(16);

            // =========================================================================
            // === DEBUG LOGGING (UPPER LEVEL TOGGLE AND LOG TOGGLES) ===
            // =========================================================================

            Widgets.Label(listing.GetRect(30f), "═══ Debug Logging ═══");
            listing.Gap(4);

            listing.CheckboxLabeled(
                "Enable Debug Logging",
                ref enableLogging,
                "Master toggle to enable or disable all mod logging"
            );
            Text.Font = GameFont.Tiny;
            Rect masterHelpRect = listing.GetRect(Text.LineHeight);
            Widgets.Label(masterHelpRect, "  (Master toggle: enables/disables all logging output and displays log categories below)");
            Text.Font = GameFont.Small;

            if (enableLogging)
            {
                listing.Gap(12);

                // === PROJECTILE LAUNCH LOGGING ===
                Widgets.Label(listing.GetRect(30f), "  ═══ Projectile Launch Logging ═══");
                listing.Gap(4);
                
                listing.CheckboxLabeled(
                    "  Log Elevation/Vertical Angle",
                    ref _logElevationLaunch
                );
                
                listing.CheckboxLabeled(
                    "  Log Rotation/Horizontal Angle",
                    ref _logRotationLaunch
                );

                listing.CheckboxLabeled(
                    "  Log Turret Clamping & Elevation Deviation",
                    ref _logTurretClamping
                );

                listing.Gap();

                // === DIAGNOSTIC LOGGING ===
                Widgets.Label(listing.GetRect(30f), "  ═══ Diagnostic Logging ═══");
                listing.Gap(4);
                
                listing.CheckboxLabeled(
                    "  Enable Rotation Diagnostics (AMC DIAGNOSTIC/OBSERVE)",
                    ref _logRotationDiagnostics
                );
                
                listing.Gap(4);
                Text.Font = GameFont.Tiny;
                Rect diagHelpRect = listing.GetRect(Text.LineHeight);
                Widgets.Label(diagHelpRect, "    (Detailed rotation/deviation analysis logs)");
                Text.Font = GameFont.Small;
                
                listing.Gap(8);
                
                listing.CheckboxLabeled(
                    "  Enable Startup Logs",
                    ref _logStartup
                );
                
                listing.Gap(4);
                Text.Font = GameFont.Tiny;
                Rect startupHelpRect = listing.GetRect(Text.LineHeight);
                Widgets.Label(startupHelpRect, "    (Mod initialization and patch success messages)");
                Text.Font = GameFont.Small;
                
                listing.Gap(8);
                
                listing.CheckboxLabeled(
                    "  Enable Temporary Debug Logs",
                    ref _logTemporaryDebug
                );
                
                if (_logTemporaryDebug)
                {
                    listing.Gap(4);
                    listing.CheckboxLabeled(
                        "    └─ Include Fragment Projectile Tracking (Launch/Tick/Destroyed)",
                        ref _logFragmentProjectiles
                    );
                    Text.Font = GameFont.Tiny;
                    Rect fragHelpRect = listing.GetRect(Text.LineHeight);
                    Widgets.Label(fragHelpRect, "       (Off by default to suppress fragment projectile log flooding)");
                    Text.Font = GameFont.Small;
                }
                
                listing.Gap(4);
                Text.Font = GameFont.Tiny;
                Rect tempDebugHelpRect = listing.GetRect(Text.LineHeight);
                Widgets.Label(tempDebugHelpRect, "    (Temporary debugging logs - e.g., projectile tick tracking)");
                Text.Font = GameFont.Small;

                listing.Gap(8);
                
                listing.CheckboxLabeled(
                    "  Enable Detailed Vertical Angle Logging",
                    ref _logVerticalAngleDetailed
                );
                
                listing.Gap(4);
                Text.Font = GameFont.Tiny;
                Rect vertAngleHelpRect = listing.GetRect(Text.LineHeight);
                Widgets.Label(vertAngleHelpRect, "    (Shows breakdown: ballistic + sway + recoil + spread for each shot)");
                Text.Font = GameFont.Small;

                listing.Gap(8);

                listing.CheckboxLabeled(
                    "  Enable Turret View Transfer & LoS Bypass Logging",
                    ref _logTurretViewTransfer
                );
                
                listing.Gap(4);
                Text.Font = GameFont.Tiny;
                Rect viewTransferHelpRect = listing.GetRect(Text.LineHeight);
                Widgets.Label(viewTransferHelpRect, "    (Logs target searcher origin transfers and self-occlusion LineOfSight checks)");
                Text.Font = GameFont.Small;

                listing.Gap();

                // === TURRET COMPONENT LOGGING ===
                Widgets.Label(listing.GetRect(30f), "  ═══ Turret Component Logging ═══");
                listing.Gap(4);

                listing.CheckboxLabeled(
                    "  Log Turret Barrel Events",
                    ref _logTurretBarrel
                );
                listing.CheckboxLabeled(
                    "  Log Turret Mode Swap",
                    ref _logTurretModeSwap
                );
                listing.CheckboxLabeled(
                    "  Log Turret Ammo",
                    ref _logTurretAmmo
                );
                listing.CheckboxLabeled(
                    "  Log Turret Target",
                    ref _logTurretTarget
                );
                listing.CheckboxLabeled(
                    "  Log Turret Fire Timestamp",
                    ref _logTurretFireTimestamp
                );
                listing.CheckboxLabeled(
                    "  Log FCS Performance & Accuracy Telemetry",
                    ref _logFCS,
                    "Logs FCS warmup reductions, firing cone spread calculations, and extended range targeting (off by default)."
                );

                listing.Gap();

                // === GENERAL COMPONENT LOGGING ===
                Widgets.Label(listing.GetRect(30f), "  ═══ General Component Logging ═══");
                listing.Gap(4);

                listing.CheckboxLabeled(
                    "  Log Rotation Events",
                    ref _logRotation
                );
                listing.CheckboxLabeled(
                    "  Log Animation Events",
                    ref _logAnimation
                );
                listing.CheckboxLabeled(
                    "  Log Turret Smoke (General)",
                    ref _logTurretSmoke
                );
                
                listing.Gap(4);
                listing.CheckboxLabeled(
                    "    └─ Smoke Particle Telemetry (Detailed)",
                    ref _logTurretSmokeParticleTelemetry
                );
                Text.Font = GameFont.Tiny;
                Rect telemetryHelpRect = listing.GetRect(Text.LineHeight);
                Widgets.Label(telemetryHelpRect, "       (Logs particle spawn with full configuration and lifecycle events)");
                Text.Font = GameFont.Small;
                
                listing.Gap(4);
                listing.CheckboxLabeled(
                    "    └─ Smoke Particle Tick Tracking (Compact)",
                    ref _logTurretSmokeParticleTick
                );
                Text.Font = GameFont.Tiny;
                Rect tickHelpRect = listing.GetRect(Text.LineHeight);
                Widgets.Label(tickHelpRect, "       (Logs position/velocity each tick - very verbose!)");
                Text.Font = GameFont.Small;

                listing.Gap(8);
                
                listing.CheckboxLabeled(
                    "  Log Projectile Spawn Offsets",
                    ref _logProjectileOffsets
                );
                Text.Font = GameFont.Tiny;
                Rect offsetHelpRect = listing.GetRect(Text.LineHeight);
                Widgets.Label(offsetHelpRect, "    (Logs forward and lateral projectile spawn position offsets)");
                Text.Font = GameFont.Small;

                listing.Gap(4);
                listing.CheckboxLabeled(
                    "  Log Projectile Tracer Lines",
                    ref _logProjectileTracers
                );
                Text.Font = GameFont.Tiny;
                Rect tracerHelpRect = listing.GetRect(Text.LineHeight);
                Widgets.Label(tracerHelpRect, "    (Logs tracer startPos, endPos, screen positions, and direction vectors per frame)");
                Text.Font = GameFont.Small;

                if (IsMuzzleFlashModActive)
                {
                    listing.Gap(4);
                    listing.CheckboxLabeled(
                        "  Log Muzzle Flash Mod Events",
                        ref _logMuzzleFlashMod
                    );
                    Text.Font = GameFont.Tiny;
                    Rect mfHelpRect = listing.GetRect(Text.LineHeight);
                    Widgets.Label(mfHelpRect, "    (Logs allowed vs suppressed flash graphics from third-party Muzzle Flash mod)");
                    Text.Font = GameFont.Small;
                }

                listing.Gap(12);

                // === QUICK ACTIONS ===
                Widgets.Label(listing.GetRect(30f), "  ═══ Quick Actions ═══");
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
                    EnableAllLogs();
                    _logElevationLaunch = true;
                    _logRotationLaunch = true;
                    _logStartup = false;
                    _logRotationDiagnostics = false;
                    _logVerticalAngleDetailed = false;
                    _logTurretBarrel = false;
                    _logTurretModeSwap = false;
                    _logTurretAmmo = false;
                    _logTurretTarget = false;
                    _logTurretFireTimestamp = false;
                    _logFCS = false;
                    _logRotation = false;
                    _logAnimation = false;
                    _logTurretSmoke = false;
                    _logTurretSmokeParticleTelemetry = false;
                    _logTurretSmokeParticleTick = false;
                    _logProjectileOffsets = false;
                    _logAirburstDetonation = false;
                    _logProjectileTracers = false;
                    _logTemporaryDebug = false;
                    _logFragmentProjectiles = false;
                    _logTurretViewTransfer = false;
                    _logMuzzleFlashMod = false;
                }
            }

            listing.End();
            Widgets.EndScrollView();
        }

        private void EnableAllLogs()
        {
            enableLogging = true;
            _logElevationLaunch = true;
            _logRotationLaunch = true;
            _logTurretClamping = true;
            _logStartup = true;
            _logRotationDiagnostics = true;
            _logVerticalAngleDetailed = true;
            _logTurretBarrel = true;
            _logTurretModeSwap = true;
            _logTurretAmmo = true;
            _logTurretTarget = true;
            _logTurretFireTimestamp = true;
            _logFCS = true;
            _logRotation = true;
            _logAnimation = true;
            _logTurretSmoke = true;
            _logTurretSmokeParticleTelemetry = true;
            _logTurretSmokeParticleTick = true;
            _logProjectileOffsets = true;
            _logAirburstDetonation = true;
            _logProjectileTracers = true;
            _logTemporaryDebug = true;
            _logFragmentProjectiles = true;
            _logTurretViewTransfer = true;
            if (IsMuzzleFlashModActive)
            {
                _logMuzzleFlashMod = true;
            }
        }

        private void DisableAllLogs()
        {
            enableLogging = false;
            _logElevationLaunch = false;
            _logRotationLaunch = false;
            _logTurretClamping = false;
            _logStartup = false;
            _logRotationDiagnostics = false;
            _logVerticalAngleDetailed = false;
            _logTurretBarrel = false;
            _logTurretModeSwap = false;
            _logTurretAmmo = false;
            _logTurretTarget = false;
            _logTurretFireTimestamp = false;
            _logFCS = false;
            _logRotation = false;
            _logAnimation = false;
            _logTurretSmoke = false;
            _logTurretSmokeParticleTelemetry = false;
            _logTurretSmokeParticleTick = false;
            _logProjectileOffsets = false;
            _logAirburstDetonation = false;
            _logProjectileTracers = false;
            _logTemporaryDebug = false;
            _logFragmentProjectiles = false;
            _logTurretViewTransfer = false;
            _logMuzzleFlashMod = false;
        }
    }
}
