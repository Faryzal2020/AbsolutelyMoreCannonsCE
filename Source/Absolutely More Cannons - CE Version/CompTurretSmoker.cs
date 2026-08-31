using System;
using System.Collections.Generic;
using UnityEngine;
using Verse;
using RimWorld;

namespace AbsolutelyMoreCannons
{
    /// <summary>
    /// ThingComp that handles turret smoke spawning for three smoke types:
    /// - Muzzle smoke: Directional puff when firing
    /// - Heat smoke: Continuous emission when barrel is hot
    /// - Shockwave smoke: Radial ground burst from firing shockwave
    /// </summary>
    public class CompTurretSmoker : ThingComp
    {
        private CompProperties_TurretSmoker Props => (CompProperties_TurretSmoker)props;
        
        // State tracking
        private float currentBurstHeat = 0f; // Tracks heat buildup for heat smoke
        private CompTurretBarrel barrelComp; // Reference to barrel component for rotation/offset
        private Map cachedMap;
        private int ticksSinceLastShot = 0;
        private int lastMuzzleSmokeSpawn = -999999; // For throttling (negative muzzleSpawnDuration)

        /// <summary>
        /// Public accessor for current burst heat - used for mode swap heat transfer
        /// </summary>
        public float CurrentBurstHeat
        {
            get => currentBurstHeat;
            set => currentBurstHeat = value;
        }

        // Muzzle smoke queue - particles waiting to spawn
        private Queue<MuzzleSmokeQueuedSpawn> muzzleSmokeQueue = new Queue<MuzzleSmokeQueuedSpawn>();

        /// <summary>
        /// Queued muzzle smoke spawn with delay
        /// </summary>
        private class MuzzleSmokeQueuedSpawn
        {
            public int ticksUntilSpawn;
            public Vector3 barrelTipPosition;
            public Vector3 direction;
        }

        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);
            
            cachedMap = parent.Map;
            if (cachedMap == null)
                return;

            // Get barrel component for rotation and offset information
            barrelComp = parent.TryGetComp<CompTurretBarrel>();

            // Register with map component
            cachedMap.GetComponent<MapComponent_TurretSmokeManager>()?.RegisterSmoker(this);

            AMCLogger.LogTurretSmoke($"Initialized smoke system for {parent.def.defName}");
        }

        public override void PostDeSpawn(Map map)
        {
            base.PostDeSpawn(map);
            
            // Unregister from map component
            map?.GetComponent<MapComponent_TurretSmokeManager>()?.UnregisterSmoker(this);
        }

        /// <summary>
        /// Called by Harmony patch when turret fires.
        /// Increments burst heat and queues smoke spawns.
        /// </summary>
        public void OnFired(float turretRotation)
        {
            try
            {
                ticksSinceLastShot = 0;
                
                // Increment burst heat for heat smoke tracking
                currentBurstHeat += 1f;
                
                AMCLogger.LogTurretSmoke(
                    $"{parent.def.defName} fired - Heat: {currentBurstHeat:F1}/{Props.heatThreshold}");

                // Queue muzzle smoke spawn
                if (Props.muzzleEnabled)
                {
                    QueueMuzzleSmoke(turretRotation);
                }

                // Spawn shockwave smoke immediately
                if (Props.shockwaveEnabled)
                {
                    SpawnShockwaveSmoke(turretRotation);
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[AMC] Error in CompTurretSmoker.OnFired for {parent.def.defName}: {ex}");
            }
        }

        /// <summary>
        /// Component tick - processes muzzle smoke queue and updates heat decay
        /// </summary>
        public override void CompTick()
        {
            base.CompTick();
            
            ticksSinceLastShot++;

            // Process muzzle smoke queue
            ProcessMuzzleSmokeQueue();
            
            // Update burst heat decay (every second)
            if (Find.TickManager.TicksGame % 60 == 0)
            {
                UpdateBurstHeat();
            }
        }

        /// <summary>
        /// Queues muzzle smoke particles with spawn delay
        /// </summary>
        private void QueueMuzzleSmoke(float turretRotation)
        {
            try
            {
                // Calculate barrel tip position and direction (center of turret/barrels)
                Vector3 barrelTipPosition = GetBarrelTipPosition();
                Vector3 direction = GetDirectionFromRotation(turretRotation);

                // Rotate muzzle offset by turret direction (base offset)
                Vector3 rotatedOffset = RotateVector(Props.muzzleOffset, turretRotation);

                // Apply barrel spacing offset if available
                Vector3 barrelSpacingOffset = Vector3.zero;
                var compBarrel = parent.GetComp<CompTurretBarrel>();
                if (compBarrel != null)
                {
                    int barrelCount = Mathf.Max(1, compBarrel.Extension.barrelAmount);
                    if (barrelCount > 1)
                    {
                        // Get the barrel that fired this shot
                        int firedBarrelIndex = compBarrel.LastFiredBarrelIndex;
                        
                        // Get the lateral offset distance
                        float lateralOffset = compBarrel.GetBarrelPositionOffset(firedBarrelIndex, barrelCount);
                        
                        // Calculate perpendicular direction (90 deg clockwise from forward)
                        // This matches the direction used in CompTurretBarrel for flashes
                        float angleRad = turretRotation * Mathf.Deg2Rad;
                        Vector3 perpendicularDirection = new Vector3(
                            Mathf.Cos(angleRad),
                            0f,
                            -Mathf.Sin(angleRad)
                        );
                        
                        barrelSpacingOffset = perpendicularDirection * lateralOffset;
                        
                        AMCLogger.LogTurretSmoke(
                            $"[Multi-Barrel Smoke] Barrel {firedBarrelIndex}/{barrelCount} fired. " +
                            $"Lateral offset: {lateralOffset} -> {barrelSpacingOffset}");
                    }
                }
                
                // Queue the spawn with delay
                muzzleSmokeQueue.Enqueue(new MuzzleSmokeQueuedSpawn
                {
                    ticksUntilSpawn = Props.muzzleSpawnDelay,
                    barrelTipPosition = barrelTipPosition + rotatedOffset + barrelSpacingOffset,
                    direction = direction
                });

                AMCLogger.LogTurretSmoke(
                    $"Queued muzzle smoke for {parent.def.defName} - Delay: {Props.muzzleSpawnDelay} ticks");
            }
            catch (Exception ex)
            {
                Log.Error($"[AMC] Error queueing muzzle smoke: {ex}");
            }
        }

        /// <summary>
        /// Processes muzzle smoke queue, spawning particles when delay expires
        /// </summary>
        private void ProcessMuzzleSmokeQueue()
        {
            if (muzzleSmokeQueue.Count == 0)
                return;

            // Peek at front of queue
            var queued = muzzleSmokeQueue.Peek();
            queued.ticksUntilSpawn--;

            if (queued.ticksUntilSpawn <= 0)
            {
                // Spawn muzzle smoke particles
                muzzleSmokeQueue.Dequeue();
                SpawnMuzzleSmoke(queued.barrelTipPosition, queued.direction);
            }
        }

        /// <summary>
        /// Spawns muzzle smoke particles and registers them with map component
        /// </summary>
        private void SpawnMuzzleSmoke(Vector3 spawnPosition, Vector3 direction)
        {
            try
            {
                var mapComp = cachedMap.GetComponent<MapComponent_TurretSmokeManager>();
                if (mapComp == null)
                    return;

                int currentTick = Find.TickManager.TicksGame;
                int particleCount;
                int particleDelay;
                bool isThrottled = Props.muzzleSpawnDuration < 0;
                
                // Negative value = throttling mode
                if (isThrottled)
                {
                    int throttleDuration = -Props.muzzleSpawnDuration;
                    int ticksSinceLastSpawn = currentTick - lastMuzzleSmokeSpawn;
                    
                    // Check if enough time has passed since last spawn
                    if (ticksSinceLastSpawn < throttleDuration)
                    {
                        AMCLogger.LogTurretSmoke(
                            $"Muzzle smoke throttled for {parent.def.defName} - " +
                            $"Only {ticksSinceLastSpawn}/{throttleDuration} ticks passed");
                        return; // Don't spawn, throttled
                    }
                    
                    // Spawn single particle with no delay
                    particleCount = 1;
                    particleDelay = 0;
                    lastMuzzleSmokeSpawn = currentTick;
                    
                    AMCLogger.LogTurretSmoke(
                        $"Muzzle smoke spawned (throttled mode) for {parent.def.defName} - " +
                        $"Last spawn was {ticksSinceLastSpawn} ticks ago");
                }
                else
                {
                    // Positive value = normal mode with inter-particle delay
                    particleCount = Props.muzzleParticleCount;
                }

                AMCLogger.LogTurretSmokeParticleTelemetry(
                    $"═══ SPAWNING MUZZLE SMOKE ═══\n" +
                    $"  Turret: {parent.def.defName} at {parent.Position}\n" +
                    $"  Spawn Position: ({spawnPosition.x:F2}, {spawnPosition.y:F2}, {spawnPosition.z:F2})\n" +
                    $"  Direction: ({direction.x:F3}, {direction.y:F3}, {direction.z:F3})\n" +
                    $"  Mode: {(isThrottled ? "THROTTLED" : "NORMAL")}\n" +
                    $"  Particle Count: {particleCount}\n" +
                    $"  Config: Size={Props.muzzleParticleSize:F1}x | MaxVel={Props.muzzleVelocity:F1} c/s | " +
                    $"VelDur={Props.muzzleVelDuration} ticks | SpawnDur={Props.muzzleSpawnDuration} ticks\n" +
                    $"  FleckDef: {Props.muzzleFleckDef?.defName ?? "NULL"}");

                // Spawn particles
                for (int i = 0; i < particleCount; i++)
                {
                    // Apply direction cone - randomize direction within cone
                    Vector3 particleDirection = direction;
                    if (Props.directionCone > 0f)
                    {
                        // Random angle offset within cone
                        float angleOffset = Rand.Range(0f, Props.directionCone);
                        // Random side (left or right)
                        if (Rand.Bool)
                            angleOffset = -angleOffset;
                        
                        // Calculate current direction angle
                        float currentAngle = Mathf.Atan2(direction.z, direction.x) * Mathf.Rad2Deg;
                        float newAngle = (currentAngle + angleOffset) * Mathf.Deg2Rad;
                        
                        // Create new direction with offset
                        particleDirection = new Vector3(
                            Mathf.Cos(newAngle),
                            0f,
                            Mathf.Sin(newAngle)
                        ).normalized;
                    }
                    
                    // Get randomized size, velocity, and velocity duration
                    float particleSize = Props.GetRandomParticleSize();
                    float particleVelocity = Props.GetRandomMuzzleVelocity();
                    int particleVelDuration = Props.GetRandomMuzzleVelDuration();
                    
                    var particle = new MuzzleSmokeParticle
                    {
                        position = spawnPosition,
                        direction = particleDirection,
                        maxVelocity = particleVelocity,
                        velDuration = particleVelDuration,
                        ticksAlive = 0,
                        fleckDef = Props.muzzleFleckDef,
                        size = particleSize,
                        rotationRate = Rand.Range(-30f, 30f)
                    };

                    // Calculate delay based on mode
                    if (isThrottled)
                    {
                        particleDelay = 0; // No delay in throttled mode
                    }
                    else
                    {
                        // Inter-particle delay: duration / count
                        particleDelay = (Props.muzzleSpawnDuration > 0 && Props.muzzleParticleCount > 0) 
                            ? i * (Props.muzzleSpawnDuration / Props.muzzleParticleCount) 
                            : 0;
                    }

                    AMCLogger.LogTurretSmokeParticleTelemetry(
                        $"  Particle #{i}: Delay={particleDelay} ticks | Size={particleSize:F2}x | " +
                        $"Dir=({particleDirection.x:F3}, {particleDirection.z:F3}) | RotRate={particle.rotationRate:F1}°/tick");

                    // Register with map component for velocity tracking
                    mapComp.RegisterMuzzleParticle(particle, particleDelay);
                }

                AMCLogger.LogTurretSmoke(
                    $"Spawned {particleCount} muzzle particles for {parent.def.defName} " +
                    $"at {spawnPosition} toward {direction}");
            }
            catch (Exception ex)
            {
                Log.Error($"[AMC] Error spawning muzzle smoke: {ex}");
            }
        }

        /// <summary>
        /// Spawns radial shockwave smoke burst around muzzle (filled circle with S-curve sigmoid gradient density, particle size scaling, and fade out speed control)
        /// </summary>
        private void SpawnShockwaveSmoke(float turretRotation = float.NaN)
        {
            try
            {
                // Get current turret rotation
                if (float.IsNaN(turretRotation))
                {
                    turretRotation = GetTurretRotation();
                }
                
                // Rotate offset by turret direction
                Vector3 rotatedOffset = RotateVector(Props.shockwaveOffset, turretRotation);
                Vector3 centerPosition = parent.DrawPos + rotatedOffset;
                
                int totalParticles = 0;
                
                // Fade out speed calculation
                float fadeOutSpeedFactor = Mathf.Max(0.01f, Props.shockwaveFadeOutSpeed);
                float baseSolidTime = (Props.shockwaveFleckDef != null && Props.shockwaveFleckDef.solidTime > 0f) ? Props.shockwaveFleckDef.solidTime : 0.1f;
                float baseFadeOutTime = (Props.shockwaveFleckDef != null && Props.shockwaveFleckDef.fadeOutTime > 0f) ? Props.shockwaveFleckDef.fadeOutTime : 1.0f;
                
                float solidTimeOverride = baseSolidTime / fadeOutSpeedFactor;
                float airTimeLeft = (baseSolidTime + baseFadeOutTime) / fadeOutSpeedFactor;
                
                int radiusSteps = Mathf.Max(1, Mathf.CeilToInt(Props.shockwaveRadius));
                float stepSize = Props.shockwaveRadius / radiusSteps;
                
                // If gradient density is enabled, spawn a small center core cluster
                if (Props.shockwaveGradientDensity)
                {
                    int centerParticles = Mathf.Max(1, Mathf.RoundToInt(Props.shockwaveDensity * 0.15f));
                    float centerSize = Props.shockwaveParticleSize; // 100% size at center
                    for (int c = 0; c < centerParticles; c++)
                    {
                        Vector3 particlePos = centerPosition + new Vector3(Rand.Range(-0.15f, 0.15f), 0f, Rand.Range(-0.15f, 0.15f));
                        FleckCreationData data = FleckMaker.GetDataStatic(particlePos, cachedMap, Props.shockwaveFleckDef, centerSize);
                        data.rotationRate = Rand.Range(-20f, 20f);
                        data.velocityAngle = Rand.Range(0f, 360f);
                        data.velocitySpeed = Rand.Range(0.1f, 0.3f);
                        data.solidTimeOverride = solidTimeOverride;
                        data.airTimeLeft = airTimeLeft;
                        cachedMap.flecks.CreateFleck(data);
                        totalParticles++;
                    }
                }
                
                // Spawn concentric rings from r = 1 to radiusSteps with continuous radial spread
                for (int r = 1; r <= radiusSteps; r++)
                {
                    float currentRadius = stepSize * r;
                    float minRadius = (r == 1 && !Props.shockwaveGradientDensity) ? 0f : currentRadius - (stepSize * 0.5f);
                    float maxRadius = currentRadius + (stepSize * 0.5f);
                    
                    float t = (float)r / radiusSteps; // Normalized distance (0.0 at center, 1.0 at edge)
                    
                    // Sigmoid / S-curve interpolation factor (SmoothStep: 3t^2 - 2t^3)
                    float s = t * t * (3.0f - 2.0f * t);
                    
                    int particlesAtRadius;
                    if (Props.shockwaveGradientDensity)
                    {
                        // S-curve gradient density tapering from 1.0 (100% center) to 0.10 (10% edge)
                        float spatialDensityFactor = Mathf.Lerp(1.0f, 0.10f, s);
                        float densityScale = t * spatialDensityFactor;
                        particlesAtRadius = Mathf.Max(1, Mathf.RoundToInt(Props.shockwaveDensity * densityScale));
                    }
                    else
                    {
                        // Standard uniform spatial density (particle count per ring scales linearly with radius t)
                        float densityScale = t;
                        particlesAtRadius = Mathf.Max(1, Mathf.RoundToInt(Props.shockwaveDensity * densityScale));
                    }
                    
                    // Particle size calculation
                    float finalParticleSize;
                    if (Props.shockwaveGradientParticleSize)
                    {
                        // S-curve gradient particle size tapering from 1.0 (100% center) to 0.10 (10% edge)
                        float sizeFactor = Mathf.Lerp(1.0f, 0.10f, s);
                        finalParticleSize = Props.shockwaveParticleSize * sizeFactor;
                    }
                    else
                    {
                        finalParticleSize = Props.shockwaveParticleSize;
                    }
                    
                    // Spawn particles continuously between minRadius and maxRadius
                    for (int i = 0; i < particlesAtRadius; i++)
                    {
                        float angle = (360f / particlesAtRadius) * i + Rand.Range(-5f, 5f);
                        float angleRad = angle * Mathf.Deg2Rad;
                        float particleRadius = Rand.Range(minRadius, maxRadius);
                        
                        Vector3 offset = new Vector3(
                            Mathf.Cos(angleRad) * particleRadius,
                            0f,
                            Mathf.Sin(angleRad) * particleRadius
                        );
                        Vector3 particlePos = centerPosition + offset;
                        
                        // Spawn fleck
                        FleckCreationData data = FleckMaker.GetDataStatic(particlePos, cachedMap, Props.shockwaveFleckDef, finalParticleSize);
                        data.rotationRate = Rand.Range(-20f, 20f);
                        data.velocityAngle = Rand.Range(0f, 360f);
                        data.velocitySpeed = Rand.Range(0.1f, 0.3f);
                        data.solidTimeOverride = solidTimeOverride;
                        data.airTimeLeft = airTimeLeft;
                        cachedMap.flecks.CreateFleck(data);
                        
                        totalParticles++;
                    }
                }

                AMCLogger.LogTurretSmoke(
                    $"Spawned shockwave smoke for {parent.def.defName} - " +
                    $"Radius: {Props.shockwaveRadius}, Total particles: {totalParticles} across {radiusSteps} rings " +
                    $"(FadeOutSpeed: {Props.shockwaveFadeOutSpeed:F2}x, GradDensity: {Props.shockwaveGradientDensity}, GradSize: {Props.shockwaveGradientParticleSize})");
            }
            catch (Exception ex)
            {
                Log.Error($"[AMC] Error spawning shockwave smoke: {ex}");
            }
        }

        /// <summary>
        /// Updates burst heat decay over time with scaling based on heat above threshold
        /// </summary>
        private void UpdateBurstHeat()
        {
            if (currentBurstHeat > 0f)
            {
                float previousHeat = currentBurstHeat;
                
                // Calculate scaled decay rate: base + (heat above threshold * increase)
                float heatAboveThreshold = Mathf.Max(0f, currentBurstHeat - Props.heatThreshold);
                float scaledDecayRate = Props.heatDecayRate + (heatAboveThreshold * Props.decayIncrease);
                
                currentBurstHeat -= scaledDecayRate;
                
                if (currentBurstHeat < 0f)
                    currentBurstHeat = 0f;

                // Log when crossing threshold
                if (previousHeat >= Props.heatThreshold && currentBurstHeat < Props.heatThreshold)
                {
                    AMCLogger.LogTurretSmoke(
                        $"{parent.def.defName} cooled below threshold - Heat: {currentBurstHeat:F1}");
                }
            }
        }

        /// <summary>
        /// Returns true if heat smoke should be emitted
        /// </summary>
        public bool ShouldEmitHeatSmoke()
        {
            return Props.heatEnabled && currentBurstHeat >= Props.heatThreshold;
        }

        /// <summary>
        /// Gets the current scaled heat emission rate based on heat above threshold
        /// </summary>
        public float GetScaledEmissionRate()
        {
            float heatAboveThreshold = Mathf.Max(0f, currentBurstHeat - Props.heatThreshold);
            return Props.heatEmissionRate + (heatAboveThreshold * Props.emissionIncrease);
        }

        /// <summary>
        /// Gets spawn positions for heat smoke (multiple points along barrel, duplicated for each barrel)
        /// </summary>
        public Vector3[] GetHeatSmokePositions()
        {
            // Get current turret rotation
            float turretRotation = GetTurretRotation();
            int barrelCount = 1;
            
            if (barrelComp != null)
            {
                barrelCount = Mathf.Max(1, barrelComp.Extension.barrelAmount);
            }
            
            Vector3 direction = GetDirectionFromRotation(turretRotation);
            
            // Calculate total points: points per barrel * number of barrels
            int pointsPerBarrel = Props.heatEmissionPoints;
            Vector3[] positions = new Vector3[pointsPerBarrel * barrelCount];
            
            // Rotate base offset once
            Vector3 rotatedBase = RotateVector(Props.heatOffset, turretRotation);
            
            // Calculate perpendicular direction (90 deg clockwise from forward) for lateral offset
            float angleRad = turretRotation * Mathf.Deg2Rad;
            Vector3 perpendicularDirection = new Vector3(
                Mathf.Cos(angleRad),
                0f,
                -Mathf.Sin(angleRad)
            );
            
            // Generate points for each barrel
            int currentIndex = 0;
            for (int b = 0; b < barrelCount; b++)
            {
                float lateralOffset = 0f;
                // Get lateral offset for this barrel if we have the component
                if (barrelComp != null)
                {
                    lateralOffset = barrelComp.GetBarrelPositionOffset(b, barrelCount);
                }
                
                Vector3 barrelSpacingOffset = perpendicularDirection * lateralOffset;
                
                for (int p = 0; p < pointsPerBarrel; p++)
                {
                    // Base (rotated) + forward spacing + barrel lateral spacing
                    positions[currentIndex] = parent.DrawPos + rotatedBase + 
                                            (direction * Props.heatEmissionSpacing * p) + 
                                            barrelSpacingOffset;
                    currentIndex++;
                }
            }
            
            return positions;
        }

        /// <summary>
        /// Gets barrel tip position using barrel component if available
        /// </summary>
        private Vector3 GetBarrelTipPosition()
        {
            if (barrelComp != null && barrelComp.Extension != null)
            {
                // Use barrel offset from extension to approximate barrel tip
                float rotation = barrelComp.GetCurrentBarrelRotation();
                Vector3 direction = GetDirectionFromRotation(rotation);
                Vector3 barrelOffset = barrelComp.Extension.barrelOffset;
                
                // Approximate barrel tip as turret position + barrel offset magnitude forward
                float estimatedLength = barrelOffset.magnitude * 1.5f; // Estimate
                return parent.DrawPos + direction * estimatedLength;
            }
            
            // Fallback to turret position
            return parent.DrawPos;
        }

        /// <summary>
        /// Gets current turret rotation angle from barrelComp, CE top, or parent rotation fallback.
        /// </summary>
        public float GetTurretRotation()
        {
            if (barrelComp != null)
            {
                float rot = barrelComp.GetCurrentBarrelRotation();
                if (!float.IsNaN(rot)) return rot;
            }

            if (parent is Building_Turret buildingTurret)
            {
                try
                {
                    var nonSnapField = buildingTurret.GetType().GetField("NonSnapTurretRot", 
                        System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
                    if (nonSnapField != null)
                    {
                        object val = nonSnapField.GetValue(buildingTurret);
                        if (val is float fRot && fRot >= 0f) return fRot;
                    }

                    var topField = buildingTurret.GetType().GetField("top", 
                        System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
                    if (topField != null)
                    {
                        object top = topField.GetValue(buildingTurret);
                        if (top != null)
                        {
                            var curRotationProp = top.GetType().GetProperty("CurRotation", 
                                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
                            if (curRotationProp != null)
                            {
                                return (float)curRotationProp.GetValue(top);
                            }
                        }
                    }
                }
                catch { }
            }

            return parent.Rotation.AsAngle;
        }

        /// <summary>
        /// Converts rotation angle to normalized direction vector
        /// </summary>
        private Vector3 GetDirectionFromRotation(float rotationDegrees)
        {
            float angleRad = rotationDegrees * Mathf.Deg2Rad;
            return new Vector3(Mathf.Sin(angleRad), 0f, Mathf.Cos(angleRad)).normalized;
        }

        /// <summary>
        /// Rotates a vector by the given angle (in degrees)
        /// </summary>
        private Vector3 RotateVector(Vector3 vector, float rotationDegrees)
        {
            float angleRad = rotationDegrees * Mathf.Deg2Rad;
            float cos = Mathf.Cos(angleRad);
            float sin = Mathf.Sin(angleRad);
            
            // Rotate around Y axis (top-down rotation)
            return new Vector3(
                vector.x * cos + vector.z * sin,   // Rotated X
                vector.y,                           // Y unchanged
                -vector.x * sin + vector.z * cos    // Rotated Z
            );
        }

        /// <summary>
        /// Gets heat smoke component properties
        /// </summary>
        public CompProperties_TurretSmoker GetProps()
        {
            return Props;
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref currentBurstHeat, "currentBurstHeat", 0f);
            Scribe_Values.Look(ref ticksSinceLastShot, "ticksSinceLastShot", 0);
        }
    }
}
