using System;
using UnityEngine;
using Verse;

namespace AbsolutelyMoreCannons
{
    /// <summary>
    /// Custom mote for muzzle smoke with cubic deceleration physics.
    /// Spawned when turret fires, moves in firing direction with custom velocity curve.
    /// </summary>
    public class MoteSmokeMuzzle : MoteThrown
    {
        // Physics parameters
        private Vector3 direction = Vector3.zero;
        private float maxVelocity = 0f;
        private int velDuration = 60;
        private int ticksAlive = 0;
        
        // Telemetry
        private int particleId = -1;
        private Vector3 initialPosition = Vector3.zero;

        /// <summary>
        /// Initialize the mote with physics parameters
        /// </summary>
        public void Setup(
            Vector3 position,
            Vector3 dir,
            float maxVel,
            int duration,
            float size,
            float rotRate,
            int pId)
        {
            exactPosition = position;
            initialPosition = position;
            direction = dir.normalized;
            maxVelocity = maxVel;
            velDuration = duration;
            rotationRate = rotRate;
            particleId = pId;
            ticksAlive = 0;
            // Set scale using base class field (linearScale)
            linearScale = new Vector3(size, 1f, size);

            AMCLogger.LogTurretSmokeParticleTelemetry(
                $"MoteSmokeMuzzle P{particleId} initialized | " +
                $"Pos=({position.x:F2}, {position.z:F2}) | " +
                $"Dir=({dir.x:F3}, {dir.z:F3}) | " +
                $"MaxVel={maxVel:F2}c/s | VelDur={duration} ticks");
        }

        public override void Tick()
        {
            base.Tick();

            // Calculate wind drift (always applied, even after velocity phase)
            Vector3 windMovement = Vector3.zero;
            if (Map != null)
            {
                float windSpeed = Map.windManager.WindSpeed;
                float windDirection = Map.GetComponent<MapComponent_TurretSmokeManager>()?.WindDirection ?? 0f;
                
                // Convert wind to movement vector (cells per tick)
                float windAngleRad = windDirection * Mathf.Deg2Rad;
                Vector3 windVelocity = new Vector3(
                    Mathf.Sin(windAngleRad),
                    0f,
                    Mathf.Cos(windAngleRad)
                ) * windSpeed;
                
                // Convert from cells/second to cells/tick and add small random variation
                windMovement = windVelocity * (1f / 60f) * Rand.Range(0.8f, 1.2f);
            }

            // Apply custom physics during velocity phase
            if (ticksAlive < velDuration)
            {
                // Calculate cubic deceleration: v = v₀ * (1 - t/T)³
                float progress = (float)ticksAlive / velDuration;
                float velocityMultiplier = Mathf.Pow(1f - progress, 3f);
                float currentVelocity = maxVelocity * velocityMultiplier;

                // Combine directional velocity with wind drift
                Vector3 directionalMovement = direction * currentVelocity * (1f / 60f);
                Vector3 movement = directionalMovement + windMovement;
                exactPosition += movement;

                // Telemetry logging
                AMCLogger.LogTurretSmokeParticleTick(
                    $"P{particleId} T{ticksAlive + 1,3}: " +
                    $"Pos=({exactPosition.x:F2},{exactPosition.z:F2}) | " +
                    $"Vel={currentVelocity:F2}c/s | " +
                    $"Dir=({direction.x:F2},{direction.z:F2})");

                ticksAlive++;
            }
            else if (ticksAlive == velDuration)
            {
                // Transition to wind-only motion
                float distanceTraveled = Vector3.Distance(initialPosition, exactPosition);
                
                AMCLogger.LogTurretSmokeParticleTelemetry(
                    $"MoteSmokeMuzzle P{particleId} velocity phase COMPLETE | " +
                    $"Final Pos=({exactPosition.x:F2}, {exactPosition.z:F2}) | " +
                    $"Distance Traveled={distanceTraveled:F2} cells | " +
                    $"Transitioning to wind-only motion");

                AMCLogger.LogTurretSmoke(
                    $"Muzzle mote P{particleId} completed velocity phase, traveled {distanceTraveled:F1} cells");

                ticksAlive++;
            }
            else
            {
                // After velocity phase - only wind drift
                exactPosition += windMovement;
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref direction, "direction", Vector3.zero);
            Scribe_Values.Look(ref maxVelocity, "maxVelocity", 0f);
            Scribe_Values.Look(ref velDuration, "velDuration", 60);
            Scribe_Values.Look(ref ticksAlive, "ticksAlive", 0);
            Scribe_Values.Look(ref particleId, "particleId", -1);
            Scribe_Values.Look(ref initialPosition, "initialPosition", Vector3.zero);
        }
    }
}
