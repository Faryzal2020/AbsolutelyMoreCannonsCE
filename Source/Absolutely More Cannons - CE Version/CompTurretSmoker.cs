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
                    SpawnShockwaveSmoke();
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
                    
                    // Get randomized size
                    float particleSize = Props.GetRandomParticleSize();
                    
                    var particle = new MuzzleSmokeParticle
                    {
                        position = spawnPosition,
                        direction = particleDirection,
                        maxVelocity = Props.muzzleVelocity,
                        velDuration = Props.muzzleVelDuration,
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
        /// Spawns radial shockwave smoke burst around muzzle (filled circle with density scaling)
        /// </summary>
        private void SpawnShockwaveSmoke()
        {
            try
            {
                // Get current turret rotation
                float turretRotation = 0f;
                if (barrelComp != null)
                {
                    turretRotation = barrelComp.GetCurrentBarrelRotation();
                }
                
                // Rotate offset by turret direction
                Vector3 rotatedOffset = RotateVector(Props.shockwaveOffset, turretRotation);
                Vector3 centerPosition = parent.DrawPos + rotatedOffset;
                
                int totalParticles = 0;
                
                // Create filled circle by spawning at multiple radii
                // Density scales linearly: at radius r, particles = density × (r / maxRadius)
                int radiusSteps = Mathf.CeilToInt(Props.shockwaveRadius);
                
                for (int r = 1; r <= radiusSteps; r++)
                {
                    float currentRadius = (Props.shockwaveRadius / radiusSteps) * r;
                    float densityScale = (float)r / radiusSteps;
                    int particlesAtRadius = Mathf.RoundToInt(Props.shockwaveDensity * densityScale);
                    
                    // Need at least a few particles per ring
                    particlesAtRadius = Mathf.Max(particlesAtRadius, r == radiusSteps ? Props.shockwaveDensity : 4);
                    
                    // Spawn particles around this radius
                    for (int i = 0; i < particlesAtRadius; i++)
                    {
                        float angle = (360f / particlesAtRadius) * i;
                        // Add small random variation to angle
                        angle += Rand.Range(-5f, 5f);
                        float angleRad = angle * Mathf.Deg2Rad;
                        
                        // Add small random variation to radius
                        float radiusVariation = currentRadius * Rand.Range(0.9f, 1.1f);
                        
                        Vector3 offset = new Vector3(
                            Mathf.Cos(angleRad) * radiusVariation,
                            0f,
                            Mathf.Sin(angleRad) * radiusVariation
                        );
                        
                        Vector3 particlePos = centerPosition + offset;
                        
                        // Spawn fleck
                        FleckCreationData data = FleckMaker.GetDataStatic(particlePos, cachedMap, Props.shockwaveFleckDef, Props.shockwaveParticleSize);
                        data.rotationRate = Rand.Range(-20f, 20f);
                        data.velocityAngle = Rand.Range(0f, 360f);
                        data.velocitySpeed = Rand.Range(0.1f, 0.3f);
                        cachedMap.flecks.CreateFleck(data);
                        
                        totalParticles++;
                    }
                }

                AMCLogger.LogTurretSmoke(
                    $"Spawned shockwave smoke for {parent.def.defName} - " +
                    $"Radius: {Props.shockwaveRadius}, Total particles: {totalParticles} (across {radiusSteps} rings)");
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
            float turretRotation = 0f;
            int barrelCount = 1;
            
            if (barrelComp != null)
            {
                turretRotation = barrelComp.GetCurrentBarrelRotation();
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
