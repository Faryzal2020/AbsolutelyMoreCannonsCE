using System;
using System.Collections.Generic;
using UnityEngine;
using Verse;
using RimWorld;

namespace AbsolutelyMoreCannons
{
    /// <summary>
    /// MapComponent that manages all turret smoke effects on the map.
    /// Handles muzzle particle tracking, heat smoke emission, and wind simulation.
    /// Based on Simple FX Smoke's MapComponent_FleckManager architecture.
    /// </summary>
    public class MapComponent_TurretSmokeManager : MapComponent
    {
        // Registry of all turret s mokers on this map
        private List<CompTurretSmoker> registeredSmokers = new List<CompTurretSmoker>();
        
        // Active muzzle smoke particles being tracked
        private List<TrackedMuzzleParticle> activeMuzzleParticles = new List<TrackedMuzzleParticle>();
        
        // Wind simulation (based on Simple FX Smoke)
        private float windDirection = 35f;
        private float transitiveDirection = 35f;
        private int tickerWindDirection = -1;
        private int ticker = 35;
        
        // Constants for wind simulation
        private const float LOW_WIND = 0.25f;
        private const float HIGH_WIND = 0.75f;
        private const float DIRECTION_STEPS = 0.25f;
        private const float INDOOR_SPEED = 0.5f;
        private const float INDOOR_ANGLE = 35f;
        private const int MAX_ANGLE = 70;
        private const int DIRECTION_WAIT_TIME = 25;
        
        // Particle ID counter for telemetry
        private int nextParticleId = 0;

        /// <summary>
        /// Tracked muzzle particle with spawn delay
        /// </summary>
        private class TrackedMuzzleParticle
        {
            public MuzzleSmokeParticle particle;
            public int delayTicks; // Ticks to wait before spawning
            public int particleId; // Unique ID for logging
            public Vector3 initialPosition; // Starting position for distance calculation
        }

        public MapComponent_TurretSmokeManager(Map map) : base(map)
        {
            transitiveDirection = windDirection = INDOOR_ANGLE;
        }

        /// <summary>
        /// Current wind direction in degrees (for motes to access)
        /// </summary>
        public float WindDirection => windDirection;
        /// <summary>
        /// Register a turret smoker component
        /// </summary>
        public void RegisterSmoker(CompTurretSmoker smoker)
        {
            if (!registeredSmokers.Contains(smoker))
            {
                registeredSmokers.Add(smoker);
                AMCLogger.LogTurretSmoke($"Registered smoker: {smoker.parent.def.defName} (Total: {registeredSmokers.Count})");
            }
        }

        /// <summary>
        /// Unregister a turret smoker component
        /// </summary>
        public void UnregisterSmoker(CompTurretSmoker smoker)
        {
            if (registeredSmokers.Contains(smoker))
            {
                registeredSmokers.Remove(smoker);
                AMCLogger.LogTurretSmoke($"Unregistered smoker: {smoker.parent.def.defName} (Total: {registeredSmokers.Count})");
            }
        }

        /// <summary>
        /// Register a muzzle smoke particle for velocity tracking
        /// </summary>
        public void RegisterMuzzleParticle(MuzzleSmokeParticle particle, int spawnDelay = 0)
        {
            int particleId = nextParticleId++;
            
            AMCLogger.LogTurretSmokeParticleTelemetry(
                $"Particle P{particleId} registered: Delay={spawnDelay} ticks | " +
                $"Pos=({particle.position.x:F2}, {particle.position.z:F2})");
            
            // If no delay, spawn immediately instead of waiting for ticker
            if (spawnDelay == 0)
            {
                var tracked = new TrackedMuzzleParticle
                {
                    particle = particle,
                    delayTicks = 0,
                    particleId = particleId,
                    initialPosition = particle.position
                };
                
                AMCLogger.LogTurretSmokeParticleTelemetry(
                    $"Particle P{particleId} spawning IMMEDIATELY (delay=0)");
                
                SpawnMuzzleMote(tracked);
            }
            else
            {
                // Add to tracking list for delayed spawn
                activeMuzzleParticles.Add(new TrackedMuzzleParticle
                {
                    particle = particle,
                    delayTicks = spawnDelay,
                    particleId = particleId,
                    initialPosition = particle.position
                });
                
                AMCLogger.LogTurretSmokeParticleTelemetry(
                    $"Particle P{particleId} added to queue for delayed spawn ({spawnDelay} ticks)");
            }
        }

        public override void MapComponentTick()
        {
            base.MapComponentTick();
            
            int gameTicks = Find.TickManager.TicksGame;
            
            // Update wind direction
            UpdateWindDirection(gameTicks);
            
            // Process muzzle particle delays EVERY TICK (not just on ticker)
            ProcessMuzzleParticleDelays();
            
            // Variable tick rate based on wind speed
            if (gameTicks % ticker == 0)
            {
                float currentWindSpeed = Mathf.Clamp(map.windManager.WindSpeed, LOW_WIND, HIGH_WIND);
                ticker = (int)(35f / (currentWindSpeed + 0.5f));
                
                // Only process if this is the visible map
                if (Find.CurrentMap == map)
                {
                    ProcessHeatSmoke(gameTicks, currentWindSpeed);
                }
            }
        }

        /// <summary>
        /// Process muzzle particle delay countdown - runs every tick for accurate timing
        /// Spawns immediately when delay reaches 0
        /// </summary>
        private void ProcessMuzzleParticleDelays()
        {
            for (int i = activeMuzzleParticles.Count - 1; i >= 0; i--)
            {
                var tracked = activeMuzzleParticles[i];
                
                if (tracked.delayTicks > 0)
                {
                    tracked.delayTicks--;
                    
                    AMCLogger.LogTurretSmokeParticleTelemetry(
                        $"Particle P{tracked.particleId} delay countdown: {tracked.delayTicks} ticks remaining");
                    
                    // Spawn immediately when delay reaches 0 (don't wait for ticker!)
                    if (tracked.delayTicks == 0)
                    {
                        AMCLogger.LogTurretSmokeParticleTelemetry(
                            $"Particle P{tracked.particleId} ready to spawn (delay reached 0)");
                        
                        SpawnMuzzleMote(tracked);
                        
                        activeMuzzleParticles.RemoveAt(i);
                        
                        AMCLogger.LogTurretSmokeParticleTelemetry(
                            $"Particle P{tracked.particleId} mote spawned and removed from tracking (mote self-manages)");
                    }
                }
            }
        }

        /// <summary>
        /// Process active muzzle smoke particles - spawns custom motes when delay expires
        /// </summary>
        private void ProcessMuzzleParticles(int gameTicks, float windSpeed)
        {
            for (int i = activeMuzzleParticles.Count - 1; i >= 0; i--)
            {
                var tracked = activeMuzzleParticles[i];
                
                // Spawn immediately if delay has reached 0
                if (tracked.delayTicks == 0)
                {
                    AMCLogger.LogTurretSmokeParticleTelemetry(
                        $"Particle P{tracked.particleId} ready to spawn (delay was 0)");
                    
                    // Spawn custom mote with cubic deceleration physics
                    SpawnMuzzleMote(tracked);
                    
                    // Remove from tracking - mote is self-managing now
                    activeMuzzleParticles.RemoveAt(i);
                    
                    AMCLogger.LogTurretSmokeParticleTelemetry(
                        $"Particle P{tracked.particleId} mote spawned and removed from tracking (mote self-manages)");
                }
            }
        }
        
        /// <summary>
        /// Spawns a custom MoteSmokeMuzzle with cubic deceleration physics
        /// </summary>
        private void SpawnMuzzleMote(TrackedMuzzleParticle tracked)
        {
            var particle = tracked.particle;
            
            if (!particle.position.ShouldSpawnMotesAt(map))
            {
                AMCLogger.LogTurretSmokeParticleTelemetry(
                    $"Particle P{tracked.particleId} position {particle.position} should not spawn motes (out of view)");
                return;
            }
            
            try
            {
                // Create custom mote thing
                ThingDef moteDef = DefDatabase<ThingDef>.GetNamed("Mote_AMC_MuzzleSmoke", true);
                MoteSmokeMuzzle mote = (MoteSmokeMuzzle)ThingMaker.MakeThing(moteDef);
                
                // Configure mote with physics parameters
                mote.Setup(
                    position: particle.position,
                    dir: particle.direction,
                    maxVel: particle.maxVelocity,
                    duration: particle.velDuration,
                    size: particle.size,
                    rotRate: particle.rotationRate,
                    pId: tracked.particleId);
                
                // Spawn mote on map
                GenSpawn.Spawn(mote, particle.position.ToIntVec3(), map);
                
                // Calculate angle for logging
                float angleRadians = Mathf.Atan2(particle.direction.x, particle.direction.z);
                float angleDegrees = angleRadians * Mathf.Rad2Deg;
                
                AMCLogger.LogTurretSmokeParticleTelemetry(
                    $"Spawned MoteSmokeMuzzle for P{tracked.particleId} | " +
                    $"Pos=({particle.position.x:F2}, {particle.position.z:F2}) | " +
                    $"Angle={angleDegrees:F1}° | MaxVel={particle.maxVelocity:F2}c/s | " +
                    $"VelDur={particle.velDuration} ticks | Size={particle.size:F1}x");
                    
                AMCLogger.LogTurretSmoke(
                    $"Spawned muzzle mote P{tracked.particleId} at {particle.position}");
            }
            catch (Exception ex)
            {
                Log.Error($"[AMC] Error spawning MoteSmokeMuzzle for P{tracked.particleId}: {ex}");
            }
        }

        /// <summary>
        /// Process heat smoke emission for all hot turrets
        /// </summary>
        private void ProcessHeatSmoke(int gameTicks, float windSpeed)
        {
            foreach (var smoker in registeredSmokers)
            {
                if (smoker.ShouldEmitHeatSmoke())
                {
                    var props = smoker.GetProps();
                    
                    // Get all emission positions for this turret
                    Vector3[] positions = smoker.GetHeatSmokePositions();
                    
                    // Get scaled emission rate based on heat above threshold
                    float scaledEmissionRate = smoker.GetScaledEmissionRate();
                    
                    AMCLogger.LogTurretSmoke(
                        $"Processing heat smoke for {smoker.parent.def.defName} - " +
                        $"{positions.Length} emission points | EmissionRate={scaledEmissionRate:F1}p/s");
                    
                    // Calculate how many particles to spawn this tick based on emission rate
                    // emissionRate is particles per second, ticker varies based on wind
                    // Distribute spawning probability across all emission points
                    float particlesPerTick = scaledEmissionRate / 60f;
                    float probabilityPerPoint = (particlesPerTick * ticker) / positions.Length;
                    
                    int spawnedCount = 0;
                    // Try to spawn from each emission point
                    for (int i = 0; i < positions.Length; i++)
                    {
                        if (Rand.Value < probabilityPerPoint)
                        {
                            SpawnDefaultWindFleck(positions[i], props.heatFleckDef, props.heatParticleSize, windSpeed);
                            spawnedCount++;
                            
                            AMCLogger.LogTurretSmoke(
                                $"  Heat smoke spawned at point {i}: ({positions[i].x:F2}, {positions[i].z:F2})");
                        }
                    }
                    
                    if (spawnedCount > 0)
                    {
                        AMCLogger.LogTurretSmoke(
                            $"Total heat smoke spawned: {spawnedCount}/{positions.Length} points");
                    }
                }
            }
        }

        /// <summary>
        /// Spawns a fleck with default wind-based motion (like campfire smoke)
        /// </summary>
        private void SpawnDefaultWindFleck(Vector3 position, FleckDef fleckDef, float size, float windSpeed)
        {
            // Check if position should spawn motes
            if (!position.ShouldSpawnMotesAt(map))
                return;

            float angle = windDirection + Rand.Range(-5f, 5f);
            float rotationRate = Rand.Range(-30f, 30f);
            float speed = windSpeed;
            
            // Check if roofed for indoor behavior
            bool isRoofed = map.roofGrid.Roofed(position.ToIntVec3());
            if (isRoofed)
            {
                speed = INDOOR_SPEED;
                angle = INDOOR_ANGLE + Rand.Range(-5f, 5f);
            }
            
            FleckCreationData data = FleckMaker.GetDataStatic(position, map, fleckDef, size);
            data.rotationRate = rotationRate;
            data.velocityAngle = angle;
            data.velocitySpeed = (Rand.Range(1, 20) / 100f) + speed;
            map.flecks.CreateFleck(data);
        }

        /// <summary>
        /// Updates wind direction for default smoke motion (from Simple FX Smoke)
        /// </summary>
        private void UpdateWindDirection(int gameTicks)
        {
            if (gameTicks % 25 == 0)
            {
                // Slowly move direction toward target angle
                if (windDirection != transitiveDirection)
                {
                    windDirection = windDirection > transitiveDirection 
                        ? windDirection - DIRECTION_STEPS 
                        : windDirection + DIRECTION_STEPS;
                }

                // If wind is high, set timer and return
                if (map.windManager.WindSpeed > LOW_WIND)
                {
                    tickerWindDirection = DIRECTION_WAIT_TIME;
                    return;
                }

                // Change target angle when it has been low wind for a while
                if (--tickerWindDirection == 0)
                {
                    transitiveDirection = Rand.Range(-MAX_ANGLE, MAX_ANGLE);
                }
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref windDirection, "windDirection", 35f);
            Scribe_Values.Look(ref transitiveDirection, "transitiveDirection", 35f);
            Scribe_Values.Look(ref tickerWindDirection, "tickerDirection", -1);
        }
    }
}
