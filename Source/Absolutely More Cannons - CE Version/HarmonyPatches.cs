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
            
            // Try to patch dual fire mode system
            TryPatchDualFireMode(harmony);
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

            // Patch IncrementBarrelCount to add our custom lateral offset
            var incrementBarrelMethod = AccessTools.Method(verbType, "IncrementBarrelCount");
            if (incrementBarrelMethod != null)
            {
                harmony.Patch(
                    original: incrementBarrelMethod,
                    postfix: new HarmonyMethod(typeof(HarmonyPatches), nameof(Postfix_Verb_LaunchProjectileCE_IncrementBarrelCount))
                );
                Log.Message("[Projectile Offset] Successfully patched Verb_LaunchProjectileCE.IncrementBarrelCount");
            }
            else
            {
                Log.Warning("[Projectile Offset] Could not find Verb_LaunchProjectileCE.IncrementBarrelCount method");
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

        /// <summary>
        /// Postfix patch for Verb_LaunchProjectileCE.IncrementBarrelCount to add custom barrel offset.
        /// Modifies the return value to include our lateral spacing for multi-barrel turrets.
        /// </summary>
        public static void Postfix_Verb_LaunchProjectileCE_IncrementBarrelCount(object __instance, ref Vector2 __result)
        {
            try
            {
                // Get the caster
                var casterField = AccessTools.Field(__instance.GetType().BaseType, "caster");
                if (casterField == null) return;
                
                var caster = casterField.GetValue(__instance) as Thing;
                if (caster == null) return;

                // Try to get CompTurretBarrel
                var barrelComp = caster.TryGetComp<CompTurretBarrel>();
                if (barrelComp == null || barrelComp.Extension == null) return;

                // Get offset parameters
                int barrelAmount = barrelComp.Extension.barrelAmount;
                float barrelSpacing = barrelComp.Extension.barrelSpacing;
                bool sequentialFiring = barrelComp.Extension.sequentialFiring;

                // Only apply lateral offset for multi-barrel turrets
                if (barrelAmount <= 1 || barrelSpacing == 0f || !sequentialFiring)
                    return;

                // Get the shotRotation field (float in degrees)
                var shotRotationField = AccessTools.Field(__instance.GetType(), "shotRotation");
                if (shotRotationField == null) return;

                float shotRotation = (float)shotRotationField.GetValue(__instance);

                // Get multiBarrelIndex (already incremented by the original method)
                var multiBarrelIndexField = AccessTools.Field(__instance.GetType(), "multiBarrelIndex");
                if (multiBarrelIndexField == null) return;

                int rawIndex = (int)multiBarrelIndexField.GetValue(__instance);
                int currentBarrelIndex = rawIndex % barrelAmount;

                // Calculate perpendicular direction (90 degrees clockwise from forward)
                float rotationRad = shotRotation * Mathf.Deg2Rad;
                Vector2 rightDir = new Vector2(Mathf.Cos(rotationRad), -Mathf.Sin(rotationRad));

                // Calculate lateral offset based on barrel index
                // Formula: (barrelIndex - (N-1)/2) * spacing
                float lateralOffset = (currentBarrelIndex - (barrelAmount - 1) / 2f) * barrelSpacing;
                
                // Add our lateral offset to CE's result
                Vector2 ourOffset = rightDir * lateralOffset;
                __result += ourOffset;
                
                Log.Message($"[Projectile Offset DEBUG] IncrementBarrelCount - " +
                    $"barrelIndex: {currentBarrelIndex}/{barrelAmount}, " +
                    $"lateralOffset: {lateralOffset:F2}, " +
                    $"CE_result: ({__result.x - ourOffset.x:F2}, {__result.y - ourOffset.y:F2}), " +
                    $"ourOffset: ({ourOffset.x:F2}, {ourOffset.y:F2}), " +
                    $"final: ({__result.x:F2}, {__result.y:F2})");
            }
            catch (Exception ex)
            {
                Log.Warning($"[Barrel Animation] Error in IncrementBarrelCount offset patch: {ex.Message}");
            }
        }

        // ============================================================================
        // DUAL FIRE MODE PATCHES
        // ============================================================================

        /// <summary>
        /// Attempts to patch dual fire mode system for Combat Extended turrets.
        /// </summary>
        private static void TryPatchDualFireMode(Harmony harmony)
        {
            try
            {
                var ceTurretType = AccessTools.TypeByName("CombatExtended.Building_TurretGunCE");
                if (ceTurretType == null)
                {
                    Log.Message("[DualFireMode] CombatExtended not found, skipping dual fire mode patches.");
                    return;
                }

                Log.Message("[DualFireMode] Found CombatExtended, applying dual fire mode patches...");

                // 1. Patch AttackVerb getter to return mode-specific verb
                var attackVerbProperty = AccessTools.Property(ceTurretType, "AttackVerb");
                if (attackVerbProperty != null)
                {
                    var getterMethod = attackVerbProperty.GetGetMethod();
                    if (getterMethod != null)
                    {
                        harmony.Patch(
                            original: getterMethod,
                            prefix: new HarmonyMethod(typeof(HarmonyPatches), nameof(Prefix_TurretGunCE_AttackVerb))
                        );
                        Log.Message("[DualFireMode] Successfully patched Building_TurretGunCE.AttackVerb getter");
                    }
                }

                // 2. Patch turret gun SpawnSetup to initialize dual verbs
                var spawnSetupMethod = AccessTools.Method(ceTurretType, "SpawnSetup");
                if (spawnSetupMethod != null)
                {
                    harmony.Patch(
                        original: spawnSetupMethod,
                        postfix: new HarmonyMethod(typeof(HarmonyPatches), nameof(Postfix_TurretGunCE_SpawnSetup))
                    );
                    Log.Message("[DualFireMode] Successfully patched Building_TurretGunCE.SpawnSetup");
                }

                // 3. Patch CompEquippable.PrimaryVerb to return mode-specific verb
                var primaryVerbProp = AccessTools.Property(typeof(CompEquippable), "PrimaryVerb");
                if (primaryVerbProp != null)
                {
                    var getterMethod = primaryVerbProp.GetGetMethod();
                    if (getterMethod != null)
                    {
                        harmony.Patch(
                            original: getterMethod,
                            postfix: new HarmonyMethod(typeof(HarmonyPatches), nameof(Postfix_CompEquippable_PrimaryVerb))
                        );
                        Log.Message("[DualFireMode] Successfully patched CompEquippable.PrimaryVerb getter");
                    }
                }

                // 4. Patch CompAmmoUser.CurrentAmmoSet to return mode-specific ammo
                var ammoUserType = AccessTools.TypeByName("CombatExtended.CompAmmoUser");
                if (ammoUserType != null)
                {
                    var currentAmmoSetProperty = AccessTools.Property(ammoUserType, "CurrentAmmoSet");
                    if (currentAmmoSetProperty != null)
                    {
                        var getterMethod = currentAmmoSetProperty.GetGetMethod();
                        if (getterMethod != null)
                        {
                            harmony.Patch(
                                original: getterMethod,
                                postfix: new HarmonyMethod(typeof(HarmonyPatches), nameof(Postfix_CompAmmoUser_CurrentAmmoSet))
                            );
                            Log.Message("[DualFireMode] Successfully patched CompAmmoUser.CurrentAmmoSet getter");
                        }
                    }
                }

                Log.Message("[DualFireMode] Dual fire mode patch initialization complete");
            }
            catch (Exception ex)
            {
                Log.Error($"[DualFireMode] Error patching dual fire mode system: {ex.Message}\n{ex.StackTrace}");
            }
        }

        // DUAL FIRE MODE PATCHES
        // ============================================================================

        /// <summary>
        /// Prefix for Building_TurretGunCE.AttackVerb getter.
        /// Returns the active mode's verb instead of the gun's default verb.
        /// </summary>
        public static bool Prefix_TurretGunCE_AttackVerb(Building_TurretGun __instance, ref Verb __result)
        {
            var dualModeComp = __instance.TryGetComp<CompDualFireMode>();
            if (dualModeComp == null)
                return true; // Not a dual-mode turret, use original logic

            // Return the active mode's verb
            var activeVerb = dualModeComp.ActiveVerb;
            if (activeVerb != null)
            {
                __result = activeVerb;
                
                // Log detailed info for debugging
                var mode = dualModeComp.CurrentMode;
                var verbType = activeVerb.GetType().Name;
                var minRange = activeVerb.verbProps?.minRange ?? -1;
                var requireLOS = activeVerb.verbProps?.requireLineOfSight ?? true;
                
                Log.Message($"[DualFireMode] AttackVerb called for {__instance.def.defName}: returning {verbType} (mode={mode}, minRange={minRange}, requireLOS={requireLOS})");
                
                return false; // Skip original method
            }

            return true; // Fallback to original if something went wrong
        }

        /// <summary>
        /// Postfix for Building_TurretGunCE.SpawnSetup.
        /// Initializes dual-mode verbs after turret spawns.
        /// </summary>
        public static void Postfix_TurretGunCE_SpawnSetup(Building_TurretGun __instance, bool respawningAfterLoad)
        {
            if (respawningAfterLoad)
                return;

            var dualModeComp = __instance.TryGetComp<CompDualFireMode>();
            if (dualModeComp == null)
                return;

            // Initialize verbs (will be called from comp's PostSpawnSetup, but retry here for safety)
            dualModeComp.InitializeVerbs();
        }

        /// <summary>
        /// Postfix for CompEquippable.PrimaryVerb getter.
        /// Returns the active mode's verb for dual-mode turret guns.
        /// This ensures UI and targeting use the correct verb.
        /// </summary>
        public static void Postfix_CompEquippable_PrimaryVerb(CompEquippable __instance, ref Verb __result)
        {
            // Get the gun
            var gun = __instance.parent;
            if (gun == null) return;
            
            // Get the turret that owns this gun
            var turret = gun.ParentHolder as Building_TurretGun;
            if (turret == null) return;
            
            var dualModeComp = turret.TryGetComp<CompDualFireMode>();
            if (dualModeComp == null) return;
            
            // Return the active mode's verb
            var activeVerb = dualModeComp.ActiveVerb;
            if (activeVerb != null)
            {
                __result = activeVerb;
            }
        }

        /// <summary>
        /// Postfix for CompAmmoUser.CurrentAmmoSet getter.
        /// Returns mode-specific ammo set for dual-mode turrets.
        /// </summary>
       public static void Postfix_CompAmmoUser_CurrentAmmoSet(object __instance, ref object __result)
        {
            // Get the weapon's parent (should be turret gun)
            var compType = __instance.GetType();
            var parentField = compType.GetProperty("parent");
            if (parentField == null)
                return;
                
            var weapon = parentField.GetValue(__instance) as ThingWithComps;
            if (weapon == null)
                return;

            // Find the turret building that owns this gun
            var turret = weapon.ParentHolder as Building_TurretGun;
            if (turret == null)
                return;

            var dualModeComp = turret.TryGetComp<CompDualFireMode>();
            if (dualModeComp == null)
                return; // Not a dual-mode turret

            // Get mode-specific ammo set name
            var ammoSetName = dualModeComp.GetCurrentAmmoSet();
            if (string.IsNullOrEmpty(ammoSetName))
                return;

            // Look up the AmmoSetDef via reflection
            var ammoSetDefType = AccessTools.TypeByName("CombatExtended.AmmoSetDef");
            if (ammoSetDefType != null)
            {
                var defDatabase = typeof(DefDatabase<>).MakeGenericType(ammoSetDefType);
                var getNamedMethod = defDatabase.GetMethod("GetNamedSilentFail");
                if (getNamedMethod != null)
                {
                    var ammoSetDef = getNamedMethod.Invoke(null, new object[] { ammoSetName });
                    if (ammoSetDef != null)
                    {
                        __result = ammoSetDef;
                    }
                }
            }
        }

        // DUAL FIRE MODE PATCHES
        // ============================================================================

        /// <summary>
        /// Postfix for Verb_LaunchProjectileCE.RecoilAmount property getter.
        /// Catches null reference errors and returns 0 instead.
        /// </summary>
        public static Exception Finalizer_Verb_LaunchProjectileCE_RecoilAmount(Exception __exception, ref float __result)
        {
            if (__exception != null)
            {
                // An exception occurred (likely null EquipmentSource)
                __result = 0f;
                
                // Log once
                if (!CompTurretBarrel.loggedTypes.Contains("DualMode_RecoilAmountError"))
                {
                    CompTurretBarrel.loggedTypes.Add("DualMode_RecoilAmountError");
                    Log.Message($"[DualFireMode] Caught exception in RecoilAmount getter, returning 0: {__exception.GetType().Name}");
                }
                
                // Suppress the exception
                return null;
            }
            
            return null;
        }

        /// <summary>
        /// Finalizer for Verb_LaunchProjectileCE.CanHitTarget.
        /// Catches null reference errors in tooltip calculations.
        /// </summary>
        public static Exception Finalizer_Verb_CanHitTarget(Exception __exception, ref bool __result)
        {
            if (__exception != null)
            {
                // An exception occurred - return false (can't hit target)
                __result = false;
                
                // Log once
                if (!CompTurretBarrel.loggedTypes.Contains("DualMode_CanHitTargetError"))
                {
                    CompTurretBarrel.loggedTypes.Add("DualMode_CanHitTargetError");
                    Log.Message($"[DualFireMode] Caught exception in CanHitTarget, returning false: {__exception.GetType().Name}");
                }
                
                // Suppress the exception
                return null;
            }
            
            return null;
        }

        /// <summary>
        /// Finalizer for Verb.Available.
        /// Catches null reference errors when checking if verb is available.
        /// </summary>
        public static Exception Finalizer_Verb_Available(Exception __exception, ref bool __result)
        {
            if (__exception != null)
            {
                // An exception occurred - return false (verb not available)
                __result = false;
                
                // Log once
                if (!CompTurretBarrel.loggedTypes.Contains("DualMode_VerbAvailableError"))
                {
                    CompTurretBarrel.loggedTypes.Add("DualMode_VerbAvailableError");
                    Log.Message($"[DualFireMode] Caught exception in Verb.Available, returning false: {__exception.GetType().Name}");
                }
                
                // Suppress the exception
                return null;
            }
            
            return null;
        }

        /// <summary>
        /// Finalizer for Verb_LaunchProjectileCE.WarmupTime property getter.
        /// Catches null reference errors and returns default warmup time.
        /// </summary>
        public static Exception Finalizer_Verb_WarmupTime(Exception __exception, ref float __result)
        {
            if (__exception != null)
            {
                // An exception occurred - return default warmup time (1 second)
                __result = 1f;
                
                // Log once
                if (!CompTurretBarrel.loggedTypes.Contains("DualMode_WarmupTimeError"))
                {
                    CompTurretBarrel.loggedTypes.Add("DualMode_WarmupTimeError");
                    Log.Message($"[DualFireMode] Caught exception in WarmupTime getter, returning 1.0f: {__exception.GetType().Name}");
                }
                
                // Suppress the exception
                return null;
            }
            
            return null;
        }

        /// <summary>
        /// Prefix for CE_Utility.Recoil to handle our custom dual-mode verbs.
        /// Prevents null reference errors when our verbs don't have complete initialization.
        /// </summary>
        public static bool Prefix_CE_Utility_Recoil(Verb shootVerb, ref Vector3 drawOffset, ref float angleOffset)
        {
            try
            {
                // Simple check: if the verb's EquipmentSource is null, skip recoil
                // This handles our custom verbs and any other improperly initialized verbs
                if (shootVerb != null)
                {
                    var equipmentSourceField = typeof(Verb).GetField("equipmentSource", BindingFlags.Instance | BindingFlags.NonPublic);
                    if (equipmentSourceField != null)
                    {
                        var equipSource = equipmentSourceField.GetValue(shootVerb);
                        if (equipSource == null)
                        {
                            // EquipmentSource is null - skip recoil calculation to prevent crash
                            drawOffset = Vector3.zero;
                            angleOffset = 0f;
                            
                            // Log once per session
                            if (!CompTurretBarrel.loggedTypes.Contains("DualMode_NullEquipSourceRecoil"))
                            {
                                CompTurretBarrel.loggedTypes.Add("DualMode_NullEquipSourceRecoil");
                                Log.Message($"[DualFireMode] Skipping recoil for verb with null EquipmentSource: {shootVerb.GetType().Name}");
                            }
                            
                            return false; // Skip original method
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[DualFireMode] Error in Prefix_CE_Utility_Recoil: {ex.Message}");
                // On error, skip recoil to be safe
                drawOffset = Vector3.zero;
                angleOffset = 0f;
                return false;
            }

            return true; // Run original method
        }

        /// <summary>
        /// Postfix for Building_TurretGunCE.AttackVerb getter.
        /// Swaps the verb based on current fire mode.
        /// </summary>
        public static void Postfix_TurretGunCE_AttackVerb(ref Verb __result, ThingWithComps __instance)
        {
            try
            {
                var dualModeComp = __instance.TryGetComp<CompDualFireMode>();
                if (dualModeComp == null)
                    return; // Not a dual-mode turret

                // Get the appropriate verb for current mode
                Verb targetVerb = dualModeComp.CurrentMode == FireMode.Direct
                    ? dualModeComp.DirectVerb
                    : dualModeComp.IndirectVerb;

                // Only replace if we have a valid verb
                // If verbs aren't initialized yet, let the original verb be used (prevents drawing errors)
                if (targetVerb != null)
                {
                    __result = targetVerb;
                }
                else
                {
                    // Verbs not ready yet - log once and use default
                    if (!CompTurretBarrel.loggedTypes.Contains("DualMode_VerbNotReady"))
                    {
                        CompTurretBarrel.loggedTypes.Add("DualMode_VerbNotReady");
                        Log.Warning($"[DualFireMode] Verbs not initialized yet for {__instance.def.defName}, using default verb temporarily");
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[DualFireMode] Error in Postfix_TurretGunCE_AttackVerb: {ex.Message}");
            }
        }

        /// <summary>
        /// Postfix for CompAmmoUser.CurrentAmmoSet getter.
        /// Returns mode-specific ammo set for dual-mode turrets.
        /// </summary>
        public static void Postfix_CompAmmoUser_CurrentAmmoSet(ref object __result, ThingComp __instance)
        {
            try
            {
                // Get the parent thing (gun)
                var gun = __instance.parent;
                if (gun == null)
                    return;

                // Try to find the turret building that owns this gun
                ThingWithComps turret = null;
                
                // Method 1: Check if parent has a holder (equipment/inventory)
                var equipmentSourceField = gun.GetType().GetField("equipmentSource", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (equipmentSourceField != null)
                {
                    var equipmentSource = equipmentSourceField.GetValue(gun);
                    if (equipmentSource != null)
                    {
                        var parentHolderField = equipmentSource.GetType().GetField("pawn", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                        if (parentHolderField != null)
                        {
                            turret = parentHolderField.GetValue(equipmentSource) as ThingWithComps;
                        }
                    }
                }

                // Method 2: If gun is on map, try to find nearby turret
                if (turret == null && gun.Spawned)
                {
                    var ceTurretType = AccessTools.TypeByName("CombatExtended.Building_TurretGunCE");
                    if (ceTurretType != null)
                    {
                        // Search for turret at same position
                        var thingsAtPos = gun.Map.thingGrid.ThingsListAtFast(gun.Position);
                        foreach (var thing in thingsAtPos)
                        {
                            if (ceTurretType.IsAssignableFrom(thing.GetType()))
                            {
                                turret = thing as ThingWithComps;
                                break;
                            }
                        }
                    }
                }

                if (turret == null)
                    return; // Couldn't find turret

                var dualModeComp = turret.TryGetComp<CompDualFireMode>();
                if (dualModeComp == null)
                    return; // Not a dual-mode turret

                // Get the ammo set name for current mode
                var ammoSetName = dualModeComp.GetCurrentAmmoSet();
                if (string.IsNullOrEmpty(ammoSetName))
                    return;

                // Try to get AmmoSetDef from database
                var ammoSetDefType = AccessTools.TypeByName("CombatExtended.AmmoSetDef");
                if (ammoSetDefType != null)
                {
                    // Access DefDatabase<AmmoSetDef>
                    var defDatabaseType = typeof(DefDatabase<>).MakeGenericType(ammoSetDefType);
                    var getNamedMethod = defDatabaseType.GetMethod("GetNamed", new[] { typeof(string), typeof(bool) });
                    
                    if (getNamedMethod != null)
                    {
                        try
                        {
                            // Invoke GetNamed(string defName, bool errorOnFail)
                            var ammoSetDef = getNamedMethod.Invoke(null, new object[] { ammoSetName, false });
                            if (ammoSetDef != null)
                            {
                                // Set the result via reflection
                                __result = ammoSetDef;
                            }
                        }
                        catch (Exception ex)
                        {
                            Log.Warning($"[DualFireMode] Failed to get AmmoSetDef for {ammoSetName}: {ex.Message}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // Silently fail to avoid spam - ammo system will use default
                if (!CompTurretBarrel.loggedTypes.Contains("AmmoSet_Error"))
                {
                    CompTurretBarrel.loggedTypes.Add("AmmoSet_Error");
                    Log.Warning($"[DualFireMode] Error in Postfix_CompAmmoUser_CurrentAmmoSet: {ex.Message}");
                }
            }
        }
    }
}
