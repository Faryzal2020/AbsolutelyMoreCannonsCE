using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using HarmonyLib;
using Verse;
using RimWorld; // Add this using directive
using UnityEngine; // Add this using directive

namespace AbsolutelyMoreCannons
{
    /// <summary>
    /// Harmony patches to integrate turret barrel animations with CombatExtended.
    /// </summary>
    [StaticConstructorOnStartup]
    public static class HarmonyPatches
    {
        static HarmonyPatches()
        {
            var harmony = new Harmony("AbsolutelyMoreCannons.TurretBarrelAnimation");

            // Patch GenDraw.DrawRadiusRing to handle large turret ranges (>70 tiles)
            TryPatchLargeRadiusRing(harmony);
            
            // Patch Verb.TryCastNextBurstShot for RPM override
            harmony.Patch(
                original: AccessTools.Method(typeof(Verb), "TryCastNextBurstShot"),
                postfix: new HarmonyMethod(typeof(HarmonyPatches), nameof(Postfix_Verb_TryCastNextBurstShot))
            );

            // Try to patch CE turret methods at runtime
            TryPatchCETurrets(harmony);
        }

        /// <summary>
        /// Patches GenDraw.DrawRadiusRing to handle large turret ranges (>70 tiles).
        /// Vanilla RimWorld has a precalculated list that doesn't support very large radii.
        /// </summary>
        private static void TryPatchLargeRadiusRing(Harmony harmony)
        {
            try
            {
                // Find the DrawRadiusRing method - there are multiple overloads
                // We need the one with (IntVec3 center, float radius) signature
                var drawRadiusRingMethod = AccessTools.Method(typeof(GenDraw), "DrawRadiusRing", 
                    new Type[] { typeof(IntVec3), typeof(float) });
                
                if (drawRadiusRingMethod != null)
                {
                    harmony.Patch(
                        original: drawRadiusRingMethod,
                        prefix: new HarmonyMethod(typeof(HarmonyPatches), nameof(Prefix_DrawRadiusRing))
                    );
                    Log.Message("Turret Barrel Animation: Patched GenDraw.DrawRadiusRing for large radius support.");
                }
                else
                {
                    Log.Warning("Turret Barrel Animation: Could not find GenDraw.DrawRadiusRing(IntVec3, float) method.");
                }
            }
            catch (Exception ex)
            {
                Log.Warning($"Turret Barrel Animation: Error patching DrawRadiusRing: {ex.Message}");
            }
        }

        /// <summary>
        /// Prefix patch for GenDraw.DrawRadiusRing to handle large radii (>70 tiles).
        /// Draws a simplified ring for large radii to avoid the precalculated list error.
        /// </summary>
        public static bool Prefix_DrawRadiusRing(IntVec3 center, float radius)
        {
            // If radius is too large, use simplified drawing
            if (radius > 70f)
            {
                DrawLargeRadiusRing(center, radius);
                return false; // Skip original method
            }
            return true; // Run original method for normal radii
        }

        /// <summary>
        /// Draws a simplified radius ring for large turret ranges.
        /// Uses density-based markers that scale with radius to avoid the precalculated list limitation.
        /// </summary>
        private static void DrawLargeRadiusRing(IntVec3 center, float radius)
        {
            try
            {
                Vector3 centerPos = center.ToVector3ShiftedWithAltitude(AltitudeLayer.MetaOverlays);
                Color color = Color.white;
                
                DrawDensityBasedMarkers(centerPos, radius, color);
            }
            catch (Exception ex)
            {
                // If drawing fails, just skip it - better than crashing
                Log.Warning($"Turret Barrel Animation: Error drawing large radius ring: {ex.Message}");
            }
        }

        /// <summary>
        /// Draws markers around a circle based on density percentage.
        /// Marker count scales with radius to maintain consistent visual density.
        /// </summary>
        private static void DrawDensityBasedMarkers(Vector3 center, float radius, Color color)
        {
            // Density as percentage: 0.5 = 50% filled (max requested), 1.0 = 100% filled (no gaps)
            const float MARKER_DENSITY = 0.5f; // 50% density - maximum requested
            const int MIN_MARKERS = 16; // Minimum markers for small large radii
            const int MAX_MARKERS = 200; // Maximum markers to avoid performance issues
            
            // Calculate circumference in tiles
            float circumference = 2f * Mathf.PI * radius;
            
            // Marker size (in tiles) - approximate size of the marker mesh
            float markerSizeTiles = 0.8f;
            
            // Calculate how many markers would fit if placed continuously (100% density)
            float maxPossibleMarkers = circumference / markerSizeTiles;
            
            // Apply density percentage
            int markerCount = Mathf.RoundToInt(maxPossibleMarkers * MARKER_DENSITY);
            markerCount = Mathf.Clamp(markerCount, MIN_MARKERS, MAX_MARKERS);
            
            // Angle step between each marker
            float angleStep = 360f / markerCount;
            
            // Use the same material as vanilla for consistency
            Material markerMaterial = GenDraw.InteractionCellMaterial;
            
            // Draw markers around the circle
            for (int i = 0; i < markerCount; i++)
            {
                float angle = i * angleStep;
                float radians = angle * Mathf.Deg2Rad;
                
                // RimWorld coordinate system: X = East/West, Z = North/South
                // Rotation: 0° = North, 90° = East
                Vector3 offset = new Vector3(
                    Mathf.Sin(radians) * radius,  // X (East/West)
                    0f,
                    Mathf.Cos(radians) * radius    // Z (North/South)
                );
                
                Vector3 markerPos = center + offset;
                
                Graphics.DrawMesh(
                    MeshPool.plane10,
                    markerPos,
                    Quaternion.identity,
                    markerMaterial,
                    0
                );
            }
        }

        private static void TryPatchCETurrets(Harmony harmony)
        {
            // Find CE turret class at runtime
            var ceTurretType = AccessTools.TypeByName("CombatExtended.Building_TurretGunCE");

            if (ceTurretType != null)
            {
                Log.Message("Turret Barrel Animation: Found CombatExtended turrets, applying patches.");

                // Patch CE verb firing to trigger barrel animations
                // We patch Verb_LaunchProjectileCE.TryCastShot because this is called when CE turrets actually fire
                // This is more reliable than BeginBurst which can be called even when the verb can't fire
                TryPatchCEVerbFiring(harmony);

                // Patch Verb_ShootCE.ShotsPerBurstFor for burst count override
                var shootCEType = AccessTools.TypeByName("CombatExtended.Verb_ShootCE");
                if (shootCEType != null)
                {
                    var shotsPerBurstForMethod = AccessTools.Method(shootCEType, "ShotsPerBurstFor");
                    if (shotsPerBurstForMethod != null)
                    {
                        harmony.Patch(
                            original: shotsPerBurstForMethod,
                            postfix: new HarmonyMethod(typeof(HarmonyPatches), nameof(Postfix_Verb_ShotsPerBurstFor))
                        );
                        Log.Message("Turret Barrel Animation: Patched Verb_ShootCE.ShotsPerBurstFor");
                    }
                }

                // Patch burst completion to trigger recoil
                var burstCompleteMethod = AccessTools.Method(ceTurretType, "BurstComplete");
                if (burstCompleteMethod != null)
                {
                    harmony.Patch(
                        original: burstCompleteMethod,
                        postfix: new HarmonyMethod(typeof(HarmonyPatches), nameof(Postfix_BurstComplete))
                    );
                }

                // Patch the turret building's DrawAt to draw barrel after turret top if needed
                var drawAtMethod = AccessTools.Method(ceTurretType, "DrawAt");
                if (drawAtMethod != null)
                {
                    harmony.Patch(
                        original: drawAtMethod,
                        postfix: new HarmonyMethod(typeof(HarmonyPatches), nameof(Postfix_TurretDrawAt))
                    );
                }

                // Patch turret top drawing to ensure barrel draws after turret top
                TryPatchCETurretTop(harmony);
            }
            else
            {
                Log.Message("Turret Barrel Animation: CombatExtended not found. Barrel animations will only work with basic recoil.");
            }
        }

        /// <summary>
        /// Attempts to patch CE turret top drawing to ensure barrel draws after turret top.
        /// </summary>
        private static void TryPatchCETurretTop(Harmony harmony)
        {
            // Try to find CE turret top class
            var ceTurretTopType = AccessTools.TypeByName("CombatExtended.TurretTop");
            
            if (ceTurretTopType != null)
            {
                Log.Message("Turret Barrel Animation: Found CE TurretTop class, patching Draw method.");
                
                // Try to patch the Draw method
                var drawMethod = AccessTools.Method(ceTurretTopType, "Draw");
                if (drawMethod != null)
                {
                    harmony.Patch(
                        original: drawMethod,
                        postfix: new HarmonyMethod(typeof(HarmonyPatches), nameof(Postfix_TurretTopDraw))
                    );
                    Log.Message("Turret Barrel Animation: Successfully patched CE TurretTop.Draw method.");
                }
                else
                {
                    // Try alternative method names
                    var drawAtMethod = AccessTools.Method(ceTurretTopType, "DrawAt");
                    if (drawAtMethod != null)
                    {
                        harmony.Patch(
                            original: drawAtMethod,
                            postfix: new HarmonyMethod(typeof(HarmonyPatches), nameof(Postfix_TurretTopDraw))
                        );
                        Log.Message("Turret Barrel Animation: Successfully patched CE TurretTop.DrawAt method.");
                    }
                    else
                    {
                        Log.Warning("Turret Barrel Animation: Could not find Draw or DrawAt method in CE TurretTop class.");
                    }
                }
            }
            else
            {
                Log.Message("Turret Barrel Animation: CE TurretTop class not found. Using Y offset method only.");
            }
        }

        /// <summary>
        /// Patches CE verb firing using Harmony's TargetMethod approach for runtime method discovery.
        /// Also patches warmup-related methods to provide warmup event notifications.
        /// </summary>
        private static void TryPatchCEVerbFiring(Harmony harmony)
        {
            var verbType = AccessTools.TypeByName("CombatExtended.Verb_LaunchProjectileCE");
            if (verbType == null)
            {
                //Log.Warning("[Barrel Flash Debug] Verb_LaunchProjectileCE type not found");
                return;
            }

            // Patch TryCastShot for firing events
            var tryCastShotMethod = AccessTools.Method(verbType, "TryCastShot");
            if (tryCastShotMethod != null)
            {
                harmony.Patch(
                    original: tryCastShotMethod,
                    postfix: new HarmonyMethod(typeof(HarmonyPatches), nameof(Postfix_Verb_LaunchProjectileCE_TryCastShot))
                );
                //Log.Message("[Barrel Flash Debug] Successfully patched Verb_LaunchProjectileCE.TryCastShot");
            }
            else
            {
                //Log.Warning("[Barrel Flash Debug] Could not find Verb_LaunchProjectileCE.TryCastShot method");
            }

            // Patch warmup-related methods
            TryPatchCEVerbWarmup(harmony, verbType);
        }

        /// <summary>
        /// Patches CE verb warmup methods to provide warmup event notifications.
        /// </summary>
        private static void TryPatchCEVerbWarmup(Harmony harmony, System.Type verbType)
        {
            // Patch WarmupComplete for successful warmup finish
            var warmupCompleteMethod = AccessTools.Method(verbType, "WarmupComplete");
            if (warmupCompleteMethod != null)
            {
                harmony.Patch(
                    original: warmupCompleteMethod,
                    postfix: new HarmonyMethod(typeof(HarmonyPatches), nameof(Postfix_Verb_WarmupComplete))
                );
                //Log.Message("[Warmup Debug] Successfully patched Verb.WarmupComplete");
            }

            // Patch Reset method for warmup interruption
            var resetMethod = AccessTools.Method(verbType, "Reset");
            if (resetMethod != null)
            {
                harmony.Patch(
                    original: resetMethod,
                    postfix: new HarmonyMethod(typeof(HarmonyPatches), nameof(Postfix_Verb_Reset))
                );
                //Log.Message("[Warmup Debug] Successfully patched Verb.Reset");
            }

            // Try to patch TryStartCastOn for warmup start
            // This method initiates the warmup process
            // Note: TryStartCastOn has multiple overloads, so we need to be more specific
            var tryStartCastOnMethods = AccessTools.GetDeclaredMethods(verbType)
                .Where(m => m.Name == "TryStartCastOn")
                .ToArray();

            if (tryStartCastOnMethods.Length > 0)
            {
                // Try to find a method that returns bool (the main TryStartCastOn method)
                var tryStartCastOnMethod = tryStartCastOnMethods.FirstOrDefault(m => m.ReturnType == typeof(bool));
                if (tryStartCastOnMethod != null)
                {
                    harmony.Patch(
                        original: tryStartCastOnMethod,
                        postfix: new HarmonyMethod(typeof(HarmonyPatches), nameof(Postfix_Verb_TryStartCastOn))
                    );
                    //Log.Message("[Warmup Debug] Successfully patched Verb.TryStartCastOn");
                }
                else
                {
                    //Log.Warning("[Warmup Debug] Could not find suitable TryStartCastOn method to patch");
                }
            }
        }

        /// <summary>
        /// Called after a CE verb tries to cast a shot.
        /// Only triggers firing animation if the shot was successful and the caster is a turret.
        /// </summary>
        public static void Postfix_Verb_LaunchProjectileCE_TryCastShot(bool __result, object __instance)
        {
            try
            {
                // Only trigger if the shot was successful
                if (!__result)
                {
                    return;
                }

                // Get the caster (turret) from the verb
                Thing caster = null;

                // Try property first
                var casterProperty = AccessTools.Property(__instance.GetType(), "Caster");
                if (casterProperty != null)
                {
                    caster = casterProperty.GetValue(__instance) as Thing;
                }

                // Try field if property didn't work
                if (caster == null)
                {
                    var casterField = AccessTools.Field(__instance.GetType(), "caster");
                    if (casterField != null)
                    {
                        caster = casterField.GetValue(__instance) as Thing;
                    }
                }

                if (caster == null)
                {
                    return; // Couldn't find caster
                }

                // Check if it's a turret (CE or vanilla)
                bool isTurret = caster is Building_Turret;
                if (!isTurret)
                {
                    // Try to check if it's a CE turret by type name
                    var ceTurretType = AccessTools.TypeByName("CombatExtended.Building_TurretGunCE");
                    if (ceTurretType != null && ceTurretType.IsAssignableFrom(caster.GetType()))
                    {
                        isTurret = true;
                    }
                }

                if (!isTurret)
                {
                    return; // Not a turret, ignore
                }

                // Found a turret that successfully fired - trigger the animation
                var barrelComp = caster.TryGetComp<CompTurretBarrel>();
                if (barrelComp != null)
                {
                    barrelComp.TriggerFiring();
                }
            }
            catch (Exception ex)
            {
                // Catch any exceptions to prevent breaking the turret's firing logic
                Verse.Log.Error($"[Barrel Animation] Error in Postfix_Verb_LaunchProjectileCE_TryCastShot: {ex}");
            }
        }

        /// <summary>
        /// Called after a verb successfully completes warmup.
        /// Triggers warmup complete events for turrets.
        /// </summary>
        public static void Postfix_Verb_WarmupComplete(object __instance)
        {
            try
            {
                // Get the caster from the verb
                Thing caster = GetCasterFromVerb(__instance);
                if (caster == null)
                {
                    return;
                }

                // Check if it's a turret
                if (!IsTurret(caster))
                {
                    return; // Not a turret, ignore
                }

                // Notify the mod that warmup completed for this turret
                var barrelComp = caster.TryGetComp<CompTurretBarrel>();
                if (barrelComp != null)
                {
                    // Call a method to handle warmup completion
                    barrelComp.OnWarmupComplete();
                }

                //Log.Message($"[Warmup Debug] Warmup completed for turret {caster.def.defName}");
            }
            catch (Exception ex)
            {
                Verse.Log.Error($"[Barrel Animation] Error in Postfix_Verb_WarmupComplete: {ex}");
            }
        }

        /// <summary>
        /// Called after a verb is reset (warmup interrupted).
        /// Triggers warmup interrupt events for turrets.
        /// </summary>
        public static void Postfix_Verb_Reset(object __instance)
        {
            try
            {
                // Get the caster from the verb
                Thing caster = GetCasterFromVerb(__instance);
                if (caster == null)
                {
                    return;
                }

                // Check if it's a turret
                if (!IsTurret(caster))
                {
                    return; // Not a turret, ignore
                }

                // Notify the mod that warmup was interrupted for this turret
                var barrelComp = caster.TryGetComp<CompTurretBarrel>();
                if (barrelComp != null)
                {
                    // Call a method to handle warmup interruption
                    barrelComp.OnWarmupInterrupted();
                }

                //Log.Message($"[Warmup Debug] Warmup interrupted for turret {caster.def.defName}");
            }
            catch (Exception ex)
            {
                Verse.Log.Error($"[Barrel Animation] Error in Postfix_Verb_Reset: {ex}");
            }
        }

        /// <summary>
        /// Called after TryStartCastOn (warmup initiation attempt).
        /// Triggers warmup start events for turrets when warmup actually begins.
        /// </summary>
        public static void Postfix_Verb_TryStartCastOn(bool __result, object __instance)
        {
            try
            {
                // Only trigger if TryStartCastOn returned true (warmup successfully started)
                if (!__result)
                {
                    return;
                }

                // Get the caster from the verb
                Thing caster = GetCasterFromVerb(__instance);
                if (caster == null)
                {
                    return;
                }

                // Check if it's a turret
                if (!IsTurret(caster))
                {
                    return; // Not a turret, ignore
                }

                // Notify the mod that warmup started for this turret
                var barrelComp = caster.TryGetComp<CompTurretBarrel>();
                if (barrelComp != null)
                {
                    // Call a method to handle warmup start
                    barrelComp.OnWarmupStarted();
                }

                //Log.Message($"[Warmup Debug] Warmup started for turret {caster.def.defName}");
            }
            catch (Exception ex)
            {
                Verse.Log.Error($"[Barrel Animation] Error in Postfix_Verb_TryStartCastOn: {ex}");
            }
        }

        /// <summary>
        /// Helper method to get the caster from a verb instance.
        /// </summary>
        private static Thing GetCasterFromVerb(object verbInstance)
        {
            // Try property first
            var casterProperty = AccessTools.Property(verbInstance.GetType(), "Caster");
            if (casterProperty != null)
            {
                return casterProperty.GetValue(verbInstance) as Thing;
            }

            // Try field if property didn't work
            var casterField = AccessTools.Field(verbInstance.GetType(), "caster");
            if (casterField != null)
            {
                return casterField.GetValue(verbInstance) as Thing;
            }

            return null;
        }

        /// <summary>
        /// Helper method to check if a thing is a turret.
        /// </summary>
        private static bool IsTurret(Thing thing)
        {
            if (thing is Building_Turret)
            {
                return true;
            }

            // Check for CE turret types
            var ceTurretType = AccessTools.TypeByName("CombatExtended.Building_TurretGunCE");
            if (ceTurretType != null && ceTurretType.IsAssignableFrom(thing.GetType()))
            {
                return true;
            }

            return false;
        }


        /// <summary>
        /// Called after a turret completes a burst.
        /// Triggers burst complete events for spinning animation.
        /// </summary>
        public static void Postfix_BurstComplete(object __instance)
        {
            if (__instance is Building_Turret turret)
            {
                var barrelComp = turret.GetComp<CompTurretBarrel>();
                if (barrelComp != null)
                {
                    // Trigger burst complete for spinning animation
                    barrelComp.OnBurstComplete();
                }
            }
        }

        /// <summary>
        /// Called after CE turret building drawing.
        /// Draws the barrel after the turret top if drawOnTop is true.
        /// This is an alternative method that works by patching the turret building itself.
        /// </summary>
        public static void Postfix_TurretDrawAt(object __instance)
        {
            try
        {
            if (__instance is Building_Turret turret)
            {
                var barrelComp = turret.GetComp<CompTurretBarrel>();
                    if (barrelComp != null && barrelComp.Extension != null && barrelComp.Extension.drawOnTop)
                {
                        // Draw the barrel after the turret building (which includes turret top) has been drawn
                        // This ensures the barrel appears on top
                        barrelComp.DrawBarrelNow();
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Warning($"Turret Barrel Animation: Error in Postfix_TurretDrawAt: {ex.Message}");
            }
        }

        /// <summary>
        /// Called after CE turret top drawing.
        /// Draws the barrel component after the turret top only if drawOnTop is true.
        /// </summary>
        public static void Postfix_TurretTopDraw(object __instance)
        {
            try
            {
                Building_Turret turret = null;
                
                // Try to get the parent turret from the turret top
                // CE TurretTop typically has a reference to the parent turret
                var turretTopType = __instance.GetType();
                
                // Try field names
                var fieldNames = new[] { "parentTurret", "ParentTurret", "turret", "Turret", "parent", "Parent" };
                foreach (var fieldName in fieldNames)
                {
                    var field = AccessTools.Field(turretTopType, fieldName);
                    if (field != null)
                    {
                        var value = field.GetValue(__instance);
                        if (value is Building_Turret t)
                        {
                            turret = t;
                            break;
                        }
                    }
                }
                
                // Try property names if field didn't work
                if (turret == null)
                {
                    var propertyNames = new[] { "parentTurret", "ParentTurret", "turret", "Turret", "parent", "Parent" };
                    foreach (var propName in propertyNames)
                    {
                        var prop = AccessTools.Property(turretTopType, propName);
                        if (prop != null)
                        {
                            var value = prop.GetValue(__instance);
                            if (value is Building_Turret t)
                            {
                                turret = t;
                                break;
                            }
                        }
                    }
                }

                if (turret != null)
                {
                    // Debug: Log turret top altitude
                    string turretTopAltitudeKey = $"TURRETTOP_ALTITUDE_{turret.def.defName}";
                    if (!AbsolutelyMoreCannons.CompTurretBarrel.loggedTypes.Contains(turretTopAltitudeKey))
                    {
                        AbsolutelyMoreCannons.CompTurretBarrel.loggedTypes.Add(turretTopAltitudeKey);
                        // Try to get turret top draw position
                        var turretTopDrawPos = turret.DrawPos;
                        Verse.Log.Message($"[Altitude Debug] Turret Top (via parent turret) Y position: {turretTopDrawPos.y:F3}");
                    }
                    
                    var barrelComp = turret.GetComp<CompTurretBarrel>();
                    if (barrelComp != null && barrelComp.Extension != null)
                    {
                        // Only draw if drawOnTop is enabled
                        if (barrelComp.Extension.drawOnTop)
                        {
                            // Draw the barrel after the turret top
                            barrelComp.DrawBarrelNow();
                        }
                    }
                }
                else
                {
                    // Debug: Log that we couldn't find the turret
                    Log.Warning($"Turret Barrel Animation: Could not find parent turret from turret top. TurretTop type: {turretTopType.Name}");
                }
            }
            catch (Exception ex)
            {
                // Log the error for debugging
                Log.Warning($"Turret Barrel Animation: Error drawing barrel after turret top: {ex.Message}\n{ex.StackTrace}");
            }
        }
        
        /// <summary>
        /// Postfix for Verb.TryCastNextBurstShot to override tick delay based on RPM.
        /// </summary>
        public static void Postfix_Verb_TryCastNextBurstShot(Verb __instance)
        {
             try
             {
                 Thing caster = __instance.Caster;
                 if (caster == null) return;

                 // Check for our component
                 var barrelComp = caster.TryGetComp<CompTurretBarrel>();
                 if (barrelComp != null)
                 {
                     float ticksFloat = barrelComp.GetCurrentTicksBetweenBurstShots();
                     if (ticksFloat > 0f)
                     {
                         int ticks = Mathf.RoundToInt(ticksFloat);
                         // Use traverse or reflection to set the field since it might be protected/private or we just want to be safe
                         // ticksToNextBurstShot is protected in Verb
                         AccessTools.Field(typeof(Verb), "ticksToNextBurstShot").SetValue(__instance, ticks);
                     }
                 }
             }
             catch (Exception ex)
             {
                // Only log once per session to avoid spam
                if (!CompTurretBarrel.loggedTypes.Contains("TryCastNextBurstShot_Error"))
                {
                    CompTurretBarrel.loggedTypes.Add("TryCastNextBurstShot_Error");
                    Log.Warning($"Turret Barrel Animation: Error in Postfix_Verb_TryCastNextBurstShot: {ex.Message}");
                }
             }
        }

        /// <summary>
        /// Postfix for Verb_ShootCE.ShotsPerBurstFor to override burst count.
        /// </summary>
        public static void Postfix_Verb_ShotsPerBurstFor(ref int __result, object __instance)
        {
             try
             {
                 Thing caster = GetCasterFromVerb(__instance);
                 if (caster == null) return;

                 var barrelComp = caster.TryGetComp<CompTurretBarrel>();
                 if (barrelComp != null)
                 {
                     int burstOverride = barrelComp.GetCurrentBurstCount();
                     if (burstOverride > 0)
                     {
                         __result = burstOverride;
                     }
                 }
             }
             catch (Exception ex)
             {
                  if (!CompTurretBarrel.loggedTypes.Contains("ShotsPerBurstFor_Error"))
                  {
                      CompTurretBarrel.loggedTypes.Add("ShotsPerBurstFor_Error");
                      Log.Warning($"Turret Barrel Animation: Error in Postfix_Verb_ShotsPerBurstFor: {ex.Message}");
                  }
             }
        }
    }
}
