using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using RimWorld;
using UnityEngine;
using Verse;

namespace AbsolutelyMoreCannons
{
    /// <summary>
    /// Component for animating turret barrels. Handles recoil, rotation, and firing animations.
    /// </summary>
    [StaticConstructorOnStartup]
    public class CompTurretBarrel : ThingComp
    {
        // Static cache to track which types we've already logged debug info for
        private static HashSet<string> loggedTypes = new HashSet<string>();
        
        // Static flash material for muzzle flash
        private static Material flashMaterial;
        
        static CompTurretBarrel()
        {
            // Create a bright material with additive/glow shader for muzzle flash
            // MoteGlow shader provides bright, additive rendering perfect for flashes
            // Use the same texture as RimWorld's ShotFlash fleck for a circular gradient effect
            Shader glowShader = ShaderDatabase.MoteGlow;
            Texture2D flashTexture = ContentFinder<Texture2D>.Get("Things/Mote/ShotFlash", true);
            flashMaterial = MaterialPool.MatFrom(flashTexture, glowShader, Color.white);
            // Note: Color will be set per-frame, so we use white as base
        }
        
        // Debug: Track rotation changes
        private float lastLoggedRotation = -999f;
        private int ticksSinceLastRotationLog = 0;
        private CompProperties_TurretBarrel Props { get { return (CompProperties_TurretBarrel)props; } }

        private ThingWithComps turret;
        private TurretBarrelExtension extension;

        // Animation state
        private int recoilTicksRemaining = 0;
        private float currentRecoilDistance = 0f;
        private float currentRecoilAngle = 0f;

        private float currentSpinFrame = 0f;
        private float currentSpinSpeed = 0f;

        private int firingTicksRemaining = 0;

        // Multi-barrel support: per-barrel firing animation state
        private int[] barrelFiringTicksRemaining = new int[0]; // Tracks firing animation for each barrel
        private int currentSequentialBarrel = 0; // Which barrel fires next in sequential mode

        // Multi-barrel support: per-barrel recoil animation state
        private int[] barrelRecoilTicksRemaining = new int[0]; // Tracks recoil animation for each barrel
        private float[] barrelRecoilDistance = new float[0]; // Current recoil distance for each barrel

        private Graphic barrelGraphic;
        private Material barrelMaterial;

        /// <summary>
        /// Gets the turret this component is attached to.
        /// Works with both vanilla and CE turrets.
        /// </summary>
        public ThingWithComps Turret
        {
            get
            {
                turret ??= parent as ThingWithComps;
                return turret;
            }
        }

        /// <summary>
        /// Checks if CombatExtended is loaded and this is a CE turret.
        /// </summary>
        public bool IsCETurret 
        { 
            get 
            { 
                if (turret == null) return false;
                var typeName = turret.GetType().Name;
                return typeName.Contains("CE") || typeName.Contains("CombatExtended");
            } 
        }

        /// <summary>
        /// Gets CE turret top if available, otherwise null.
        /// </summary>
        public object CETurretTop
        {
            get
            {
                if (turret == null) return null;
                
                // For Combat Extended, the turret top is stored as a FIELD, not a property
                // Try fields first (most common for CE turrets)
                var fieldNames = new[] { "top", "Top", "turretTop", "TurretTop", "gunTop", "GunTop" };
                var bindingFlags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;
                
                foreach (var fieldName in fieldNames)
                {
                    var topField = turret.GetType().GetField(fieldName, bindingFlags);
                    if (topField != null)
                    {
                        try
                        {
                            var value = topField.GetValue(turret);
                            if (value != null)
                            {
                                string debugKey = $"TURRETTOP_FIELD_{fieldName}_{turret.GetType().Name}";
                                if (!loggedTypes.Contains(debugKey))
                                {
                                    loggedTypes.Add(debugKey);
                                    Log.Message($"[Barrel Debug] Found turret top via field '{fieldName}': {value.GetType().Name}");
                                }
                                return value;
                            }
                        }
                        catch (Exception ex)
                        {
                            string errorKey = $"TURRETTOP_ERROR_{fieldName}";
                            if (!loggedTypes.Contains(errorKey))
                            {
                                loggedTypes.Add(errorKey);
                                Log.Warning($"[Barrel Debug] Error reading field '{fieldName}': {ex.Message}");
                            }
                        }
                    }
                }
                
                // Fallback: Try properties (for vanilla or other mods)
                var propertyNames = new[] { "top", "Top", "turretTop", "TurretTop", "gunTop", "GunTop" };
                foreach (var propName in propertyNames)
                {
                    var topProperty = turret.GetType().GetProperty(propName);
                    if (topProperty != null)
                    {
                        try
                        {
                            var value = topProperty.GetValue(turret);
                            if (value != null)
                            {
                                string debugKey = $"TURRETTOP_PROP_{propName}_{turret.GetType().Name}";
                                if (!loggedTypes.Contains(debugKey))
                                {
                                    loggedTypes.Add(debugKey);
                                    Log.Message($"[Barrel Debug] Found turret top via property '{propName}': {value.GetType().Name}");
                                }
                                return value;
                            }
                        }
                        catch { }
                    }
                }
                
                // Debug: Log all available fields if we couldn't find it
                string debugKey2 = $"TURRETTOP_SEARCH_{turret.GetType().Name}";
                if (!loggedTypes.Contains(debugKey2))
                {
                    loggedTypes.Add(debugKey2);
                    var allFields = turret.GetType().GetFields(bindingFlags)
                        .Select(f => $"{f.Name} ({f.FieldType.Name})").ToList();
                    Log.Warning($"[Barrel Debug] Could not find turret top field for {turret.GetType().Name}.\n" +
                        $"Available fields ({allFields.Count}): {string.Join(", ", allFields.Take(30))}");
                }
                
                return null;
            }
        }

        /// <summary>
        /// Gets the barrel extension configuration.
        /// </summary>
        public TurretBarrelExtension Extension
        {
            get
            {
                extension ??= Props.GetEffectiveExtension(parent.def);
                return extension;
            }
        }

        /// <summary>
        /// Initialize the component.
        /// Note: Graphics initialization is deferred to PostSpawnSetup or first draw
        /// to avoid loading graphics off the main thread during save loading.
        /// </summary>
        public override void Initialize(CompProperties props)
        {
            base.Initialize(props);
            // Don't initialize graphics here - Initialize() can be called during save loading off the main thread
        }

        /// <summary>
        /// Initialize graphics. Called lazily when first needed for drawing.
        /// Only initializes when on the main thread (during drawing).
        /// This prevents "Attempted to load a graphic off the main thread" errors.
        /// </summary>
        private void InitializeGraphics()
        {
            // Skip if already initialized
            if (barrelGraphic != null)
            {
                return;
            }
            
            // Safety checks - ensure parent is spawned and in a map
            // Drawing methods (PostDraw, DrawBarrelNow) are guaranteed to run on main thread
            if (parent == null || !parent.Spawned || parent.Map == null || Find.CurrentMap == null)
            {
                return; // Not safe to load graphics yet
            }

            // Initialize graphics
            if (Extension.barrelGraphic != null)
            {
                try
            {
                barrelGraphic = Extension.barrelGraphic.Graphic;
                if (barrelGraphic != null)
                {
                    barrelMaterial = barrelGraphic.MatSingle;
                }
                }
                catch (Exception ex)
                {
                    Log.Warning($"[Barrel Debug] Error initializing graphics for {parent?.def?.defName ?? "unknown"}: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Called after the parent thing is spawned or loaded.
        /// Note: Graphics initialization is deferred to drawing methods to avoid
        /// loading graphics off the main thread during save loading.
        /// </summary>
        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);
            // Don't initialize graphics here - PostSpawnSetup can run during
            // Map.FinalizeLoading() on a background thread. Graphics will be
            // initialized lazily when first drawn (guaranteed to be on main thread).
            
            // Initialize per-barrel firing state array
            int barrelCount = Mathf.Max(1, Extension.barrelAmount);
            if (barrelFiringTicksRemaining == null || barrelFiringTicksRemaining.Length != barrelCount)
            {
                barrelFiringTicksRemaining = new int[barrelCount];
            }
            
            // Initialize per-barrel recoil state array
            if (barrelRecoilTicksRemaining == null || barrelRecoilTicksRemaining.Length != barrelCount)
            {
                barrelRecoilTicksRemaining = new int[barrelCount];
                barrelRecoilDistance = new float[barrelCount];
            }
        }

        /// <summary>
        /// Component tick - updates animation state.
        /// </summary>
        public override void CompTick()
        {
            base.CompTick();

            UpdateRecoil();
            UpdateSpinning();
            UpdateFiringAnimation();
        }

        /// <summary>
        /// Draw the barrel component.
        /// If drawOnTop is false, draws below turret top with negative offset.
        /// If drawOnTop is true, drawing is handled by Harmony patch (on top of turret top).
        /// </summary>
        public override void PostDraw()
        {
            base.PostDraw();

            // Ensure graphics are initialized (safety check for save/load issues)
            if (barrelGraphic == null)
            {
                InitializeGraphics();
            }

            if (barrelGraphic == null || !ShouldDraw())
                return;

            // Only draw here if drawOnTop is false (draw below turret top)
            // If drawOnTop is true, the Harmony patch will handle drawing on top
            if (!Extension.drawOnTop)
            {
                DrawBarrel();
            }
        }

        /// <summary>
        /// Public method to draw the barrel. Called by Harmony patch when drawOnTop is true.
        /// </summary>
        public void DrawBarrelNow()
        {
            // Ensure graphics are initialized (safety check for save/load issues)
            if (barrelGraphic == null)
            {
                InitializeGraphics();
            }

            if (barrelGraphic == null || !ShouldDraw())
                return;

            // DrawBarrel() already includes flash drawing, so just call it
            DrawBarrel();
        }

        /// <summary>
        /// Trigger recoil animation.
        /// For sequential firing, triggers recoil for the barrel that just fired.
        /// For simultaneous firing, triggers recoil for all barrels.
        /// If recoil is already in progress, handles rapid-fire scenarios.
        /// </summary>
        public void TriggerRecoil()
        {
            if (Extension.recoilAnimation == null)
                return;

            int barrelCount = Mathf.Max(1, Extension.barrelAmount);
            
            // Ensure per-barrel recoil arrays are initialized
            if (barrelRecoilTicksRemaining == null || barrelRecoilTicksRemaining.Length != barrelCount)
            {
                barrelRecoilTicksRemaining = new int[barrelCount];
                barrelRecoilDistance = new float[barrelCount];
            }

            if (Extension.sequentialFiring && barrelCount > 1)
            {
                // Sequential firing: trigger recoil for the barrel that just fired
                // The barrel that fired is the one BEFORE currentSequentialBarrel (since it increments after firing)
                int barrelThatFired = (currentSequentialBarrel - 1 + barrelCount) % barrelCount;
                TriggerRecoilForBarrel(barrelThatFired);
            }
            else
            {
                // Simultaneous firing: trigger recoil for all barrels
                for (int i = 0; i < barrelCount; i++)
                {
                    TriggerRecoilForBarrel(i);
                }
                
                // Legacy support: also update global recoil for backwards compatibility
                TriggerRecoilLegacy();
            }
        }

        /// <summary>
        /// Trigger recoil animation for a specific barrel.
        /// </summary>
        private void TriggerRecoilForBarrel(int barrelIndex)
        {
            if (Extension.recoilAnimation == null)
                return;

            int barrelCount = Mathf.Max(1, Extension.barrelAmount);
            if (barrelIndex < 0 || barrelIndex >= barrelCount)
                return;

            // Ensure arrays are initialized
            if (barrelRecoilTicksRemaining == null || barrelRecoilTicksRemaining.Length != barrelCount)
            {
                barrelRecoilTicksRemaining = new int[barrelCount];
                barrelRecoilDistance = new float[barrelCount];
            }

            // If no recoil is active for this barrel, start fresh
            if (barrelRecoilTicksRemaining[barrelIndex] <= 0)
            {
                barrelRecoilTicksRemaining[barrelIndex] = Extension.recoilAnimation.TotalDuration;
                return;
            }

            // Recoil is already active - handle rapid-fire scenario
            float totalDuration = Extension.recoilAnimation.TotalDuration;
            float currentProgress = 1f - (float)barrelRecoilTicksRemaining[barrelIndex] / totalDuration;
            float recoilPhaseProgress = (float)Extension.recoilAnimation.recoilDuration / totalDuration;
            
            // Get current distance from curve
            float currentDistanceMultiplier = Extension.recoilAnimation.recoilCurve.Evaluate(currentProgress);
            float currentDistance = Extension.recoilAnimation.maxDistance * currentDistanceMultiplier;
            
            // Check if we're in recoil phase (moving backward) or return phase (moving forward)
            if (currentProgress < recoilPhaseProgress)
            {
                // Still in recoil phase (moving backward toward maxDistance)
                // Just reset to full duration - the barrel will continue naturally toward maxDistance
                barrelRecoilTicksRemaining[barrelIndex] = Extension.recoilAnimation.TotalDuration;
            }
            else
            {
                // In return phase (moving forward back to start)
                // Need to reverse direction and go back to maxDistance
                float targetProgress = recoilPhaseProgress;
                float closestDistance = float.MaxValue;
                
                // Search for the progress value that matches our current distance
                for (float testProgress = 0f; testProgress <= recoilPhaseProgress; testProgress += 0.005f)
                {
                    float testMultiplier = Extension.recoilAnimation.recoilCurve.Evaluate(testProgress);
                    float testDistance = Extension.recoilAnimation.maxDistance * testMultiplier;
                    float distanceDiff = Mathf.Abs(testDistance - currentDistance);
                    
                    if (distanceDiff < closestDistance)
                    {
                        closestDistance = distanceDiff;
                        targetProgress = testProgress;
                    }
                }
                
                float remainingRecoilProgress = recoilPhaseProgress - targetProgress;
                int ticksToMaxDistance = Mathf.CeilToInt(remainingRecoilProgress * totalDuration);
                
                if (ticksToMaxDistance < 1)
                    ticksToMaxDistance = 1;
                
                barrelRecoilTicksRemaining[barrelIndex] = ticksToMaxDistance + Extension.recoilAnimation.returnDuration;
            }
        }

        /// <summary>
        /// Legacy recoil trigger for backwards compatibility (single barrel).
        /// </summary>
        private void TriggerRecoilLegacy()
        {
            if (Extension.recoilAnimation == null)
                return;

            if (recoilTicksRemaining <= 0)
            {
                recoilTicksRemaining = Extension.recoilAnimation.TotalDuration;
                return;
            }

            float totalDuration = Extension.recoilAnimation.TotalDuration;
            float currentProgress = 1f - (float)recoilTicksRemaining / totalDuration;
            float recoilPhaseProgress = (float)Extension.recoilAnimation.recoilDuration / totalDuration;
            
            float currentDistanceMultiplier = Extension.recoilAnimation.recoilCurve.Evaluate(currentProgress);
            float currentDistance = Extension.recoilAnimation.maxDistance * currentDistanceMultiplier;
            
            if (currentProgress < recoilPhaseProgress)
            {
                recoilTicksRemaining = Extension.recoilAnimation.TotalDuration;
            }
            else
            {
                float targetProgress = recoilPhaseProgress;
                float closestDistance = float.MaxValue;
                
                for (float testProgress = 0f; testProgress <= recoilPhaseProgress; testProgress += 0.005f)
                {
                    float testMultiplier = Extension.recoilAnimation.recoilCurve.Evaluate(testProgress);
                    float testDistance = Extension.recoilAnimation.maxDistance * testMultiplier;
                    float distanceDiff = Mathf.Abs(testDistance - currentDistance);
                    
                    if (distanceDiff < closestDistance)
                    {
                        closestDistance = distanceDiff;
                        targetProgress = testProgress;
                    }
                }
                
                float remainingRecoilProgress = recoilPhaseProgress - targetProgress;
                int ticksToMaxDistance = Mathf.CeilToInt(remainingRecoilProgress * totalDuration);
                
                if (ticksToMaxDistance < 1)
                    ticksToMaxDistance = 1;
                
                recoilTicksRemaining = ticksToMaxDistance + Extension.recoilAnimation.returnDuration;
            }
        }

        /// <summary>
        /// Trigger firing animation.
        /// Handles both sequential (one barrel at a time) and simultaneous (all barrels) firing.
        /// </summary>
        public void TriggerFiring()
        {
            try
        {
            if (Extension.firingAnimation != null && Extension.firingAnimation.enabled)
            {
                    int barrelCount = Mathf.Max(1, Extension.barrelAmount);
                    
                    // Ensure per-barrel array is initialized
                    if (barrelFiringTicksRemaining == null || barrelFiringTicksRemaining.Length != barrelCount)
                    {
                        barrelFiringTicksRemaining = new int[barrelCount];
                    }
                    
                    if (Extension.sequentialFiring && barrelCount > 1)
                    {
                        // Sequential firing: only the current barrel fires
                        barrelFiringTicksRemaining[currentSequentialBarrel] = Extension.firingAnimation.durationTicks;
                        
                        // Spawn muzzle flash effect for this barrel only
                        if (!string.IsNullOrEmpty(Extension.firingAnimation.muzzleFlashEffect))
                        {
                            SpawnMuzzleFlashEffectForBarrel(currentSequentialBarrel);
                        }
                        
                        // Trigger recoil for this barrel (per shot, not per burst)
                        if (Extension.recoilAnimation != null)
                        {
                            TriggerRecoilForBarrel(currentSequentialBarrel);
                        }
                        
                        // Move to next barrel for next trigger
                        currentSequentialBarrel = (currentSequentialBarrel + 1) % barrelCount;
                    }
                    else
                    {
                        // Simultaneous firing: all barrels fire together
                        for (int i = 0; i < barrelCount; i++)
                        {
                            barrelFiringTicksRemaining[i] = Extension.firingAnimation.durationTicks;
                        }
                        
                        // Spawn muzzle flash effect for all barrels
                        if (!string.IsNullOrEmpty(Extension.firingAnimation.muzzleFlashEffect))
                        {
                            for (int i = 0; i < barrelCount; i++)
                            {
                                SpawnMuzzleFlashEffectForBarrel(i);
                            }
                        }
                    }
                    
                    // Legacy support: keep old firingTicksRemaining for backwards compatibility
                firingTicksRemaining = Extension.firingAnimation.durationTicks;
                    
                    // Debug: Log firing trigger
                    string triggerDebugKey = $"TRIGGER_{parent.def.defName}";
                    if (!loggedTypes.Contains(triggerDebugKey))
                    {
                        loggedTypes.Add(triggerDebugKey);
                        Log.Message($"[Barrel Flash Debug] TriggerFiring() called for {parent.def.defName}. " +
                            $"barrelAmount: {barrelCount}, sequentialFiring: {Extension.sequentialFiring}, " +
                            $"drawFlash: {Extension.firingAnimation.drawFlash}, " +
                            $"muzzleFlashEffect: {Extension.firingAnimation.muzzleFlashEffect ?? "null"}");
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[Barrel Animation] Error in TriggerFiring for {parent?.def?.defName ?? "unknown"}: {ex}");
            }
        }

        /// <summary>
        /// Spawns the configured muzzle flash effect at the barrel tip position.
        /// </summary>
        private void SpawnMuzzleFlashEffect()
        {
            // For single barrel, use barrel index 0
            SpawnMuzzleFlashEffectForBarrel(0);
        }

        /// <summary>
        /// Spawns the configured muzzle flash effect at the specified barrel's tip position.
        /// </summary>
        private void SpawnMuzzleFlashEffectForBarrel(int barrelIndex)
        {
            try
            {
                if (parent?.Map == null)
                    return;

                // Find the EffecterDef by name
                EffecterDef effectDef = DefDatabase<EffecterDef>.GetNamedSilentFail(Extension.firingAnimation.muzzleFlashEffect);
                if (effectDef == null)
                {
                    Log.Warning($"[Barrel Animation] EffecterDef '{Extension.firingAnimation.muzzleFlashEffect}' not found for {parent.def.defName}");
                    return;
                }

                // Calculate flash position at barrel tip
                float barrelRotation = GetCurrentBarrelRotation();
                float angleRad = barrelRotation * Mathf.Deg2Rad;
                
                Vector3 barrelOffset = GetCurrentBarrelOffset();
                
                // Calculate forward direction (barrel facing direction)
                Vector3 forwardDirection = new Vector3(
                    Mathf.Sin(angleRad),
                    0f,
                    Mathf.Cos(angleRad)
                );
                
                // Calculate perpendicular direction for barrel spacing (90 degrees clockwise)
                Vector3 perpendicularDirection = new Vector3(
                    Mathf.Cos(angleRad),  // Perpendicular to forward
                    0f,
                    -Mathf.Sin(angleRad)  // Perpendicular to forward
                );
                
                // Calculate barrel position offset (for multi-barrel)
                int barrelCount = Mathf.Max(1, Extension.barrelAmount);
                float barrelPositionOffset = GetBarrelPositionOffset(barrelIndex, barrelCount);
                
                // Position effect at barrel tip (barrel offset + forward offset + spacing offset)
                float flashDistance = Extension.barrelDrawSize * 0.5f + Extension.firingAnimation.flashOffset;
                Vector3 effectPos = parent.DrawPos + barrelOffset + forwardDirection * flashDistance + perpendicularDirection * barrelPositionOffset;
                
                // Create and trigger the effect
                Effecter effect = new Effecter(effectDef);
                IntVec3 effectCell = effectPos.ToIntVec3();
                TargetInfo target = new TargetInfo(effectCell, parent.Map, false);
                effect.Trigger(target, target);
            }
            catch (Exception ex)
            {
                Log.Error($"[Barrel Animation] Error spawning muzzle flash effect for barrel {barrelIndex} of {parent?.def?.defName ?? "unknown"}: {ex}");
            }
        }

        /// <summary>
        /// Get the current barrel rotation (turret top rotation only, no recoil rotation).
        /// </summary>
        public float GetCurrentBarrelRotation()
        {
            float rotation = 0f;
            bool gotTurretTopRotation = false;
            string debugKey = $"ROTATION_{parent.def.defName}";

            if (Extension.inheritTurretRotation && Turret != null)
            {
                // Debug: Log turret info once
                if (!loggedTypes.Contains(debugKey))
                {
                    loggedTypes.Add(debugKey);
                    Log.Message($"[Barrel Debug] Turret: {parent.def.defName}, Turret Type: {Turret.GetType().Name}, " +
                        $"CETurretTop is null: {CETurretTop == null}");
                if (CETurretTop != null)
                {
                        Log.Message($"[Barrel Debug] TurretTop Type: {CETurretTop.GetType().FullName}");
                    }
                }
                
                // Try to get turret top rotation - works with both vanilla and CE turrets
                if (CETurretTop != null)
                {
                    // CE turret top - try multiple property names
                    var curRotationProp = CETurretTop.GetType().GetProperty("CurRotation");
                    if (curRotationProp != null)
                    {
                        try
                        {
                            var value = curRotationProp.GetValue(CETurretTop);
                            if (value != null)
                            {
                                rotation = (float)value;
                                gotTurretTopRotation = true;
                                
                                // Debug success
                                string successKey = $"SUCCESS_{debugKey}";
                                if (!loggedTypes.Contains(successKey))
                                {
                                    loggedTypes.Add(successKey);
                                    Log.Message($"[Barrel Debug] Successfully reading CurRotation property! Current value: {rotation}°");
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            string errorKey = $"ERROR_{debugKey}_CurRotation";
                            if (!loggedTypes.Contains(errorKey))
                            {
                                loggedTypes.Add(errorKey);
                                Log.Warning($"[Barrel Debug] Error reading CurRotation: {ex.Message}");
                            }
                        }
                    }
                    
                    // If CurRotation didn't work, try alternative property names
                    if (!gotTurretTopRotation)
                    {
                        var altProp = CETurretTop.GetType().GetProperty("curRotation");
                        if (altProp != null)
                        {
                            try
                            {
                                var value = altProp.GetValue(CETurretTop);
                                if (value != null)
                                {
                                    rotation = (float)value;
                                    gotTurretTopRotation = true;
                                }
                            }
                            catch { }
                        }
                    }
                    
                    // Try field instead of property
                    if (!gotTurretTopRotation)
                    {
                        var rotationField = CETurretTop.GetType().GetField("CurRotation", 
                            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                        if (rotationField != null)
                        {
                            try
                            {
                                var value = rotationField.GetValue(CETurretTop);
                                if (value != null)
                                {
                                    rotation = (float)value;
                                    gotTurretTopRotation = true;
                                }
                            }
                            catch { }
                        }
                    }
                    
                    // Try alternative field names
                    if (!gotTurretTopRotation)
                    {
                        var altField = CETurretTop.GetType().GetField("curRotation", 
                            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                        if (altField != null)
                        {
                            try
                            {
                                var value = altField.GetValue(CETurretTop);
                                if (value != null)
                                {
                                    rotation = (float)value;
                                    gotTurretTopRotation = true;
                                }
                            }
                            catch { }
                        }
                    }
                    
                    // Try method call if property/field doesn't work
                    if (!gotTurretTopRotation)
                    {
                        var getRotationMethod = CETurretTop.GetType().GetMethod("GetRotation", 
                            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                        if (getRotationMethod != null && getRotationMethod.GetParameters().Length == 0)
                        {
                            try
                            {
                                var value = getRotationMethod.Invoke(CETurretTop, null);
                                if (value != null)
                                {
                                    rotation = (float)value;
                                    gotTurretTopRotation = true;
                                }
                            }
                            catch { }
                        }
                    }
                    
                    // Debug: Log available members if rotation detection fails (only once per type)
                    if (!gotTurretTopRotation)
                    {
                        var turretTopType = CETurretTop.GetType();
                        string typeName = $"FAILED_{turretTopType.FullName}";
                        
                        // Only log once per type to avoid spam
                        if (!loggedTypes.Contains(typeName))
                        {
                            loggedTypes.Add(typeName);
                            
                            // Log available members to help diagnose rotation detection issues
                            var props = turretTopType.GetProperties().Select(p => $"{p.Name} ({p.PropertyType.Name})").ToList();
                            var fields = turretTopType.GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                                .Select(f => $"{f.Name} ({f.FieldType.Name})").ToList();
                            
                            Log.Warning($"[Barrel Debug] Could not get turret top rotation for type {turretTopType.Name}.\n" +
                                $"Properties ({props.Count}): {string.Join(", ", props)}\n" +
                                $"Fields ({fields.Count}): {string.Join(", ", fields.Take(20))}");
                        }
                    }
                }
                
                // Fallback: try to get rotation from turret building itself
                if (!gotTurretTopRotation)
                {
                    // Try to get rotation from turret building (for CE turrets, might be stored here)
                    var turretRotationProp = Turret.GetType().GetProperty("CurRotation");
                    if (turretRotationProp != null)
                    {
                        try
                        {
                            var value = turretRotationProp.GetValue(Turret);
                            if (value != null)
                            {
                                rotation = (float)value;
                                gotTurretTopRotation = true;
                                
                                string successKey = $"SUCCESS_TURRET_{debugKey}";
                                if (!loggedTypes.Contains(successKey))
                                {
                                    loggedTypes.Add(successKey);
                                    Log.Message($"[Barrel Debug] Got rotation from Turret.CurRotation: {rotation}°");
                                }
                            }
                        }
                        catch { }
                    }
                }
                
                // Final fallback: use base rotation (for vanilla turrets or if all else fails)
                // This only gives placement rotation, not aiming rotation
                if (!gotTurretTopRotation)
                {
                    rotation = Turret.Rotation.AsAngle;
                    
                    string fallbackKey = $"FALLBACK_{debugKey}";
                    if (!loggedTypes.Contains(fallbackKey))
                    {
                        loggedTypes.Add(fallbackKey);
                        Log.Warning($"[Barrel Debug] Using fallback base rotation for {parent.def.defName}: {rotation}° (This won't track aiming!)");
                    }
                }
            }

            // Recoil should NOT affect rotation - only position movement
            // Removed: if (Extension.recoilAnimation.affectsRotation) { rotation += currentRecoilAngle; }

            // Debug: Log rotation changes every 60 ticks
            ticksSinceLastRotationLog++;
            if (ticksSinceLastRotationLog >= 60)
            {
                if (Mathf.Abs(rotation - lastLoggedRotation) > 0.1f)
                {
                    Log.Message($"[Barrel Debug] {parent.def.defName} rotation changed: {lastLoggedRotation:F1}° → {rotation:F1}°");
                    lastLoggedRotation = rotation;
                }
                ticksSinceLastRotationLog = 0;
            }

            return rotation;
        }

        /// <summary>
        /// Get the current spinning animation frame.
        /// </summary>
        public int GetCurrentSpinFrame()
        {
            if (barrelGraphic is Graphic_Collection collection)
            {
                // Use reflection to access the protected subGraphics array
                var subGraphicsField = typeof(Graphic_Collection).GetField("subGraphics", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                int frameCount = subGraphicsField?.GetValue(collection) is Graphic[] subGraphics ? subGraphics.Length : 0;
                if (frameCount > 0)
                {
                    return Mathf.FloorToInt(currentSpinFrame) % frameCount;
                }
            }
            return 0;
        }

        /// <summary>
        /// Get the current barrel position offset.
        /// The base offset is rotated based on the turret top's current rotation (aiming direction) to ensure proper positioning.
        /// </summary>
        public Vector3 GetCurrentBarrelOffset()
        {
            // Get turret top rotation angle (for rotating the offset)
            // This includes both base rotation and aiming rotation
            float turretTopRotationAngle = GetCurrentBarrelRotation();

            // Rotate the barrel offset based on turret top's current rotation
            // RimWorld uses: X = East/West, Z = North/South
            // Rotation: 0° = North, 90° = East, 180° = South, 270° = West
            // RimWorld rotations are clockwise, so we use clockwise rotation matrix:
            // x' = x*cos(θ) + z*sin(θ)
            // z' = -x*sin(θ) + z*cos(θ)
            float baseAngleRad = turretTopRotationAngle * Mathf.Deg2Rad;
            float cosAngle = Mathf.Cos(baseAngleRad);
            float sinAngle = Mathf.Sin(baseAngleRad);

            Vector3 rotatedOffset = new Vector3(
                Extension.barrelOffset.x * cosAngle + Extension.barrelOffset.z * sinAngle,
                Extension.barrelOffset.y, // Y (vertical) doesn't rotate
                -Extension.barrelOffset.x * sinAngle + Extension.barrelOffset.z * cosAngle
            );

            // Debug: Log offset rotation (only occasionally to avoid spam)
            string offsetDebugKey = $"OFFSET_{parent.def.defName}";
            if (!loggedTypes.Contains(offsetDebugKey))
            {
                loggedTypes.Add(offsetDebugKey);
                Log.Message($"[Barrel Debug] Offset rotation for {parent.def.defName}: " +
                    $"Original offset: ({Extension.barrelOffset.x:F2}, {Extension.barrelOffset.y:F2}, {Extension.barrelOffset.z:F2}), " +
                    $"Rotation: {turretTopRotationAngle:F1}°, " +
                    $"Rotated offset: ({rotatedOffset.x:F2}, {rotatedOffset.y:F2}, {rotatedOffset.z:F2})");
            }

            Vector3 offset = rotatedOffset;

            // Add recoil offset (backward from the direction the barrel is currently facing)
            // For multi-barrel setups, recoil is applied per-barrel in DrawBarrel()
            // Only apply global recoil for single-barrel setups
            int barrelCount = Mathf.Max(1, Extension.barrelAmount);
            if (barrelCount == 1 && recoilTicksRemaining > 0 && Extension.recoilAnimation != null)
            {
                float totalDuration = Extension.recoilAnimation.TotalDuration;
                if (totalDuration > 0)
            {
                    float recoilProgress = 1f - (float)recoilTicksRemaining / totalDuration;
                float recoilMultiplier = Extension.recoilAnimation.recoilCurve.Evaluate(recoilProgress);
                    float barrelRotation = GetCurrentBarrelRotation();
                    float recoilAngleRad = barrelRotation * Mathf.Deg2Rad;
                Vector3 recoilDirection = new(
                        -Mathf.Sin(recoilAngleRad),  // Negative because recoil goes backward
                    0f,
                        -Mathf.Cos(recoilAngleRad)   // Negative because recoil goes backward
                );
                offset += recoilDirection * Extension.recoilAnimation.maxDistance * recoilMultiplier;
                }
            }

            // Firing animation position offset removed - only muzzle flash is used
            // No position offset or scale transformations from firing animation

            return offset;
        }

        /// <summary>
        /// Get the current barrel scale (legacy, for backwards compatibility).
        /// </summary>
        public float GetCurrentBarrelScale()
        {
            return GetBarrelScale(0);
        }

        /// <summary>
        /// Get the scale for a specific barrel.
        /// Firing animation no longer applies scale - only muzzle flash is shown.
        /// </summary>
        private float GetBarrelScale(int barrelIndex)
        {
            // Firing animation scale removed - only muzzle flash is used
            return Extension.barrelDrawSize;
        }

        private void UpdateRecoil()
        {
            // Update legacy recoil for backwards compatibility
            if (recoilTicksRemaining > 0)
            {
                recoilTicksRemaining--;

                if (Extension.recoilAnimation != null)
                {
                    float totalDuration = Extension.recoilAnimation.TotalDuration;
                    if (totalDuration > 0)
                    {
                        float progress = 1f - (float)recoilTicksRemaining / totalDuration;
                    float recoilMultiplier = Extension.recoilAnimation.recoilCurve.Evaluate(progress);

                    currentRecoilDistance = Extension.recoilAnimation.maxDistance * recoilMultiplier;
                    }

                    currentRecoilAngle = 0f;
                }
            }
            else
            {
                currentRecoilDistance = 0f;
                currentRecoilAngle = 0f;
            }
            
            // Update per-barrel recoil
            if (barrelRecoilTicksRemaining != null && Extension.recoilAnimation != null)
            {
                float totalDuration = Extension.recoilAnimation.TotalDuration;
                for (int i = 0; i < barrelRecoilTicksRemaining.Length; i++)
                {
                    if (barrelRecoilTicksRemaining[i] > 0)
                    {
                        barrelRecoilTicksRemaining[i]--;
                        
                        if (totalDuration > 0)
                        {
                            float progress = 1f - (float)barrelRecoilTicksRemaining[i] / totalDuration;
                            float recoilMultiplier = Extension.recoilAnimation.recoilCurve.Evaluate(progress);
                            barrelRecoilDistance[i] = Extension.recoilAnimation.maxDistance * recoilMultiplier;
                        }
                    }
                    else
                    {
                        barrelRecoilDistance[i] = 0f;
                    }
                }
            }
        }

        /// <summary>
        /// Get recoil offset for a specific barrel.
        /// Returns backward recoil offset (negative direction).
        /// </summary>
        private Vector3 GetBarrelRecoilOffset(int barrelIndex, float barrelRotation)
        {
            if (Extension.recoilAnimation == null)
                return Vector3.zero;

            int barrelCount = Mathf.Max(1, Extension.barrelAmount);
            
            // Use per-barrel recoil if available
            float recoilDistance = 0f;
            if (barrelRecoilTicksRemaining != null && barrelIndex < barrelRecoilTicksRemaining.Length && barrelRecoilTicksRemaining[barrelIndex] > 0)
            {
                recoilDistance = barrelRecoilDistance != null && barrelIndex < barrelRecoilDistance.Length 
                    ? barrelRecoilDistance[barrelIndex] 
                    : 0f;
            }
            // Legacy support: use global recoil for single barrel
            else if (barrelCount == 1 && recoilTicksRemaining > 0)
            {
                recoilDistance = currentRecoilDistance;
            }

            if (recoilDistance <= 0f)
                return Vector3.zero;

            // Recoil goes backward (negative direction)
            float recoilAngleRad = barrelRotation * Mathf.Deg2Rad;
            Vector3 recoilDirection = new Vector3(
                -Mathf.Sin(recoilAngleRad),  // Negative because recoil goes backward
                0f,
                -Mathf.Cos(recoilAngleRad)   // Negative because recoil goes backward
            );

            return recoilDirection * recoilDistance;
        }

        /// <summary>
        /// Get firing animation offset for a specific barrel.
        /// Firing animation no longer applies position offset - only muzzle flash is shown.
        /// </summary>
        private Vector3 GetBarrelFiringOffset(int barrelIndex, float barrelRotation)
        {
            // Firing animation position offset removed - only muzzle flash is used
            return Vector3.zero;
        }

        private void UpdateSpinning()
        {
            if (Extension.spinningAnimation != null && Extension.spinningAnimation.enabled)
            {
                float targetSpeed = Extension.spinningAnimation.spinWhenIdle
                    ? Extension.spinningAnimation.baseSpeed
                    : 0f;

                // Increase speed when firing
                if (firingTicksRemaining > 0)
                {
                    targetSpeed = Extension.spinningAnimation.baseSpeed * Extension.spinningAnimation.firingMultiplier;
                }

                // Smooth speed changes
                if (currentSpinSpeed < targetSpeed)
                {
                    currentSpinSpeed += Extension.spinningAnimation.acceleration;
                    if (currentSpinSpeed > targetSpeed)
                        currentSpinSpeed = targetSpeed;
                }
                else if (currentSpinSpeed > targetSpeed)
                {
                    currentSpinSpeed *= Extension.spinningAnimation.deceleration;
                    if (currentSpinSpeed < targetSpeed)
                        currentSpinSpeed = targetSpeed;
                }

                // Clamp to speed limits
                currentSpinSpeed = Mathf.Max(Extension.spinningAnimation.minSpeed,
                    Mathf.Min(currentSpinSpeed, Extension.spinningAnimation.maxSpeed));

                // Update spin frame
                currentSpinFrame += currentSpinSpeed;
            }
        }

        private void UpdateFiringAnimation()
        {
            // Update legacy firing ticks for backwards compatibility
            if (firingTicksRemaining > 0)
            {
                firingTicksRemaining--;
            }
            
            // Update per-barrel firing animations
            if (barrelFiringTicksRemaining != null)
            {
                for (int i = 0; i < barrelFiringTicksRemaining.Length; i++)
                {
                    if (barrelFiringTicksRemaining[i] > 0)
                    {
                        barrelFiringTicksRemaining[i]--;
                    }
                }
            }
        }

        /// <summary>
        /// Calculates the horizontal offset for a barrel at the given index.
        /// Returns the offset in tiles, centered around 0.
        /// For odd number of barrels: center barrel is at 0.
        /// For even number of barrels: center is between middle two barrels.
        /// </summary>
        private float GetBarrelPositionOffset(int barrelIndex, int barrelCount)
        {
            if (barrelCount <= 1)
                return 0f;
            
            // Calculate offset from center
            // For odd count (e.g., 3): indices are -1, 0, 1 -> offsets are -spacing, 0, spacing
            // For even count (e.g., 4): indices are -1.5, -0.5, 0.5, 1.5 -> offsets are -1.5*spacing, -0.5*spacing, 0.5*spacing, 1.5*spacing
            float centerIndex = (barrelCount - 1) / 2f;
            float offsetFromCenter = barrelIndex - centerIndex;
            
            return offsetFromCenter * Extension.barrelSpacing;
        }

        private bool ShouldDraw()
        {
            if (!Extension.drawWhenDestroyed && parent.Destroyed)
                return false;

            return true;
        }

        private void DrawBarrel()
        {
            int barrelCount = Mathf.Max(1, Extension.barrelAmount);
            float barrelRotation = GetCurrentBarrelRotation();
            float angleRad = barrelRotation * Mathf.Deg2Rad;
            
            // Calculate perpendicular direction for barrel spacing (90 degrees clockwise from forward)
            Vector3 perpendicularDirection = new Vector3(
                Mathf.Cos(angleRad),  // Perpendicular to forward
                0f,
                -Mathf.Sin(angleRad)  // Perpendicular to forward
            );
            
            // Base position (center)
            Vector3 baseDrawPos = parent.DrawPos + GetCurrentBarrelOffset();
            
            // Adjust Y offset based on drawOnTop setting
            if (Extension.drawOnTop)
            {
                baseDrawPos.y += 0.1f; // Higher offset when drawing on top
            }
            else
            {
                baseDrawPos.y -= Extension.drawLayerOffset; // Negative offset when drawing below turret top
            }

            // Get the appropriate graphic for current spin frame
            Graphic graphicToUse = barrelGraphic;
            Material materialToUse = barrelMaterial;

            if (graphicToUse is Graphic_Collection collection)
            {
                // Use reflection to access the protected subGraphics array
                var subGraphicsField = typeof(Graphic_Collection).GetField("subGraphics", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                int frameIndex = GetCurrentSpinFrame();
                if (subGraphicsField?.GetValue(collection) is Graphic[] subGraphics && frameIndex >= 0 && frameIndex < subGraphics.Length)
                {
                    graphicToUse = subGraphics[frameIndex];
                    materialToUse = graphicToUse.MatSingle;
                }
            }

            // Draw each barrel
            for (int i = 0; i < barrelCount; i++)
            {
                // Calculate position offset for this barrel (spacing)
                float barrelPositionOffset = GetBarrelPositionOffset(i, barrelCount);
                Vector3 barrelDrawPos = baseDrawPos + perpendicularDirection * barrelPositionOffset;
                
                // Add per-barrel recoil offset (moves backward)
                barrelDrawPos += GetBarrelRecoilOffset(i, barrelRotation);
                
                // Add per-barrel firing animation offset (moves forward)
                barrelDrawPos += GetBarrelFiringOffset(i, barrelRotation);

                // Get per-barrel scale (for firing animation)
                float barrelScale = GetBarrelScale(i);

                Matrix4x4 matrix = Matrix4x4.TRS(
                    barrelDrawPos,
                    Quaternion.Euler(0f, barrelRotation, 0f),
                    new Vector3(barrelScale, 1f, barrelScale)
                );

                // Use the same drawing layer as buildings to ensure proper order
            Graphics.DrawMesh(MeshPool.plane10, matrix, materialToUse, 0);

                // Draw firing flash for this barrel if enabled
                bool barrelIsFiring = barrelFiringTicksRemaining != null && 
                                     i < barrelFiringTicksRemaining.Length && 
                                     barrelFiringTicksRemaining[i] > 0;
                
                if (barrelIsFiring && Extension.firingAnimation != null && Extension.firingAnimation.drawFlash)
                {
                    DrawFiringFlashForBarrel(i);
                }
            }
            
            // Legacy support: draw flash for old firingTicksRemaining if no per-barrel tracking
            if (barrelCount == 1 && firingTicksRemaining > 0 && Extension.firingAnimation != null && Extension.firingAnimation.drawFlash)
            {
                DrawFiringFlashForBarrel(0);
            }
        }

        /// <summary>
        /// Draws a bright flash/light effect at the barrel tip when firing.
        /// Uses Graphics.DrawMesh with a glow shader for maximum brightness.
        /// Legacy method - calls DrawFiringFlashForBarrel(0) for backwards compatibility.
        /// </summary>
        private void DrawFiringFlash()
        {
            DrawFiringFlashForBarrel(0);
        }

        /// <summary>
        /// Draws a bright flash/light effect at the specified barrel's tip when firing.
        /// Uses Graphics.DrawMesh with a glow shader for maximum brightness.
        /// </summary>
        private void DrawFiringFlashForBarrel(int barrelIndex)
        {
            if (Extension.firingAnimation == null || !Extension.firingAnimation.drawFlash)
                return;

            // Check if this barrel is actually firing
            bool barrelIsFiring = barrelFiringTicksRemaining != null &&
                                 barrelIndex < barrelFiringTicksRemaining.Length &&
                                 barrelFiringTicksRemaining[barrelIndex] > 0;

            // Legacy support: also check old firingTicksRemaining for single barrel
            if (!barrelIsFiring && barrelIndex == 0 && firingTicksRemaining > 0)
            {
                barrelIsFiring = true;
            }

            if (!barrelIsFiring)
                return;

            // Debug: Log flash drawing
            string flashDebugKey = $"FLASH_{parent.def.defName}_BARREL_{barrelIndex}";
            bool shouldLogFlash = !loggedTypes.Contains(flashDebugKey);
            if (shouldLogFlash)
            {
                loggedTypes.Add(flashDebugKey);
                int ticksRemainingForFlash = barrelFiringTicksRemaining != null && barrelIndex < barrelFiringTicksRemaining.Length
                    ? barrelFiringTicksRemaining[barrelIndex]
                    : firingTicksRemaining;
                Log.Message($"[Barrel Flash Debug] Drawing flash for barrel {barrelIndex} of {parent.def.defName}. " +
                    $"firingTicksRemaining: {ticksRemainingForFlash}, " +
                    $"drawFlash: {Extension.firingAnimation.drawFlash}, " +
                    $"flashBrightness: {Extension.firingAnimation.flashBrightness}");
            }

            // Calculate flash position at barrel tip
            float barrelRotation = GetCurrentBarrelRotation();
            float angleRad = barrelRotation * Mathf.Deg2Rad;

            // Get barrel offset to find barrel tip position
            Vector3 barrelOffset = GetCurrentBarrelOffset();

            // Calculate forward direction (barrel facing direction)
            Vector3 forwardDirection = new Vector3(
                Mathf.Sin(angleRad),
                0f,
                Mathf.Cos(angleRad)
            );

            // Calculate perpendicular direction for barrel spacing
            Vector3 perpendicularDirection = new Vector3(
                Mathf.Cos(angleRad),  // Perpendicular to forward
                0f,
                -Mathf.Sin(angleRad)  // Perpendicular to forward
            );

            // Calculate barrel position offset (for multi-barrel)
            int barrelCount = Mathf.Max(1, Extension.barrelAmount);
            float barrelPositionOffset = GetBarrelPositionOffset(barrelIndex, barrelCount);

            // Position flash at barrel tip (barrel offset + forward offset + spacing offset)
            float flashDistance = Extension.barrelDrawSize * 0.5f + Extension.firingAnimation.flashOffset;
            Vector3 flashPos = parent.DrawPos + barrelOffset + forwardDirection * flashDistance + perpendicularDirection * barrelPositionOffset;

            // Set altitude to VisEffects layer (same as muzzle flash)
            flashPos.y = AltitudeLayer.VisEffects.AltitudeFor();

            // Calculate flash intensity based on animation progress for this barrel
            int ticksRemainingForIntensity = barrelFiringTicksRemaining != null && barrelIndex < barrelFiringTicksRemaining.Length
                ? barrelFiringTicksRemaining[barrelIndex]
                : firingTicksRemaining;
            float flashProgress = 1f - (float)ticksRemainingForIntensity / Extension.firingAnimation.durationTicks;
            float intensity = Extension.firingAnimation.flashIntensityCurve.Evaluate(flashProgress);

            if (shouldLogFlash)
            {
                Log.Message($"[Barrel Flash Debug] Flash progress: {flashProgress:F2}, intensity: {intensity:F2}, " +
                    $"flashPos: ({flashPos.x:F2}, {flashPos.y:F2}, {flashPos.z:F2}), " +
                    $"barrelRotation: {barrelRotation:F1}°");
            }

            // Calculate flash scale (size) - use flashSize parameter, modified by intensity
            float flashScale = Extension.firingAnimation.flashSize * intensity;

            // Calculate flash color
            // Start with base color from XML (e.g., orange/yellow: 1, 0.8, 0.4)
            Color flashColor = Extension.firingAnimation.flashColor;

            // Apply intensity to color (for fade effect over time)
            flashColor.r *= intensity;
            flashColor.g *= intensity;
            flashColor.b *= intensity;
            flashColor.a *= intensity; // Alpha fades with intensity

            // Apply brightness multiplier directly (Graphics.DrawMesh can handle bright colors)
            // Brightness makes the color more intense/visible
            float brightnessMultiplier = Extension.firingAnimation.flashBrightness;
            flashColor.r *= brightnessMultiplier;
            flashColor.g *= brightnessMultiplier;
            flashColor.b *= brightnessMultiplier;
            // Don't clamp RGB - let it go above 1.0 for HDR-like bright effects
            // Only clamp alpha
            flashColor.a = Mathf.Clamp01(flashColor.a);

            if (shouldLogFlash)
            {
                Log.Message($"[Barrel Flash Debug] Flash scale: {flashScale:F2} (size: {Extension.firingAnimation.flashSize}, intensity: {intensity:F2})");
                Log.Message($"[Barrel Flash Debug] Flash color: R={flashColor.r:F2}, G={flashColor.g:F2}, B={flashColor.b:F2}, A={flashColor.a:F2} (brightness: {Extension.firingAnimation.flashBrightness})");
            }

            // Draw flash using Graphics.DrawMesh with glow shader
            // Use multi-layer approach for stronger glow effect
            try
            {
                if (flashScale > 0.01f && flashMaterial != null)
                {
                    // Create rotation matrix to face the barrel direction
                    Quaternion flashRotation = Quaternion.Euler(0f, barrelRotation, 0f);

                    // Layer 1: Large outer glow (soft, dim halo)
                    float glowScale = flashScale * 3f; // 3x larger for soft halo
                    Color glowColor = flashColor * 0.3f; // 30% brightness for outer glow
                    glowColor.a *= 0.5f; // More transparent

                    Matrix4x4 glowMatrix = Matrix4x4.TRS(
                        flashPos,
                        flashRotation,
                        new Vector3(glowScale, 1f, glowScale)
                    );

                    Material glowMaterial = new Material(flashMaterial);
                    glowMaterial.color = glowColor;

                    Graphics.DrawMesh(
                        MeshPool.plane10,
                        glowMatrix,
                        glowMaterial,
                        0
                    );

                    // Layer 2: Medium glow (adds depth)
                    float midGlowScale = flashScale * 1.8f;
                    Color midGlowColor = flashColor * 0.6f;
                    midGlowColor.a *= 0.7f;

                    Matrix4x4 midGlowMatrix = Matrix4x4.TRS(
                        flashPos,
                        flashRotation,
                        new Vector3(midGlowScale, 1f, midGlowScale)
                    );

                    Material midGlowMaterial = new Material(flashMaterial);
                    midGlowMaterial.color = midGlowColor;

                    Graphics.DrawMesh(
                        MeshPool.plane10,
                        midGlowMatrix,
                        midGlowMaterial,
                        0
                    );

                    // Layer 3: Core flash (bright center)
                    Matrix4x4 flashMatrix = Matrix4x4.TRS(
                        flashPos,
                        flashRotation,
                        new Vector3(flashScale, 1f, flashScale)
                    );

                    Material flashMaterialCore = new Material(flashMaterial);
                    flashMaterialCore.color = flashColor; // Full brightness

                    Graphics.DrawMesh(
                        MeshPool.plane10,
                        flashMatrix,
                        flashMaterialCore,
                        0
                    );

                    // Clean up temporary materials (Unity will handle this at end of frame)
                    // Don't need explicit Destroy - letting GC handle it is fine for per-frame materials

                    if (shouldLogFlash)
                    {
                        Log.Message($"[Barrel Flash Debug] Drew multi-layer flash at ({flashPos.x:F2}, {flashPos.y:F2}, {flashPos.z:F2}) with scale {flashScale:F2}");
                    }
                }
                else if (shouldLogFlash)
                {
                    Log.Message($"[Barrel Flash Debug] Skipping flash drawing - scale too small: {flashScale:F2} or material null: {flashMaterial == null}");
                }
            }
            catch (Exception ex)
            {
                Log.Warning($"[Barrel Flash Debug] Error drawing flash mesh: {ex.Message}");
            }
        }

        /// <summary>
        /// Expose data for saving/loading.
        /// </summary>
        public override void PostExposeData()
        {
            base.PostExposeData();

            Scribe_Values.Look(ref recoilTicksRemaining, "recoilTicksRemaining", 0);
            Scribe_Values.Look(ref currentRecoilDistance, "currentRecoilDistance", 0f);
            Scribe_Values.Look(ref currentRecoilAngle, "currentRecoilAngle", 0f);
            Scribe_Values.Look(ref currentSpinFrame, "currentSpinFrame", 0f);
            Scribe_Values.Look(ref currentSpinSpeed, "currentSpinSpeed", 0f);
            Scribe_Values.Look(ref firingTicksRemaining, "firingTicksRemaining", 0);
            Scribe_Values.Look(ref currentSequentialBarrel, "currentSequentialBarrel", 0);

            // Save/load per-barrel firing state
            List<int> barrelFiringTicksList = null;
            if (Scribe.mode == LoadSaveMode.Saving)
            {
                if (barrelFiringTicksRemaining != null)
                    barrelFiringTicksList = barrelFiringTicksRemaining.ToList();
            }
            Scribe_Collections.Look(ref barrelFiringTicksList, "barrelFiringTicksRemaining", LookMode.Value);
            if (Scribe.mode == LoadSaveMode.LoadingVars)
            {
                if (barrelFiringTicksList != null)
                    barrelFiringTicksRemaining = barrelFiringTicksList.ToArray();
                else
                    barrelFiringTicksRemaining = new int[0];
            }
            
            // Save/load per-barrel recoil state
            List<int> barrelRecoilTicksList = null;
            List<float> barrelRecoilDistanceList = null;
            if (Scribe.mode == LoadSaveMode.Saving)
            {
                if (barrelRecoilTicksRemaining != null)
                    barrelRecoilTicksList = barrelRecoilTicksRemaining.ToList();
                if (barrelRecoilDistance != null)
                    barrelRecoilDistanceList = barrelRecoilDistance.ToList();
            }
            Scribe_Collections.Look(ref barrelRecoilTicksList, "barrelRecoilTicksRemaining", LookMode.Value);
            Scribe_Collections.Look(ref barrelRecoilDistanceList, "barrelRecoilDistance", LookMode.Value);
            if (Scribe.mode == LoadSaveMode.LoadingVars)
            {
                if (barrelRecoilTicksList != null)
                    barrelRecoilTicksRemaining = barrelRecoilTicksList.ToArray();
                else
                    barrelRecoilTicksRemaining = new int[0];
                    
                if (barrelRecoilDistanceList != null)
                    barrelRecoilDistance = barrelRecoilDistanceList.ToArray();
                else
                    barrelRecoilDistance = new float[0];
            }
        }
    }
}
